using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Timing;

/// <summary>
/// Calculates countdown and overtime from accumulated monotonic run segments.
/// </summary>
public sealed class MonotonicPresentationTimer : IPresentationTimer
{
    private static readonly TimeSpan DefaultTarget = TimeSpan.FromMinutes(15);
    private readonly IMonotonicClock _clock;
    private readonly object _sync = new ();
    private TimeSpan _accumulatedElapsed;
    private long? _runStartedAt;
    private TimerRunState _runState = TimerRunState.Ready;
    private TimeSpan _target = DefaultTarget;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonotonicPresentationTimer"/> class.
    /// </summary>
    /// <param name="clock">The monotonic clock used for every elapsed-time calculation.</param>
    public MonotonicPresentationTimer(IMonotonicClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        this._clock = clock;
    }

    /// <inheritdoc/>
    public event Action<TimerSnapshot>? StateChanged;

    /// <inheritdoc/>
    public TimerSnapshot State => this.Snapshot();

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> Configure(TimeSpan target)
    {
        if (!DurationParser.IsSupported(target))
        {
            return InvalidDuration();
        }

        TimerSnapshot snapshot;
        lock (this._sync)
        {
            if (this._runState != TimerRunState.Ready)
            {
                return InvalidState("Reset the timer before changing its duration.");
            }

            this._target = target;
            this._accumulatedElapsed = TimeSpan.Zero;
            this._runStartedAt = null;
            snapshot = this.CreateSnapshot(this._clock.GetTimestamp());
        }

        this.StateChanged?.Invoke(snapshot);
        return OperationResult.Success(snapshot);
    }

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> Start()
    {
        TimerSnapshot snapshot;
        lock (this._sync)
        {
            if (this._runState != TimerRunState.Ready)
            {
                return InvalidState("Only a ready timer can be started.");
            }

            this._runStartedAt = this._clock.GetTimestamp();
            this._runState = TimerRunState.Running;
            snapshot = this.CreateSnapshot(this._runStartedAt.Value);
        }

        this.StateChanged?.Invoke(snapshot);
        return OperationResult.Success(snapshot);
    }

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> Pause()
    {
        TimerSnapshot snapshot;
        lock (this._sync)
        {
            if (this._runState != TimerRunState.Running || this._runStartedAt is null)
            {
                return InvalidState("Only a running timer can be paused.");
            }

            long now = this._clock.GetTimestamp();
            this._accumulatedElapsed += this._clock.GetElapsedTime(this._runStartedAt.Value, now);
            this._runStartedAt = null;
            this._runState = TimerRunState.Paused;
            snapshot = this.CreateSnapshot(now);
        }

        this.StateChanged?.Invoke(snapshot);
        return OperationResult.Success(snapshot);
    }

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> ResumeTimer()
    {
        TimerSnapshot snapshot;
        lock (this._sync)
        {
            if (this._runState != TimerRunState.Paused)
            {
                return InvalidState("Only a paused timer can be resumed.");
            }

            this._runStartedAt = this._clock.GetTimestamp();
            this._runState = TimerRunState.Running;
            snapshot = this.CreateSnapshot(this._runStartedAt.Value);
        }

        this.StateChanged?.Invoke(snapshot);
        return OperationResult.Success(snapshot);
    }

    /// <inheritdoc/>
    public OperationResult<TimerSnapshot> Reset()
    {
        TimerSnapshot snapshot;
        lock (this._sync)
        {
            this._accumulatedElapsed = TimeSpan.Zero;
            this._runStartedAt = null;
            this._runState = TimerRunState.Ready;
            snapshot = this.CreateSnapshot(this._clock.GetTimestamp());
        }

        this.StateChanged?.Invoke(snapshot);
        return OperationResult.Success(snapshot);
    }

    /// <inheritdoc/>
    public TimerSnapshot Snapshot()
    {
        lock (this._sync)
        {
            return this.CreateSnapshot(this._clock.GetTimestamp());
        }
    }

    private static OperationResult<TimerSnapshot> InvalidDuration() =>
        OperationResult.Failure<TimerSnapshot>(
            ErrorCodes.InvalidDuration,
            "Enter a positive whole-second duration up to 99:59:59.");

    private static OperationResult<TimerSnapshot> InvalidState(string message) =>
        OperationResult.Failure<TimerSnapshot>(ErrorCodes.InvalidTimerState, message);

    private TimerSnapshot CreateSnapshot(long now)
    {
        TimeSpan elapsed = this._accumulatedElapsed;
        if (this._runState == TimerRunState.Running && this._runStartedAt is not null)
        {
            elapsed += this._clock.GetElapsedTime(this._runStartedAt.Value, now);
        }

        return new TimerSnapshot(this._runState, this._target, this._target - elapsed);
    }
}
