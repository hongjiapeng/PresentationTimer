using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;
using PresentationTimer.Core.Services;
using PresentationTimer.Core.Timing;
using PresentationTimer.Remote.Dtos;
using PresentationTimer.Remote.Networking;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using RemoteHost = PresentationTimer.Remote.RemoteSessionHost;

namespace PresentationTimer.Remote.Tests.Integration;

/// <summary>Exercises the real loopback Kestrel host and authentication boundary.</summary>
[TestClass]
public sealed class RemoteHostIntegrationTests
{
    /// <summary>Verifies public health is harmless and presenter content requires authentication.</summary>
    /// <returns>A task that completes after the HTTP checks.</returns>
    [TestMethod]
    public async Task HttpEndpoints_WithoutCredential_ExposeOnlyHealth()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = context.BaseUri,
        };

        // Act
        HttpResponseMessage health = await client.GetAsync("/health");
        HttpResponseMessage presenter = await client.GetAsync("/presenter");
        HttpResponseMessage missingPairToken = await client.GetAsync("/pair");
        HttpResponseMessage malformedPairToken = await client.GetAsync("/pair?t=not-base64!");
        string incorrectToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        HttpResponseMessage incorrectPairToken = await client.GetAsync($"/pair?t={incorrectToken}");
        HttpResponseMessage commandProbe = await client.GetAsync("/next");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);
        string healthBody = await health.Content.ReadAsStringAsync();
        StringAssert.Contains(healthBody, "ok");
        Assert.IsFalse(healthBody.Contains("notes", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(HttpStatusCode.Unauthorized, presenter.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, missingPairToken.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, malformedPairToken.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, incorrectPairToken.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, commandProbe.StatusCode);
    }

    /// <summary>Verifies valid pairing exchanges into a strict HttpOnly cookie and offline assets load.</summary>
    /// <returns>A task that completes after the pairing flow.</returns>
    [TestMethod]
    public async Task Pairing_WithCurrentToken_IssuesCookieAndLoadsOfflinePresenter()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };
        using var client = new HttpClient(handler);

        // Act
        HttpResponseMessage pairing = await client.GetAsync(context.Descriptor.PairingUri);
        HttpResponseMessage presenter = await client.GetAsync(new Uri(context.BaseUri, "/presenter"));
        HttpResponseMessage signalRScript = await client.GetAsync(
            new Uri(context.BaseUri, "/vendor/signalr.min.js"));
        HttpResponseMessage presenterScript = await client.GetAsync(
            new Uri(context.BaseUri, "/assets/presenter.js"));

        // Assert
        Assert.AreEqual(HttpStatusCode.Redirect, pairing.StatusCode);
        Assert.AreEqual("/presenter", pairing.Headers.Location?.OriginalString);
        string setCookie = string.Join(";", pairing.Headers.GetValues("Set-Cookie"));
        StringAssert.Contains(setCookie, "httponly", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(setCookie, "samesite=strict", StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(HttpStatusCode.OK, presenter.StatusCode);
        string html = await presenter.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "/vendor/signalr.min.js");
        Assert.IsFalse(html.Contains("https://", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(HttpStatusCode.OK, signalRScript.StatusCode);
        string browserScript = await presenterScript.Content.ReadAsStringAsync();
        StringAssert.Contains(browserScript, "notes.textContent");
        Assert.IsFalse(browserScript.Contains("notes.innerHTML", StringComparison.Ordinal));
        Assert.AreEqual(context.Descriptor.PairingUri.AbsoluteUri, context.Descriptor.QrPayload);
        Assert.IsGreaterThan(100, context.Descriptor.QrPng.Length);
    }

    /// <summary>Verifies old QR and cookie credentials fail after End and a new Start.</summary>
    /// <returns>A task that completes after session rotation.</returns>
    [TestMethod]
    public async Task RestartSession_InvalidatesPriorPairingTokenAndCookie()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };
        using var client = new HttpClient(handler);
        _ = await client.GetAsync(context.Descriptor.PairingUri);
        string oldToken = QueryHelpers.ParseQuery(context.Descriptor.PairingUri.Query)["t"].ToString();

        // Act
        await context.SessionService.EndRemoteSessionAsync();
        OperationResult<DesktopPairingDescriptor> restarted =
            await context.SessionService.StartRemoteSessionAsync();
        Assert.IsTrue(restarted.IsSuccess);
        DesktopPairingDescriptor newDescriptor = restarted.Value!;
        Uri newBaseUri = context.Host.State.SelectedUrl!;
        Uri oldTokenOnNewHost = new Uri(
            QueryHelpers.AddQueryString(new Uri(newBaseUri, "/pair").AbsoluteUri, "t", oldToken));
        HttpResponseMessage oldPairing = await client.GetAsync(oldTokenOnNewHost);
        HttpResponseMessage oldCookie = await client.GetAsync(new Uri(newBaseUri, "/presenter"));

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, oldPairing.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, oldCookie.StatusCode);
        Assert.AreNotEqual(context.Descriptor.PairingUri, newDescriptor.PairingUri);
    }

    /// <summary>Verifies authenticated SignalR state, exactly-once command routing, and broadcast.</summary>
    /// <returns>A task that completes after the SignalR vertical slice.</returns>
    [TestMethod]
    public async Task SignalR_WithPairedCookie_RoutesNextOnceAndBroadcastsAuthoritativeState()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        var cookies = new CookieContainer();
        using (var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            UseCookies = true,
        }))
        {
            _ = await client.GetAsync(context.Descriptor.PairingUri);
        }

        var broadcasts = Channel.CreateUnbounded<PresenterStateDto>();
        await using HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(context.BaseUri, "/presenterHub"), options =>
            {
                options.Cookies = cookies;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        connection.On<PresenterStateDto>("stateChanged", state => broadcasts.Writer.TryWrite(state));
        await connection.StartAsync();

        // Act
        PresenterStateDto initial = await connection.InvokeAsync<PresenterStateDto>("GetState");
        PresenterCommandResultDto result =
            await connection.InvokeAsync<PresenterCommandResultDto>("Next");
        PresenterStateDto updated = await ReadUntilAsync(
            broadcasts.Reader,
            static state => state.CurrentSlideIndex == 2,
            TimeSpan.FromSeconds(3));

        // Assert
        Assert.AreEqual(1, initial.CurrentSlideIndex);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, context.Presentation.NextCalls);
        Assert.AreEqual(2, updated.CurrentSlideIndex);
        Assert.IsGreaterThan(initial.Revision, updated.Revision);
    }

    /// <summary>Verifies that the protected hub cannot be connected without a browser credential.</summary>
    /// <returns>A task that completes after the rejected negotiation.</returns>
    [TestMethod]
    public async Task SignalR_WithoutPairedCookie_RejectsConnection()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        await using HubConnection connection = BuildConnection(context.BaseUri, new CookieContainer());

        // Act and assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());
        Assert.AreEqual(0, context.SessionService.State.Remote.AuthenticatedConnectionCount);
    }

    /// <summary>Verifies reconnect performs a full authoritative resync and Previous is routed once.</summary>
    /// <returns>A task that completes after reconnect and navigation.</returns>
    [TestMethod]
    public async Task SignalR_Reconnect_ResynchronizesAndRoutesPreviousOnce()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        CookieContainer cookies = await PairAsync(context);
        await using HubConnection connection = BuildConnection(context.BaseUri, cookies);
        await connection.StartAsync();
        PresenterStateDto initial = await connection.InvokeAsync<PresenterStateDto>("GetState");
        await WaitForConnectionCountAsync(context, 1);

        // Act
        await connection.StopAsync();
        await WaitForConnectionCountAsync(context, 0);
        context.Presentation.SetSlide(3, "latest <script>notes</script>");
        await connection.StartAsync();
        PresenterStateDto resynchronized = await connection.InvokeAsync<PresenterStateDto>("GetState");
        PresenterCommandResultDto result =
            await connection.InvokeAsync<PresenterCommandResultDto>("Previous");

        // Assert
        Assert.AreEqual(1, initial.CurrentSlideIndex);
        Assert.AreEqual(3, resynchronized.CurrentSlideIndex);
        Assert.AreEqual("latest <script>notes</script>", resynchronized.SpeakerNotes);
        Assert.IsGreaterThan(initial.Revision, resynchronized.Revision);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, context.Presentation.PreviousCalls);
        await WaitForConnectionCountAsync(context, 1);
    }

    /// <summary>Verifies endpoint cleanup remains idempotent while a phone is connected.</summary>
    /// <returns>A task that completes after repeated host shutdown.</returns>
    [TestMethod]
    public async Task StopAsync_WithConnectedClient_IsBoundedAndIdempotent()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        CookieContainer cookies = await PairAsync(context);
        await using HubConnection connection = BuildConnection(context.BaseUri, cookies);
        await connection.StartAsync();
        await WaitForConnectionCountAsync(context, 1);

        // Act
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await context.Host.StopAsync(timeout.Token);
        await context.Host.StopAsync(timeout.Token);

        // Assert
        Assert.AreEqual(RemoteSessionStatus.Stopped, context.Host.State.Status);
        Assert.AreEqual(0, context.Host.State.AuthenticatedConnectionCount);
    }

    /// <summary>Verifies repeated asynchronous disposal is safe for DI container ownership.</summary>
    /// <returns>A task that completes after repeated disposal.</returns>
    [TestMethod]
    public async Task DisposeAsync_WhenRepeated_RemainsStopped()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();

        // Act
        await context.Host.DisposeAsync();
        await context.Host.DisposeAsync();

        // Assert
        Assert.AreEqual(RemoteSessionStatus.Stopped, context.Host.State.Status);
        Assert.AreEqual(0, context.Host.State.AuthenticatedConnectionCount);
    }

    /// <summary>Verifies useful pairing logs never capture remote credentials or speaker notes.</summary>
    /// <returns>A task that completes after valid and invalid pairing attempts.</returns>
    [TestMethod]
    public async Task Pairing_ValidAndInvalidTokens_LogsOutcomeWithoutCredentials()
    {
        // Arrange
        const string confidentialNotes = "CONFIDENTIAL-NOTES-6D75F9C6";
        const string invalidToken = "INVALID-PAIRING-TOKEN-8D31C3C0";
        var sink = new CollectingLogSink();
        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using var loggerFactory = new SerilogLoggerFactory(logger, dispose: false);
        await using RemoteTestContext context = await RemoteTestContext.StartAsync(
            loggerFactory,
            confidentialNotes);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        string pairingToken = QueryHelpers.ParseQuery(
            context.Descriptor.PairingUri.Query)["t"].ToString();
        Uri invalidPairingUri = new Uri(
            QueryHelpers.AddQueryString(
                new Uri(context.BaseUri, "/pair").AbsoluteUri,
                "t",
                invalidToken));

        // Act
        HttpResponseMessage accepted = await client.GetAsync(context.Descriptor.PairingUri);
        HttpResponseMessage rejected = await client.GetAsync(invalidPairingUri);
        string cookieHeader = string.Join(";", accepted.Headers.GetValues("Set-Cookie"));
        string browserCredential = cookieHeader.Split(';', 2)[0].Split('=', 2)[1];
        string captured = sink.RenderAll();

        // Assert
        Assert.AreEqual(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Contains("pairing credential was accepted", captured);
        Assert.Contains("pairing credential was rejected", captured);
        Assert.DoesNotContain(pairingToken, captured);
        Assert.DoesNotContain(invalidToken, captured);
        Assert.DoesNotContain(browserCredential, captured);
        Assert.DoesNotContain(confidentialNotes, captured);
        Assert.DoesNotContain(context.Descriptor.PairingUri.AbsoluteUri, captured);
    }

    /// <summary>Verifies debounced address changes replace the QR and preserve live browser credentials.</summary>
    /// <returns>A task that completes after endpoint rebinding.</returns>
    [TestMethod]
    public async Task NetworkAddressChanged_ActiveSession_RebindsQrAndPreservesCredentialStore()
    {
        // Arrange
        var notifier = new FakeNetworkChangeNotifier();
        await using RemoteTestContext context = await RemoteTestContext.StartAsync(
            networkChangeNotifier: notifier,
            networkChangeDebounceInterval: TimeSpan.FromMilliseconds(20));
        var cookies = new CookieContainer();
        using var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            UseCookies = true,
        });
        HttpResponseMessage paired = await client.GetAsync(context.Descriptor.PairingUri);
        Assert.AreEqual(HttpStatusCode.Redirect, paired.StatusCode);
        var replacementSource = new TaskCompletionSource<DesktopPairingDescriptor>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool pairingWasWithdrawn = false;
        context.Host.PairingChanged += descriptor =>
        {
            if (descriptor is null)
            {
                pairingWasWithdrawn = true;
            }
            else if (descriptor.PairingUri != context.Descriptor.PairingUri)
            {
                replacementSource.TrySetResult(descriptor);
            }
        };

        // Act
        notifier.Raise();
        DesktopPairingDescriptor replacement = await replacementSource.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        HttpResponseMessage presenter = await client.GetAsync(
            new Uri(context.Host.State.SelectedUrl!, "/presenter"));

        // Assert
        Assert.IsTrue(pairingWasWithdrawn);
        Assert.AreNotEqual(context.Descriptor.PairingUri, replacement.PairingUri);
        Assert.AreEqual(
            QueryHelpers.ParseQuery(context.Descriptor.PairingUri.Query)["t"].ToString(),
            QueryHelpers.ParseQuery(replacement.PairingUri.Query)["t"].ToString());
        Assert.AreEqual(RemoteSessionStatus.Ready, context.Host.State.Status);
        Assert.AreEqual(HttpStatusCode.OK, presenter.StatusCode);
    }

    /// <summary>Verifies reconnect returns the latest authoritative timer state and revision.</summary>
    /// <returns>A task that completes after timer resynchronization.</returns>
    [TestMethod]
    public async Task SignalR_ReconnectAfterTimerChanges_ReturnsLatestTimerSnapshot()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        CookieContainer cookies = await PairAsync(context);
        await using HubConnection connection = BuildConnection(context.BaseUri, cookies);
        await connection.StartAsync();
        PresenterStateDto initial = await connection.InvokeAsync<PresenterStateDto>("GetState");
        await connection.StopAsync();

        // Act
        Assert.IsTrue(context.SessionService.ConfigureTimer("0:30").IsSuccess);
        Assert.IsTrue(context.SessionService.StartTimer().IsSuccess);
        Assert.IsTrue(context.SessionService.PauseTimer().IsSuccess);
        await connection.StartAsync();
        PresenterStateDto resynchronized = await connection.InvokeAsync<PresenterStateDto>("GetState");

        // Assert
        Assert.AreEqual(TimerRunState.Paused.ToString(), resynchronized.TimerStatus);
        Assert.IsInRange(29L, 30L, resynchronized.TimerDisplaySeconds);
        Assert.IsGreaterThan(initial.Revision, resynchronized.Revision);
    }

    /// <summary>Verifies ending a session while disconnected prevents the old phone from reconnecting.</summary>
    /// <returns>A task that completes after the rejected reconnect.</returns>
    [TestMethod]
    public async Task SignalR_SessionEndsWhileDisconnected_OldCookieCannotReconnect()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        CookieContainer cookies = await PairAsync(context);
        Uri formerBaseUri = context.BaseUri;
        await using HubConnection connection = BuildConnection(formerBaseUri, cookies);
        await connection.StartAsync();
        await connection.StopAsync();

        // Act
        await context.SessionService.EndRemoteSessionAsync();

        // Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());
        Assert.AreEqual(RemoteSessionStatus.Stopped, context.Host.State.Status);
        Assert.AreEqual(0, context.Host.State.AuthenticatedConnectionCount);
    }

    /// <summary>Verifies timer transitions and presentation loss broadcast complete authoritative state.</summary>
    /// <returns>A task that completes after both broadcasts.</returns>
    [TestMethod]
    public async Task SignalR_TimerAndPresentationLoss_BroadcastAuthoritativeSnapshots()
    {
        // Arrange
        await using RemoteTestContext context = await RemoteTestContext.StartAsync();
        CookieContainer cookies = await PairAsync(context);
        var broadcasts = Channel.CreateUnbounded<PresenterStateDto>();
        await using HubConnection connection = BuildConnection(context.BaseUri, cookies);
        connection.On<PresenterStateDto>("stateChanged", state => broadcasts.Writer.TryWrite(state));
        await connection.StartAsync();
        PresenterStateDto initial = await connection.InvokeAsync<PresenterStateDto>("GetState");

        // Act
        Assert.IsTrue(context.SessionService.ConfigureTimer("0:45").IsSuccess);
        Assert.IsTrue(context.SessionService.StartTimer().IsSuccess);
        Assert.IsTrue(context.SessionService.PauseTimer().IsSuccess);
        PresenterStateDto paused = await ReadUntilAsync(
            broadcasts.Reader,
            state => state.TimerStatus == TimerRunState.Paused.ToString(),
            TimeSpan.FromSeconds(3));
        context.Presentation.Disconnect();
        PresenterStateDto disconnected = await ReadUntilAsync(
            broadcasts.Reader,
            state => state.PresentationStatus == PresentationConnectionState.Disconnected.ToString(),
            TimeSpan.FromSeconds(3));

        // Assert
        Assert.IsInRange(44L, 45L, paused.TimerDisplaySeconds);
        Assert.IsGreaterThan(initial.Revision, paused.Revision);
        Assert.IsNull(disconnected.CurrentSlideIndex);
        Assert.IsNull(disconnected.TotalSlides);
        Assert.AreEqual(string.Empty, disconnected.SpeakerNotes);
        Assert.IsGreaterThan(paused.Revision, disconnected.Revision);
    }

    /// <summary>Verifies one unbindable adapter cannot discard a healthy endpoint.</summary>
    /// <returns>A task that completes after partial endpoint binding.</returns>
    [TestMethod]
    public async Task StartAsync_UnbindableAdapter_PreservesHealthyEndpoint()
    {
        // Arrange and act
        await using RemoteTestContext context = await RemoteTestContext.StartAsync(
            networkAddressProvider: new SingleNetworkAddressProvider(
                IPAddress.Parse("203.0.113.123"),
                "Unbindable test adapter"));

        // Assert
        Assert.AreEqual(RemoteSessionStatus.Ready, context.Host.State.Status);
        Assert.HasCount(1, context.Host.State.CandidateUrls);
        Assert.IsTrue(IPAddress.IsLoopback(IPAddress.Parse(context.Host.State.SelectedUrl!.Host)));
    }

    private static HubConnection BuildConnection(Uri baseUri, CookieContainer cookies) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(baseUri, "/presenterHub"), options =>
            {
                options.Cookies = cookies;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    private static async Task<CookieContainer> PairAsync(RemoteTestContext context)
    {
        var cookies = new CookieContainer();
        using var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            UseCookies = true,
        });
        HttpResponseMessage response = await client.GetAsync(context.Descriptor.PairingUri);
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        return cookies;
    }

    private static async Task WaitForConnectionCountAsync(RemoteTestContext context, int expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (context.SessionService.State.Remote.AuthenticatedConnectionCount != expected)
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static async Task<PresenterStateDto> ReadUntilAsync(
        ChannelReader<PresenterStateDto> reader,
        Func<PresenterStateDto, bool> predicate,
        TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        await foreach (PresenterStateDto state in reader.ReadAllAsync(cancellation.Token))
        {
            if (predicate(state))
            {
                return state;
            }
        }

        throw new AssertFailedException("The expected presenter broadcast was not received.");
    }

    private sealed class RemoteTestContext : IAsyncDisposable
    {
        private RemoteTestContext(
            RemoteHost host,
            PresentationSessionService sessionService,
            FakePresentationController presentation,
            DesktopPairingDescriptor descriptor)
        {
            this.Host = host;
            this.SessionService = sessionService;
            this.Presentation = presentation;
            this.Descriptor = descriptor;
        }

        public RemoteHost Host { get; }

        public PresentationSessionService SessionService { get; }

        public FakePresentationController Presentation { get; }

        public DesktopPairingDescriptor Descriptor { get; }

        public Uri BaseUri => this.Host.State.SelectedUrl!;

        public static async Task<RemoteTestContext> StartAsync(
            Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null,
            string notes = "first notes",
            INetworkChangeNotifier? networkChangeNotifier = null,
            TimeSpan? networkChangeDebounceInterval = null,
            INetworkAddressProvider? networkAddressProvider = null)
        {
            var presentation = new FakePresentationController(notes);
            PresentationSessionService? service = null;
            var host = new RemoteHost(
                () => service!,
                networkAddressProvider ?? new EmptyNetworkAddressProvider(),
                allowLoopbackPairing: true,
                loggerFactory,
                networkChangeNotifier,
                networkChangeDebounceInterval);
            service = new PresentationSessionService(
                new MonotonicPresentationTimer(new StopwatchMonotonicClock()),
                presentation,
                host);
            OperationResult<DesktopPairingDescriptor> started = await service.StartRemoteSessionAsync();
            Assert.IsTrue(started.IsSuccess, started.Message);
            return new RemoteTestContext(host, service, presentation, started.Value!);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Host.DisposeAsync();
            this.SessionService.Dispose();
        }
    }

    private sealed class EmptyNetworkAddressProvider : INetworkAddressProvider
    {
        public IReadOnlyList<NetworkAddressCandidate> GetCandidates() =>
            Array.Empty<NetworkAddressCandidate>();
    }

    private sealed class SingleNetworkAddressProvider : INetworkAddressProvider
    {
        private readonly NetworkAddressCandidate _candidate;

        public SingleNetworkAddressProvider(IPAddress address, string label)
        {
            this._candidate = new NetworkAddressCandidate(address, label);
        }

        public IReadOnlyList<NetworkAddressCandidate> GetCandidates() => new[] { this._candidate };
    }

    private sealed class FakePresentationController : IPresentationController
    {
        private PresentationSnapshot _state;

        public FakePresentationController(string notes)
        {
            this._state = new PresentationSnapshot(
                PresentationConnectionState.Running,
                1,
                3,
                notes,
                null);
        }

        public event Action<PresentationSnapshot>? StateChanged;

        public PresentationSnapshot State => this._state;

        public int NextCalls { get; private set; }

        public int PreviousCalls { get; private set; }

        public Task StartMonitoringAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopMonitoringAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<OperationResult> NextAsync(CancellationToken cancellationToken = default)
        {
            this.NextCalls++;
            this._state = this._state with { CurrentSlideIndex = 2, SpeakerNotes = "second notes" };
            this.StateChanged?.Invoke(this._state);
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> PreviousAsync(CancellationToken cancellationToken = default)
        {
            this.PreviousCalls++;
            this.SetSlide(2, "second notes");
            return Task.FromResult(OperationResult.Success());
        }

        public void SetSlide(int index, string notes)
        {
            this._state = this._state with { CurrentSlideIndex = index, SpeakerNotes = notes };
            this.StateChanged?.Invoke(this._state);
        }

        public void Disconnect()
        {
            this._state = new PresentationSnapshot(
                PresentationConnectionState.Disconnected,
                3,
                3,
                "stale notes must clear",
                ErrorCodes.PresentationDisconnected);
            this.StateChanged?.Invoke(this._state);
        }
    }

    private sealed class FakeNetworkChangeNotifier : INetworkChangeNotifier
    {
        private bool _started;

        public event Action? Changed;

        public void Start() => this._started = true;

        public void Stop() => this._started = false;

        public void Raise()
        {
            if (this._started)
            {
                this.Changed?.Invoke();
            }
        }
    }

    private sealed class CollectingLogSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new ConcurrentQueue<LogEvent>();

        public void Emit(LogEvent logEvent) => this._events.Enqueue(logEvent);

        public string RenderAll() => string.Join(
            Environment.NewLine,
            this._events.Select(static logEvent => string.Concat(
                logEvent.RenderMessage(CultureInfo.InvariantCulture),
                " ",
                string.Join(
                    " ",
                    logEvent.Properties.Select(static property =>
                        string.Concat(property.Key, "=", property.Value.ToString()))),
                " ",
                logEvent.Exception?.ToString())));
    }
}
