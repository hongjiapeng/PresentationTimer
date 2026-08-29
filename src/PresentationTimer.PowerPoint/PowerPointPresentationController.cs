using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;
using PresentationTimer.PowerPoint.Interop;
using PresentationTimer.PowerPoint.Threading;
using Ppt = Microsoft.Office.Interop.PowerPoint;

namespace PresentationTimer.PowerPoint;

/// <summary>
/// Monitors and controls an already-running PowerPoint slide show on a private pumped STA.
/// </summary>
public sealed partial class PowerPointPresentationController : IPresentationController, IAsyncDisposable
{
    private const int CallRejected = unchecked((int)0x80010001);
    private const int ObjectNotConnected = unchecked((int)0x800401FD);
    private const int RetryLater = unchecked((int)0x8001010A);
    private const int RpcDisconnected = unchecked((int)0x80010108);
    private const int ServerUnavailable = unchecked((int)0x800706BA);
    private const int NavigationAttemptLimit = 3;
    private readonly IActiveObjectResolver _activeObjectResolver;
    private readonly IPowerPointApplicationActivator _applicationActivator;
    private readonly StaComDispatcher _dispatcher;
    private readonly object _lifecycleGate = new object();
    private readonly ILogger<PowerPointPresentationController> _logger;
    private readonly TimeSpan _reconciliationInterval;
    private CancellationTokenSource? _monitoringCancellation;
    private Task? _reconciliationTask;
    private Timer? _eventRefreshTimer;
    private Ppt.Application? _application;
    private PresentationSnapshot _state = PresentationSnapshot.Initial;
    private bool _started;
    private bool _stopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerPointPresentationController"/> class.
    /// </summary>
    public PowerPointPresentationController()
        : this(
            new ActiveObjectResolver(),
            new StaComDispatcher(),
            TimeSpan.FromSeconds(2),
            NullLogger<PowerPointPresentationController>.Instance,
            new PowerPointApplicationActivator())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerPointPresentationController"/> class
    /// connected to the process logging pipeline.
    /// </summary>
    /// <param name="logger">The process logger.</param>
    public PowerPointPresentationController(ILogger<PowerPointPresentationController> logger)
        : this(
            new ActiveObjectResolver(),
            new StaComDispatcher(),
            TimeSpan.FromSeconds(2),
            logger,
            new PowerPointApplicationActivator())
    {
    }

    internal PowerPointPresentationController(
        IActiveObjectResolver activeObjectResolver,
        StaComDispatcher dispatcher,
        TimeSpan reconciliationInterval,
        ILogger<PowerPointPresentationController>? logger = null,
        IPowerPointApplicationActivator? applicationActivator = null)
    {
        ArgumentNullException.ThrowIfNull(activeObjectResolver);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (reconciliationInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reconciliationInterval),
                reconciliationInterval,
                "The reconciliation interval must be positive.");
        }

        this._activeObjectResolver = activeObjectResolver;
        this._applicationActivator = applicationActivator ?? new PowerPointApplicationActivator();
        this._dispatcher = dispatcher;
        this._reconciliationInterval = reconciliationInterval;
        this._logger = logger ?? NullLogger<PowerPointPresentationController>.Instance;
    }

    /// <inheritdoc/>
    public event Action<PresentationSnapshot>? StateChanged;

    /// <inheritdoc/>
    public PresentationSnapshot State => Volatile.Read(ref this._state);

    /// <inheritdoc/>
    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        lock (this._lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(this._stopped, this);
            if (this._started)
            {
                return;
            }

            this._started = true;
            this._monitoringCancellation = new CancellationTokenSource();
        }

        LogMonitoringStarting(this._logger);
        try
        {
            await this._dispatcher.InvokeAsync(this.Reconcile, cancellationToken).ConfigureAwait(false);
            this._reconciliationTask = this.RunReconciliationAsync(this._monitoringCancellation.Token);
        }
        catch
        {
            lock (this._lifecycleGate)
            {
                this._started = false;
                this._monitoringCancellation.Dispose();
                this._monitoringCancellation = null;
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? monitoringCancellation;
        Task? reconciliationTask;
        lock (this._lifecycleGate)
        {
            if (this._stopped)
            {
                return;
            }

            this._stopped = true;
            monitoringCancellation = this._monitoringCancellation;
            reconciliationTask = this._reconciliationTask;
        }

        monitoringCancellation?.Cancel();
        if (reconciliationTask is not null)
        {
            try
            {
                await reconciliationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (monitoringCancellation?.IsCancellationRequested == true)
            {
            }
        }

        await this._dispatcher.InvokeAsync(this.DetachApplication, cancellationToken).ConfigureAwait(false);
        await this._dispatcher.StopAsync(cancellationToken).ConfigureAwait(false);
        monitoringCancellation?.Dispose();
        LogMonitoringStopped(this._logger);
    }

    /// <inheritdoc/>
    public Task<OperationResult> NextAsync(CancellationToken cancellationToken = default) =>
        this.NavigateAsync(forward: true, cancellationToken);

    /// <inheritdoc/>
    public Task<OperationResult> PreviousAsync(CancellationToken cancellationToken = default) =>
        this.NavigateAsync(forward: false, cancellationToken);

    /// <inheritdoc/>
    public async Task<OperationResult> OpenPresentationAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        OperationResult<string> validation = PresentationFilePath.Validate(filePath);
        if (!validation.IsSuccess || validation.Value is not string normalizedPath)
        {
            return OperationResult.Failure(
                validation.ErrorCode ?? ErrorCodes.PresentationInvalidFile,
                validation.Message ?? "Select an existing PowerPoint presentation file.");
        }

        if (this._stopped)
        {
            return OperationResult.Failure(
                ErrorCodes.PresentationUnavailable,
                "Presentation control is stopping.");
        }

        try
        {
            return await this._dispatcher.InvokeAsync(
                () => this.OpenPresentation(normalizedPath),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return OperationResult.Failure(
                ErrorCodes.PresentationUnavailable,
                "Presentation control is unavailable.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await this.StopMonitoringAsync(timeout.Token).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    internal static async Task<OperationResult> ExecuteWithBusyRetryAsync(
        Func<CancellationToken, Task<OperationResult>> command,
        int attemptLimit,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (attemptLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptLimit),
                attemptLimit,
                "The attempt limit must be positive.");
        }

        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryDelay),
                retryDelay,
                "The retry delay cannot be negative.");
        }

        OperationResult result = OperationResult.Failure(
            ErrorCodes.PresentationBusy,
            "PowerPoint is busy. Try the command again.");
        for (int attempt = 1; attempt <= attemptLimit; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await command(cancellationToken).ConfigureAwait(false);
            if (result.ErrorCode != ErrorCodes.PresentationBusy || attempt == attemptLimit)
            {
                return result;
            }

            await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private static bool IsBusy(COMException exception) =>
        exception.HResult is CallRejected or RetryLater;

    private static bool IsDisconnected(COMException exception) =>
        exception.HResult is ObjectNotConnected or RpcDisconnected or ServerUnavailable;

    private static Ppt.Presentation? FindOpenPresentation(
        Ppt.Presentations presentations,
        string normalizedPath,
        ComObjectScope scope)
    {
        for (int index = 1; index <= presentations.Count; index++)
        {
            Ppt.Presentation presentation = scope.Track(presentations[index]);
            if (PathsEqual(presentation.FullName, normalizedPath))
            {
                return presentation;
            }
        }

        return null;
    }

    private static bool PathsEqual(string candidatePath, string normalizedPath)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(candidatePath),
                normalizedPath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    [LoggerMessage(1000, LogLevel.Information, "Starting PowerPoint monitoring")]
    private static partial void LogMonitoringStarting(ILogger logger);

    [LoggerMessage(1001, LogLevel.Information, "PowerPoint monitoring stopped")]
    private static partial void LogMonitoringStopped(ILogger logger);

    [LoggerMessage(1002, LogLevel.Information, "PowerPoint is unavailable on this system")]
    private static partial void LogUnavailable(ILogger logger);

    [LoggerMessage(1003, LogLevel.Debug, "PowerPoint is not running")]
    private static partial void LogNotRunning(ILogger logger);

    [LoggerMessage(1004, LogLevel.Information, "Attached to the running PowerPoint application")]
    private static partial void LogAttached(ILogger logger);

    [LoggerMessage(1005, LogLevel.Warning, "PowerPoint attachment returned an unexpected state")]
    private static partial void LogUnexpectedAttachment(ILogger logger);

    [LoggerMessage(1006, LogLevel.Warning, "PowerPoint rejected a navigation command because it is busy")]
    private static partial void LogNavigationBusy(ILogger logger);

    [LoggerMessage(1007, LogLevel.Warning, "PowerPoint disconnected during navigation")]
    private static partial void LogNavigationDisconnected(ILogger logger, Exception exception);

    [LoggerMessage(
        1008,
        LogLevel.Debug,
        "PowerPoint state changed to {Connection} at slide {SlideIndex} of {SlideCount}")]
    private static partial void LogStateChanged(
        ILogger logger,
        PresentationConnectionState connection,
        int? slideIndex,
        int? slideCount);

    [LoggerMessage(1009, LogLevel.Debug, "PowerPoint is busy during state reconciliation")]
    private static partial void LogReconciliationBusy(ILogger logger);

    [LoggerMessage(1010, LogLevel.Warning, "PowerPoint disconnected during state reconciliation")]
    private static partial void LogReconciliationDisconnected(ILogger logger, Exception exception);

    [LoggerMessage(1011, LogLevel.Warning, "PowerPoint state reconciliation failed")]
    private static partial void LogReconciliationFailed(ILogger logger, Exception exception);

    [LoggerMessage(1012, LogLevel.Information, "Clearing stale PowerPoint state and waiting to reattach")]
    private static partial void LogClearingStaleState(ILogger logger);

    [LoggerMessage(1013, LogLevel.Information, "Opening a user-selected presentation in PowerPoint")]
    private static partial void LogPresentationOpening(ILogger logger);

    [LoggerMessage(1014, LogLevel.Warning, "PowerPoint could not open the user-selected presentation")]
    private static partial void LogPresentationOpenFailed(ILogger logger, Exception exception);

    [LoggerMessage(1015, LogLevel.Information, "PowerPoint opened the selected presentation and started its slide show")]
    private static partial void LogPresentationOpened(ILogger logger);

    private void AttachApplication()
    {
        ActiveObjectResult result = this._activeObjectResolver.Resolve("PowerPoint.Application");
        switch (result.Status)
        {
            case ActiveObjectStatus.Unavailable:
                LogUnavailable(this._logger);
                this.Publish(new PresentationSnapshot(
                    PresentationConnectionState.Unavailable,
                    null,
                    null,
                    string.Empty,
                    ErrorCodes.PresentationUnavailable));
                return;
            case ActiveObjectStatus.NotRunning:
                LogNotRunning(this._logger);
                this.Publish(PresentationSnapshot.Initial);
                return;
            case ActiveObjectStatus.Attached when result.Instance is not null:
                this._application = (Ppt.Application)result.Instance;
                this.SubscribeToApplicationEvents();
                LogAttached(this._logger);
                return;
            default:
                LogUnexpectedAttachment(this._logger);
                this.Publish(new PresentationSnapshot(
                    PresentationConnectionState.Disconnected,
                    null,
                    null,
                    string.Empty,
                    ErrorCodes.PresentationDisconnected));
                return;
        }
    }

    private void DetachApplication()
    {
        this._eventRefreshTimer?.Dispose();
        this._eventRefreshTimer = null;
        if (this._application is null)
        {
            return;
        }

        try
        {
            this.UnsubscribeFromApplicationEvents();
        }
        catch (COMException)
        {
        }
        finally
        {
            if (Marshal.IsComObject(this._application))
            {
                _ = Marshal.FinalReleaseComObject(this._application);
            }

            this._application = null;
        }
    }

    private OperationResult OpenPresentation(string normalizedPath)
    {
        LogPresentationOpening(this._logger);
        OperationResult applicationResult = this.EnsureApplicationForOpen();
        if (!applicationResult.IsSuccess || this._application is null)
        {
            return applicationResult;
        }

        try
        {
            using var scope = new ComObjectScope();
            this._application.Visible = Microsoft.Office.Core.MsoTriState.msoTrue;
            if (this.HasRunningSlideShow(normalizedPath, scope))
            {
                this.Publish(PresentationSnapshotReader.Read(this._application));
                LogPresentationOpened(this._logger);
                return OperationResult.Success();
            }

            Ppt.Presentations presentations = scope.Track(this._application.Presentations);
            Ppt.Presentation? presentation = FindOpenPresentation(
                presentations,
                normalizedPath,
                scope);
            presentation ??= scope.Track(presentations.Open(
                normalizedPath,
                Microsoft.Office.Core.MsoTriState.msoTrue,
                Microsoft.Office.Core.MsoTriState.msoFalse,
                Microsoft.Office.Core.MsoTriState.msoTrue));
            Ppt.SlideShowSettings settings = scope.Track(presentation.SlideShowSettings);
            _ = scope.Track(settings.Run());
            this.Publish(PresentationSnapshotReader.Read(this._application));
            LogPresentationOpened(this._logger);
            return OperationResult.Success();
        }
        catch (COMException exception) when (IsBusy(exception))
        {
            LogNavigationBusy(this._logger);
            this.Publish(this.State with { LastErrorCode = ErrorCodes.PresentationBusy });
            return OperationResult.Failure(
                ErrorCodes.PresentationBusy,
                "PowerPoint is busy. Try opening the presentation again.");
        }
        catch (COMException exception) when (IsDisconnected(exception))
        {
            LogNavigationDisconnected(this._logger, exception);
            this.HandleDisconnection();
            return OperationResult.Failure(
                ErrorCodes.PresentationDisconnected,
                "PowerPoint disconnected. Waiting to reconnect.");
        }
        catch (Exception exception)
        {
            LogPresentationOpenFailed(this._logger, exception);
            this.PublishOpenFailure();
            return OperationResult.Failure(
                ErrorCodes.PresentationOpenFailed,
                "PowerPoint could not open or start the selected presentation.");
        }
    }

    private OperationResult EnsureApplicationForOpen()
    {
        if (this._application is not null)
        {
            return OperationResult.Success();
        }

        ApplicationActivationResult activation =
            this._applicationActivator.Activate("PowerPoint.Application");
        if (activation.Status == ApplicationActivationStatus.Unavailable)
        {
            this.Publish(new PresentationSnapshot(
                PresentationConnectionState.Unavailable,
                null,
                null,
                string.Empty,
                ErrorCodes.PresentationUnavailable));
            return OperationResult.Failure(
                ErrorCodes.PresentationUnavailable,
                "Desktop PowerPoint is not installed or registered.");
        }

        if (activation.Status != ApplicationActivationStatus.Activated ||
            activation.Instance is not Ppt.Application application)
        {
            if (activation.Instance is not null && Marshal.IsComObject(activation.Instance))
            {
                _ = Marshal.FinalReleaseComObject(activation.Instance);
            }

            this.Publish(new PresentationSnapshot(
                PresentationConnectionState.Disconnected,
                null,
                null,
                string.Empty,
                ErrorCodes.PresentationOpenFailed));
            return OperationResult.Failure(
                ErrorCodes.PresentationOpenFailed,
                "Desktop PowerPoint could not be started.");
        }

        this._application = application;
        try
        {
            this.SubscribeToApplicationEvents();
        }
        catch
        {
            this.DetachApplication();
            throw;
        }

        LogAttached(this._logger);
        return OperationResult.Success();
    }

    private bool HasRunningSlideShow(string normalizedPath, ComObjectScope scope)
    {
        if (this._application is null)
        {
            return false;
        }

        Ppt.SlideShowWindows windows = scope.Track(this._application.SlideShowWindows);
        for (int index = 1; index <= windows.Count; index++)
        {
            Ppt.SlideShowWindow window = scope.Track(windows[index]);
            Ppt.Presentation presentation = scope.Track(window.Presentation);
            if (PathsEqual(presentation.FullName, normalizedPath))
            {
                return true;
            }
        }

        return false;
    }

    private void PublishOpenFailure()
    {
        if (this._application is null)
        {
            this.Publish(new PresentationSnapshot(
                PresentationConnectionState.Disconnected,
                null,
                null,
                string.Empty,
                ErrorCodes.PresentationOpenFailed));
            return;
        }

        try
        {
            this.Publish(PresentationSnapshotReader.Read(this._application) with
            {
                LastErrorCode = ErrorCodes.PresentationOpenFailed,
            });
        }
        catch (COMException exception) when (IsDisconnected(exception))
        {
            this.HandleDisconnection();
        }
        catch (COMException)
        {
            this.Publish(new PresentationSnapshot(
                PresentationConnectionState.Disconnected,
                null,
                null,
                string.Empty,
                ErrorCodes.PresentationOpenFailed));
        }
    }

    private async Task<OperationResult> NavigateAsync(bool forward, CancellationToken cancellationToken)
    {
        if (this._stopped)
        {
            return OperationResult.Failure(
                ErrorCodes.PresentationUnavailable,
                "Presentation control is stopping.");
        }

        try
        {
            return await ExecuteWithBusyRetryAsync(
                token => this._dispatcher.InvokeAsync(() => this.Navigate(forward), token),
                NavigationAttemptLimit,
                TimeSpan.FromMilliseconds(75),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return OperationResult.Failure(
                ErrorCodes.PresentationUnavailable,
                "Presentation control is unavailable.");
        }
    }

    private OperationResult Navigate(bool forward)
    {
        if (this._application is null || this.State.Connection != PresentationConnectionState.Running)
        {
            return OperationResult.Failure(
                ErrorCodes.PresentationUnavailable,
                "Start a PowerPoint slide show before navigating.");
        }

        try
        {
            using var scope = new ComObjectScope();
            Ppt.SlideShowWindows windows = scope.Track(this._application.SlideShowWindows);
            if (windows.Count == 0)
            {
                this.Reconcile();
                return OperationResult.Failure(
                    ErrorCodes.PresentationUnavailable,
                    "Start a PowerPoint slide show before navigating.");
            }

            Ppt.SlideShowWindow window = scope.Track(windows[1]);
            Ppt.SlideShowView view = scope.Track(window.View);
            if (forward)
            {
                view.Next();
            }
            else
            {
                view.Previous();
            }

            this.ScheduleDeferredRefresh();
            return OperationResult.Success();
        }
        catch (COMException exception) when (IsBusy(exception))
        {
            LogNavigationBusy(this._logger);
            return OperationResult.Failure(
                ErrorCodes.PresentationBusy,
                "PowerPoint is busy. Try the command again.");
        }
        catch (COMException exception) when (IsDisconnected(exception))
        {
            LogNavigationDisconnected(this._logger, exception);
            this.HandleDisconnection();
            return OperationResult.Failure(
                ErrorCodes.PresentationDisconnected,
                "PowerPoint disconnected. Waiting to reconnect.");
        }
    }

    private void OnPresentationInvalidated(Ppt.Presentation presentation) => this.ScheduleDeferredRefresh();

    private void OnSlideShowInvalidated(Ppt.SlideShowWindow window) => this.ScheduleDeferredRefresh();

    private void OnSlideShowEnded(Ppt.Presentation presentation) => this.ScheduleDeferredRefresh();

    private void OnWindowInvalidated(Ppt.Presentation presentation, Ppt.DocumentWindow window) =>
        this.ScheduleDeferredRefresh();

    private void Publish(PresentationSnapshot snapshot)
    {
        PresentationSnapshot safeSnapshot = snapshot.WithoutStaleContent();
        PresentationSnapshot previous = Interlocked.Exchange(ref this._state, safeSnapshot);
        if (previous != safeSnapshot)
        {
            LogStateChanged(
                this._logger,
                safeSnapshot.Connection,
                safeSnapshot.CurrentSlideIndex,
                safeSnapshot.TotalSlides);
            this.StateChanged?.Invoke(safeSnapshot);
        }
    }

    private void Reconcile()
    {
        if (this._application is null)
        {
            this.AttachApplication();
        }

        if (this._application is null)
        {
            return;
        }

        try
        {
            this.Publish(PresentationSnapshotReader.Read(this._application));
        }
        catch (COMException exception) when (IsBusy(exception))
        {
            LogReconciliationBusy(this._logger);
            this.Publish(this.State with { LastErrorCode = ErrorCodes.PresentationBusy });
        }
        catch (COMException exception) when (IsDisconnected(exception))
        {
            LogReconciliationDisconnected(this._logger, exception);
            this.HandleDisconnection();
        }
        catch (COMException exception)
        {
            LogReconciliationFailed(this._logger, exception);
            this.HandleDisconnection();
        }
    }

    private async Task RefreshAfterEventAsync()
    {
        try
        {
            await this._dispatcher.InvokeAsync(this.Reconcile).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunReconciliationAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(this._reconciliationInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await this._dispatcher.InvokeAsync(this.Reconcile, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ScheduleDeferredRefresh()
    {
        this._eventRefreshTimer ??= new Timer(
            static controller => _ = ((PowerPointPresentationController)controller!).RefreshAfterEventAsync(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _ = this._eventRefreshTimer.Change(TimeSpan.FromMilliseconds(150), Timeout.InfiniteTimeSpan);
    }

    private void SubscribeToApplicationEvents()
    {
        if (this._application is null)
        {
            return;
        }

        this._application.SlideShowBegin += this.OnSlideShowInvalidated;
        this._application.SlideShowNextSlide += this.OnSlideShowInvalidated;
        this._application.SlideShowEnd += this.OnSlideShowEnded;
        this._application.AfterPresentationOpen += this.OnPresentationInvalidated;
        this._application.PresentationClose += this.OnPresentationInvalidated;
        this._application.WindowActivate += this.OnWindowInvalidated;
        this._application.WindowDeactivate += this.OnWindowInvalidated;
    }

    private void UnsubscribeFromApplicationEvents()
    {
        if (this._application is null)
        {
            return;
        }

        this._application.SlideShowBegin -= this.OnSlideShowInvalidated;
        this._application.SlideShowNextSlide -= this.OnSlideShowInvalidated;
        this._application.SlideShowEnd -= this.OnSlideShowEnded;
        this._application.AfterPresentationOpen -= this.OnPresentationInvalidated;
        this._application.PresentationClose -= this.OnPresentationInvalidated;
        this._application.WindowActivate -= this.OnWindowInvalidated;
        this._application.WindowDeactivate -= this.OnWindowInvalidated;
    }

    private void HandleDisconnection()
    {
        LogClearingStaleState(this._logger);
        this.DetachApplication();
        this.Publish(new PresentationSnapshot(
            PresentationConnectionState.Disconnected,
            null,
            null,
            string.Empty,
            ErrorCodes.PresentationDisconnected));
    }
}
