using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;
using PresentationTimer.Remote.Authentication;
using PresentationTimer.Remote.Hosting;
using PresentationTimer.Remote.Hubs;
using PresentationTimer.Remote.Networking;
using PresentationTimer.Remote.Security;

namespace PresentationTimer.Remote;

/// <summary>
/// Owns one explicit, ephemeral, authenticated local presenter web session.
/// </summary>
public sealed partial class RemoteSessionHost : IRemoteSessionHost, IAsyncDisposable
{
    private readonly SessionCredentialStore _credentials = new SessionCredentialStore();
    private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
    private readonly INetworkAddressProvider _networkAddresses;
    private readonly Func<IPresentationSessionService> _sessionServiceAccessor;
    private readonly bool _allowLoopbackPairing;
    private readonly List<WebApplication> _applications = new List<WebApplication>();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RemoteSessionHost> _logger;
    private readonly TimeSpan _networkChangeDebounceInterval;
    private readonly Timer _networkChangeDebounceTimer;
    private readonly INetworkChangeNotifier _networkChangeNotifier;
    private RemoteConnectionTracker? _connectionTracker;
    private int _disposeState;
    private string? _pairingToken;
    private RemoteSessionPublicState _state = RemoteSessionPublicState.Initial;

    /// <summary>Initializes a new instance of the <see cref="RemoteSessionHost"/> class.</summary>
    /// <param name="sessionServiceAccessor">Resolves the application service after composition completes.</param>
    public RemoteSessionHost(Func<IPresentationSessionService> sessionServiceAccessor)
        : this(
            sessionServiceAccessor,
            new NetworkAddressProvider(),
            allowLoopbackPairing: false,
            NullLoggerFactory.Instance,
            new NetworkChangeNotifier())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteSessionHost"/> class connected to the
    /// process logging pipeline.
    /// </summary>
    /// <param name="sessionServiceAccessor">Resolves the application service after composition completes.</param>
    /// <param name="loggerFactory">The process logging factory.</param>
    public RemoteSessionHost(
        Func<IPresentationSessionService> sessionServiceAccessor,
        ILoggerFactory loggerFactory)
        : this(
            sessionServiceAccessor,
            new NetworkAddressProvider(),
            allowLoopbackPairing: false,
            loggerFactory,
            new NetworkChangeNotifier())
    {
    }

    internal RemoteSessionHost(
        Func<IPresentationSessionService> sessionServiceAccessor,
        INetworkAddressProvider networkAddresses,
        bool allowLoopbackPairing,
        ILoggerFactory? loggerFactory = null,
        INetworkChangeNotifier? networkChangeNotifier = null,
        TimeSpan? networkChangeDebounceInterval = null)
    {
        ArgumentNullException.ThrowIfNull(sessionServiceAccessor);
        ArgumentNullException.ThrowIfNull(networkAddresses);
        this._sessionServiceAccessor = sessionServiceAccessor;
        this._networkAddresses = networkAddresses;
        this._allowLoopbackPairing = allowLoopbackPairing;
        this._loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        this._logger = this._loggerFactory.CreateLogger<RemoteSessionHost>();
        this._networkChangeNotifier = networkChangeNotifier ?? new NetworkChangeNotifier();
        this._networkChangeDebounceInterval = networkChangeDebounceInterval ?? TimeSpan.FromMilliseconds(750);
        if (this._networkChangeDebounceInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(networkChangeDebounceInterval),
                networkChangeDebounceInterval,
                "The network-change debounce interval must be positive.");
        }

        this._networkChangeNotifier.Changed += this.OnNetworkAddressChanged;
        this._networkChangeDebounceTimer = new Timer(
            static host => _ = ((RemoteSessionHost)host!).RebindAfterNetworkChangeAsync(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc/>
    public event Action<DesktopPairingDescriptor?>? PairingChanged;

    /// <inheritdoc/>
    public event Action<RemoteSessionPublicState>? StateChanged;

    /// <inheritdoc/>
    public RemoteSessionPublicState State => Volatile.Read(ref this._state);

    /// <inheritdoc/>
    public async Task<OperationResult<DesktopPairingDescriptor>> StartAsync(
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref this._disposeState) != 0)
        {
            return OperationResult.Failure<DesktopPairingDescriptor>(
                ErrorCodes.RemoteStartFailed,
                "The remote presenter host is stopping.");
        }

        await this._lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._applications.Count > 0)
            {
                return OperationResult.Failure<DesktopPairingDescriptor>(
                    ErrorCodes.RemoteStartFailed,
                    "A remote session is already active.");
            }

            LogSessionStarting(this._logger);
            this.Publish(new RemoteSessionPublicState(
                RemoteSessionStatus.Starting,
                ImmutableArray<Uri>.Empty,
                null,
                0,
                null));
            this._pairingToken = this._credentials.CreateSession();
            IReadOnlyList<NetworkAddressCandidate> candidates = this._networkAddresses.GetCandidates();
            this._connectionTracker = new RemoteConnectionTracker();
            this._connectionTracker.CountChanged += this.OnConnectionCountChanged;
            ImmutableArray<Uri> boundCandidates = await this.BindApplicationsAsync(
                candidates,
                cancellationToken).ConfigureAwait(false);
            if (boundCandidates.IsEmpty)
            {
                this._credentials.Revoke();
                this._pairingToken = null;
                this.PublishPairing(null);
                await this.StopApplicationsAsync(cancellationToken).ConfigureAwait(false);
                this.Publish(new RemoteSessionPublicState(
                    RemoteSessionStatus.Failed,
                    ImmutableArray<Uri>.Empty,
                    null,
                    0,
                    ErrorCodes.RemoteNoLanAddress));
                return OperationResult.Failure<DesktopPairingDescriptor>(
                    ErrorCodes.RemoteNoLanAddress,
                    "No usable local IPv4 address is available for phone pairing.");
            }

            Uri selectedUrl = boundCandidates[0];
            DesktopPairingDescriptor descriptor = CreatePairingDescriptor(
                boundCandidates,
                candidates,
                this._pairingToken);
            this.Publish(new RemoteSessionPublicState(
                RemoteSessionStatus.Ready,
                boundCandidates,
                selectedUrl,
                0,
                null));
            this.PublishPairing(descriptor);
            this._networkChangeNotifier.Start();
            LogSessionReady(
                this._logger,
                boundCandidates.Length,
                selectedUrl.Host,
                selectedUrl.Port);
            return OperationResult.Success(descriptor);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this._credentials.Revoke();
            this._pairingToken = null;
            this.PublishPairing(null);
            throw;
        }
        catch (Exception exception)
        {
            LogSessionStartFailed(this._logger, exception);
            this._credentials.Revoke();
            this._pairingToken = null;
            this.PublishPairing(null);
            await this.DisposeApplicationAfterFailedStartAsync().ConfigureAwait(false);
            this.Publish(new RemoteSessionPublicState(
                RemoteSessionStatus.Failed,
                ImmutableArray<Uri>.Empty,
                null,
                0,
                ErrorCodes.RemoteStartFailed));
            return OperationResult.Failure<DesktopPairingDescriptor>(
                ErrorCodes.RemoteStartFailed,
                "The local presenter server could not start.");
        }
        finally
        {
            this._lifecycleGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await this._lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LogSessionStopping(this._logger);
            this._networkChangeNotifier.Stop();
            _ = this._networkChangeDebounceTimer.Change(
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            this._credentials.Revoke();
            this._pairingToken = null;
            this.PublishPairing(null);
            int applicationCount = this._applications.Count;
            this.Publish(new RemoteSessionPublicState(
                applicationCount == 0 ? RemoteSessionStatus.Stopped : RemoteSessionStatus.Stopping,
                ImmutableArray<Uri>.Empty,
                null,
                0,
                null));

            if (this._connectionTracker is not null)
            {
                this._connectionTracker.CountChanged -= this.OnConnectionCountChanged;
                this._connectionTracker = null;
            }

            await this.StopApplicationsAsync(cancellationToken).ConfigureAwait(false);

            this.Publish(RemoteSessionPublicState.Initial);
            LogSessionStopped(this._logger);
        }
        finally
        {
            this._lifecycleGate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this._disposeState, 1) != 0)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await this.StopAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogDisposalFailed(this._logger, exception);
        }

        this._networkChangeNotifier.Changed -= this.OnNetworkAddressChanged;
        this._networkChangeNotifier.Stop();
        this._networkChangeDebounceTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static DesktopPairingDescriptor CreatePairingDescriptor(
        ImmutableArray<Uri> boundCandidateUrls,
        IReadOnlyList<NetworkAddressCandidate> requestedCandidates,
        string pairingToken)
    {
        if (boundCandidateUrls.IsEmpty)
        {
            throw new ArgumentException("At least one bound candidate URL is required.", nameof(boundCandidateUrls));
        }

        ArgumentNullException.ThrowIfNull(requestedCandidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(pairingToken);
        var labelsByAddress = requestedCandidates.ToDictionary(
            static candidate => candidate.Address.ToString(),
            static candidate => candidate.InterfaceLabel,
            StringComparer.Ordinal);
        ImmutableArray<DesktopPairingCandidate> candidates = boundCandidateUrls
            .Select(url =>
            {
                string label = labelsByAddress.GetValueOrDefault(url.Host, "Localhost (diagnostic)");
                Uri pairingUri = BuildPairingUri(url, pairingToken);
                return new DesktopPairingCandidate(label, pairingUri, pairingUri.AbsoluteUri)
                {
                    QrPng = PairingQrCodeGenerator.CreatePng(pairingUri.AbsoluteUri),
                };
            })
            .ToImmutableArray();
        DesktopPairingCandidate selected = candidates[0];
        return new DesktopPairingDescriptor(selected.PairingUri, selected.QrPayload)
        {
            QrPng = selected.QrPng,
            Candidates = candidates,
        };
    }

    private static Uri BuildPairingUri(Uri selectedUrl, string pairingToken)
    {
        string value = QueryHelpers.AddQueryString(
            new Uri(selectedUrl, "/pair").AbsoluteUri,
            "t",
            pairingToken);
        return new Uri(value, UriKind.Absolute);
    }

    private static ImmutableArray<Uri> GetBoundCandidateUrls(
        IReadOnlyList<WebApplication> applications,
        IReadOnlyList<NetworkAddressCandidate> requestedCandidates,
        bool allowLoopbackPairing)
    {
        var requested = requestedCandidates
            .Select(static candidate => candidate.Address)
            .ToHashSet();

        return applications.SelectMany(static application =>
        {
            IServer server = application.Services.GetRequiredService<IServer>();
            IServerAddressesFeature? addresses = server.Features.Get<IServerAddressesFeature>();
            return addresses?.Addresses ?? Array.Empty<string>();
        })
            .Select(static address => new Uri(address, UriKind.Absolute))
            .Where(uri => IPAddress.TryParse(uri.Host, out IPAddress? address) &&
                (requested.Contains(address) || (allowLoopbackPairing && IPAddress.IsLoopback(address))))
            .OrderBy(static uri => IPAddress.IsLoopback(IPAddress.Parse(uri.Host)))
            .ThenBy(static uri => uri.AbsoluteUri, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static async Task StartEndpointAsync(
        WebApplication application,
        CancellationToken cancellationToken)
    {
        using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupTimeout.CancelAfter(TimeSpan.FromSeconds(3));
        await application.StartAsync(startupTimeout.Token).ConfigureAwait(false);
    }

    private static bool IsSameOrigin(HttpContext context)
    {
        string? originValue = context.Request.Headers.Origin.FirstOrDefault();
        if (!Uri.TryCreate(originValue, UriKind.Absolute, out Uri? origin))
        {
            return false;
        }

        string expected = $"{context.Request.Scheme}://{context.Request.Host}";
        return string.Equals(
            origin.GetLeftPart(UriPartial.Authority),
            expected,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void MapEndpoints(
        WebApplication application,
        SessionCredentialStore credentials,
        ILogger<RemoteSessionHost> logger)
    {
        byte[] index = EmbeddedWebAssets.Read("index.html");
        byte[] styles = EmbeddedWebAssets.Read("assets/presenter.css");
        byte[] script = EmbeddedWebAssets.Read("assets/presenter.js");
        byte[] signalR = EmbeddedWebAssets.Read("vendor/signalr.min.js");

        application.MapGet("/health", static () => Results.Json(new { status = "ok" }))
            .AllowAnonymous();
        application.MapGet("/pair", (HttpContext context) =>
        {
            string? pairingToken = context.Request.Query["t"].FirstOrDefault();
            if (!credentials.TryExchangePairingToken(pairingToken, out string? browserCredential))
            {
                LogPairingRejected(logger);
                return Results.Json(
                    new { error = "invalid_or_expired" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            context.Response.Cookies.Append(
                SessionCredentialStore.CookieName,
                browserCredential!,
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    Path = "/",
                    SameSite = SameSiteMode.Strict,
                    Secure = false,
                });
            LogPairingAccepted(logger);
            return Results.Redirect("/presenter");
        }).AllowAnonymous();
        application.MapGet("/presenter", () => Results.Bytes(index, "text/html; charset=utf-8"))
            .RequireAuthorization();
        application.MapGet("/assets/presenter.css", () => Results.Bytes(styles, "text/css; charset=utf-8"))
            .RequireAuthorization();
        application.MapGet("/assets/presenter.js", () => Results.Bytes(script, "text/javascript; charset=utf-8"))
            .RequireAuthorization();
        application.MapGet("/vendor/signalr.min.js", () => Results.Bytes(signalR, "text/javascript; charset=utf-8"))
            .RequireAuthorization();
        application.MapHub<PresenterHub>("/presenterHub", options =>
        {
            options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
        }).RequireAuthorization();
    }

    [LoggerMessage(2000, LogLevel.Information, "Starting a new authenticated remote presenter session")]
    private static partial void LogSessionStarting(ILogger logger);

    [LoggerMessage(2001, LogLevel.Warning, "Remote endpoint binding failed for address {Address}")]
    private static partial void LogEndpointBindingFailed(ILogger logger, string address);

    [LoggerMessage(
        2002,
        LogLevel.Information,
        "Remote presenter session ready on {EndpointCount} endpoint(s); selected {Address}:{Port}")]
    private static partial void LogSessionReady(
        ILogger logger,
        int endpointCount,
        string address,
        int port);

    [LoggerMessage(2003, LogLevel.Error, "Remote presenter session failed to start")]
    private static partial void LogSessionStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(2004, LogLevel.Information, "Stopping the remote presenter session")]
    private static partial void LogSessionStopping(ILogger logger);

    [LoggerMessage(2005, LogLevel.Information, "Remote presenter session stopped")]
    private static partial void LogSessionStopped(ILogger logger);

    [LoggerMessage(2006, LogLevel.Warning, "Remote presenter disposal exceeded its cleanup boundary")]
    private static partial void LogDisposalFailed(ILogger logger, Exception exception);

    [LoggerMessage(2007, LogLevel.Warning, "Remote presenter pairing credential was rejected")]
    private static partial void LogPairingRejected(ILogger logger);

    [LoggerMessage(2008, LogLevel.Information, "Remote presenter pairing credential was accepted")]
    private static partial void LogPairingAccepted(ILogger logger);

    [LoggerMessage(2009, LogLevel.Information, "Rebinding remote presenter endpoints after a network change")]
    private static partial void LogNetworkRebinding(ILogger logger);

    [LoggerMessage(2010, LogLevel.Warning, "No phone-reachable endpoint is available after a network change")]
    private static partial void LogNetworkRebindUnavailable(ILogger logger);

    [LoggerMessage(
        2011,
        LogLevel.Information,
        "Remote presenter endpoints rebound; selected {Address}:{Port}")]
    private static partial void LogNetworkRebindCompleted(ILogger logger, string address, int port);

    [LoggerMessage(2012, LogLevel.Error, "Remote presenter endpoint rebinding failed")]
    private static partial void LogNetworkRebindFailed(ILogger logger, Exception exception);

    private WebApplication BuildApplication(
        IPAddress endpointAddress,
        RemoteConnectionTracker connectionTracker)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RemoteSessionHost).Assembly.GetName().Name,
            Args = Array.Empty<string>(),
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ForwardingLoggerProvider(this._loggerFactory));
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Listen(endpointAddress, 0);
        });
        builder.Services.AddSingleton(this._credentials);
        builder.Services.AddSingleton(connectionTracker);
        builder.Services.AddSingleton(this._sessionServiceAccessor());
        builder.Services.AddSignalR();
        builder.Services.AddAuthentication(SessionAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, SessionCookieAuthenticationHandler>(
                SessionAuthenticationDefaults.Scheme,
                static _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHostedService<PresenterStateBroadcaster>();

        WebApplication application = builder.Build();
        application.Use(async (context, next) =>
        {
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (context.Request.Path.StartsWithSegments("/presenterHub") &&
                context.Request.Headers.Origin.Count > 0 &&
                !IsSameOrigin(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });
        application.UseAuthentication();
        application.UseAuthorization();
        MapEndpoints(application, this._credentials, this._logger);
        return application;
    }

    private async Task<ImmutableArray<Uri>> BindApplicationsAsync(
        IReadOnlyList<NetworkAddressCandidate> candidates,
        CancellationToken cancellationToken)
    {
        RemoteConnectionTracker tracker = this._connectionTracker ??
            throw new InvalidOperationException("The remote connection tracker is unavailable.");
        WebApplication loopbackApplication = this.BuildApplication(IPAddress.Loopback, tracker);
        this._applications.Add(loopbackApplication);
        await StartEndpointAsync(loopbackApplication, cancellationToken).ConfigureAwait(false);

        foreach (NetworkAddressCandidate candidate in candidates)
        {
            WebApplication candidateApplication = this.BuildApplication(candidate.Address, tracker);
            this._applications.Add(candidateApplication);
            try
            {
                await StartEndpointAsync(candidateApplication, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogEndpointBindingFailed(this._logger, candidate.Address.ToString());
                this._applications.Remove(candidateApplication);
                await candidateApplication.DisposeAsync().ConfigureAwait(false);
            }
        }

        return GetBoundCandidateUrls(this._applications, candidates, this._allowLoopbackPairing);
    }

    private async Task RebindAfterNetworkChangeAsync()
    {
        if (Volatile.Read(ref this._disposeState) != 0)
        {
            return;
        }

        await this._lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            string? pairingToken = this._pairingToken;
            if (pairingToken is null ||
                this.State.Status is RemoteSessionStatus.Stopped or RemoteSessionStatus.Stopping)
            {
                return;
            }

            LogNetworkRebinding(this._logger);
            this.PublishPairing(null);
            this.Publish(new RemoteSessionPublicState(
                RemoteSessionStatus.Starting,
                ImmutableArray<Uri>.Empty,
                null,
                0,
                null));
            await this.StopApplicationsAsync(CancellationToken.None).ConfigureAwait(false);
            IReadOnlyList<NetworkAddressCandidate> candidates = this._networkAddresses.GetCandidates();
            ImmutableArray<Uri> boundCandidates = await this.BindApplicationsAsync(
                candidates,
                CancellationToken.None).ConfigureAwait(false);
            if (boundCandidates.IsEmpty)
            {
                await this.StopApplicationsAsync(CancellationToken.None).ConfigureAwait(false);
                this.Publish(new RemoteSessionPublicState(
                    RemoteSessionStatus.Failed,
                    ImmutableArray<Uri>.Empty,
                    null,
                    0,
                    ErrorCodes.RemoteNoLanAddress));
                LogNetworkRebindUnavailable(this._logger);
                return;
            }

            Uri selectedUrl = boundCandidates[0];
            DesktopPairingDescriptor descriptor = CreatePairingDescriptor(
                boundCandidates,
                candidates,
                pairingToken);
            this.Publish(new RemoteSessionPublicState(
                RemoteSessionStatus.Ready,
                boundCandidates,
                selectedUrl,
                0,
                null));
            this.PublishPairing(descriptor);
            LogNetworkRebindCompleted(this._logger, selectedUrl.Host, selectedUrl.Port);
        }
        catch (Exception exception)
        {
            await this.StopApplicationsAsync(CancellationToken.None).ConfigureAwait(false);
            this.PublishPairing(null);
            this.Publish(new RemoteSessionPublicState(
                RemoteSessionStatus.Failed,
                ImmutableArray<Uri>.Empty,
                null,
                0,
                ErrorCodes.RemoteStartFailed));
            LogNetworkRebindFailed(this._logger, exception);
        }
        finally
        {
            this._lifecycleGate.Release();
        }
    }

    private async Task StopApplicationsAsync(CancellationToken cancellationToken)
    {
        WebApplication[] applications = this._applications.ToArray();
        this._applications.Clear();
        foreach (WebApplication application in applications)
        {
            using var stopTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stopTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                await application.StopAsync(stopTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Continue disposing every independently bound endpoint.
            }
            finally
            {
                try
                {
                    await application.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private async Task DisposeApplicationAfterFailedStartAsync()
    {
        if (this._connectionTracker is not null)
        {
            this._connectionTracker.CountChanged -= this.OnConnectionCountChanged;
            this._connectionTracker = null;
        }

        foreach (WebApplication application in this._applications)
        {
            await application.DisposeAsync().ConfigureAwait(false);
        }

        this._applications.Clear();
    }

    private void OnConnectionCountChanged(int count)
    {
        RemoteSessionPublicState current = this.State;
        if (current.Status == RemoteSessionStatus.Ready)
        {
            this.Publish(current with { AuthenticatedConnectionCount = count });
        }
    }

    private void OnNetworkAddressChanged()
    {
        if (this._pairingToken is null || Volatile.Read(ref this._disposeState) != 0)
        {
            return;
        }

        try
        {
            _ = this._networkChangeDebounceTimer.Change(
                this._networkChangeDebounceInterval,
                Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void PublishPairing(DesktopPairingDescriptor? descriptor) =>
        this.PairingChanged?.Invoke(descriptor);

    private void Publish(RemoteSessionPublicState state)
    {
        RemoteSessionPublicState previous = Interlocked.Exchange(ref this._state, state);
        if (previous != state)
        {
            this.StateChanged?.Invoke(state);
        }
    }
}
