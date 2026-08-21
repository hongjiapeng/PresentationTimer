using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;
using PresentationTimer.Core.Timing;

namespace PresentationTimer.Core.Services;

/// <summary>
/// Coordinates application commands and merges infrastructure events into one aggregate state.
/// </summary>
public sealed class PresentationSessionService : IPresentationSessionService, IDisposable
{
    private readonly IPresentationController _presentationController;
    private readonly IRemoteSessionHost _remoteSessionHost;
    private readonly SessionStateStore _store;
    private readonly IPresentationTimer _timer;
    private int _eventsDetached;
    private int _shutdownRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresentationSessionService"/> class.
    /// </summary>
    /// <param name="timer">The authoritative timer.</param>
    /// <param name="presentationController">The presentation infrastructure adapter.</param>
    /// <param name="remoteSessionHost">The remote-session infrastructure adapter.</param>
    /// <param name="timeProvider">The diagnostic UTC time provider.</param>
    public PresentationSessionService(
        IPresentationTimer timer,
        IPresentationController presentationController,
        IRemoteSessionHost remoteSessionHost,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(presentationController);
        ArgumentNullException.ThrowIfNull(remoteSessionHost);

        this._timer = timer;
        this._presentationController = presentationController;
        this._remoteSessionHost = remoteSessionHost;
        this._store = new SessionStateStore(
            presentationController.State.WithoutStaleContent(),
            timer.State,
            remoteSessionHost.State,
            timeProvider);

        this._timer.StateChanged += this.OnTimerStateChanged;
        this._presentationController.StateChanged += this.OnPresentationStateChanged;
        this._remoteSessionHost.StateChanged += this.OnRemoteStateChanged;
        this._remoteSessionHost.PairingChanged += this.OnPairingChanged;
        this._store.StateChanged += this.OnStoreStateChanged;
    }

    /// <inheritdoc/>
    public event Action<PresentationSessionState>? StateChanged;

    /// <inheritdoc/>
    public event Action<DesktopPairingDescriptor?>? PairingChanged;

    /// <inheritdoc/>
    public PresentationSessionState State => this._store.State;

    private bool IsShutdownRequested => Volatile.Read(ref this._shutdownRequested) != 0;

    /// <inheritdoc/>
    public void BeginShutdown() => Interlocked.Exchange(ref this._shutdownRequested, 1);

    /// <inheritdoc/>
    public void DetachEvents()
    {
        if (Interlocked.Exchange(ref this._eventsDetached, 1) != 0)
        {
            return;
        }

        this._timer.StateChanged -= this.OnTimerStateChanged;
        this._presentationController.StateChanged -= this.OnPresentationStateChanged;
        this._remoteSessionHost.StateChanged -= this.OnRemoteStateChanged;
        this._remoteSessionHost.PairingChanged -= this.OnPairingChanged;
        this._store.StateChanged -= this.OnStoreStateChanged;
    }

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> ConfigureTimer(string? durationText)
    {
        if (this.IsShutdownRequested)
        {
            return ClosingFailure<TimerSnapshot>();
        }

        OperationResult<TimeSpan> parsed = DurationParser.Parse(durationText);
        return parsed.IsSuccess && parsed.Value is TimeSpan target
            ? this._timer.Configure(target)
            : OperationResult.Failure<TimerSnapshot>(
                parsed.ErrorCode ?? ErrorCodes.InvalidDuration,
                parsed.Message ?? "Enter a valid duration.");
    }

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> StartTimer() =>
        this.IsShutdownRequested ? ClosingFailure<TimerSnapshot>() : this._timer.Start();

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> PauseTimer() =>
        this.IsShutdownRequested ? ClosingFailure<TimerSnapshot>() : this._timer.Pause();

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> ResumeTimer() =>
        this.IsShutdownRequested ? ClosingFailure<TimerSnapshot>() : this._timer.ResumeTimer();

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> ResetTimer() =>
        this.IsShutdownRequested ? ClosingFailure<TimerSnapshot>() : this._timer.Reset();

    /// <inheritdoc/>
    public TimerSnapshot RefreshTimer()
    {
        TimerSnapshot snapshot = ToWholeSecondSnapshot(this._timer.Snapshot());
        this._store.UpdateTimer(snapshot);
        return snapshot;
    }

    /// <inheritdoc/>
    public async Task StartPresentationMonitoringAsync(CancellationToken cancellationToken = default)
    {
        await this._presentationController.StartMonitoringAsync(cancellationToken).ConfigureAwait(false);
        this._store.UpdatePresentation(this._presentationController.State.WithoutStaleContent());
    }

    /// <inheritdoc/>
    public Task<OperationResult> NextSlideAsync(CancellationToken cancellationToken = default) =>
        this.IsShutdownRequested
            ? Task.FromResult(ClosingFailure())
            : this._presentationController.NextAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<OperationResult> PreviousSlideAsync(CancellationToken cancellationToken = default) =>
        this.IsShutdownRequested
            ? Task.FromResult(ClosingFailure())
            : this._presentationController.PreviousAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<OperationResult<DesktopPairingDescriptor>> StartRemoteSessionAsync(
        CancellationToken cancellationToken = default)
    {
        if (this.IsShutdownRequested)
        {
            return ClosingFailure<DesktopPairingDescriptor>();
        }

        OperationResult<DesktopPairingDescriptor> result =
            await this._remoteSessionHost.StartAsync(cancellationToken).ConfigureAwait(false);
        this._store.UpdateRemote(this._remoteSessionHost.State);
        return result;
    }

    /// <inheritdoc/>
    public async Task EndRemoteSessionAsync(CancellationToken cancellationToken = default)
    {
        await this._remoteSessionHost.StopAsync(cancellationToken).ConfigureAwait(false);
        this._store.UpdateRemote(this._remoteSessionHost.State);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.BeginShutdown();
        this.DetachEvents();
    }

    private static OperationResult ClosingFailure() =>
        OperationResult.Failure(ErrorCodes.ApplicationClosing, "The application is closing.");

    private static OperationResult<T> ClosingFailure<T>() =>
        OperationResult.Failure<T>(ErrorCodes.ApplicationClosing, "The application is closing.");

    private static TimerSnapshot ToWholeSecondSnapshot(TimerSnapshot timer)
    {
        double seconds = timer.Remaining.TotalSeconds;
        long displayedSeconds = seconds > 0
            ? checked((long)Math.Ceiling(seconds))
            : checked((long)Math.Floor(seconds));
        return timer with { Remaining = TimeSpan.FromSeconds(displayedSeconds) };
    }

    private void OnPresentationStateChanged(PresentationSnapshot presentation) =>
        this._store.UpdatePresentation(presentation.WithoutStaleContent());

    private void OnRemoteStateChanged(RemoteSessionPublicState remote) =>
        this._store.UpdateRemote(remote);

    private void OnPairingChanged(DesktopPairingDescriptor? descriptor) =>
        this.PairingChanged?.Invoke(descriptor);

    private void OnStoreStateChanged(PresentationSessionState state) =>
        this.StateChanged?.Invoke(state);

    private void OnTimerStateChanged(TimerSnapshot timer) =>
        this._store.UpdateTimer(ToWholeSecondSnapshot(timer));
}
