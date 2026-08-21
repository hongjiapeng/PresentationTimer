namespace PresentationTimer.Core.Models;

/// <summary>
/// Represents one authoritative timer calculation.
/// </summary>
public sealed record TimerSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimerSnapshot"/> class.
    /// </summary>
    /// <param name="runState">Whether the timer is ready, running, or paused.</param>
    /// <param name="target">The configured target duration.</param>
    /// <param name="remaining">Signed remaining time; negative values represent overtime.</param>
    public TimerSnapshot(TimerRunState runState, TimeSpan target, TimeSpan remaining)
    {
        this.RunState = runState;
        this.Target = target;
        this.Remaining = remaining;
    }

    /// <summary>Gets whether the timer is ready, running, or paused.</summary>
    public TimerRunState RunState { get; init; }

    /// <summary>Gets the configured target duration.</summary>
    public TimeSpan Target { get; init; }

    /// <summary>Gets signed remaining time; negative values represent overtime.</summary>
    public TimeSpan Remaining { get; init; }

    /// <summary>Gets a value indicating whether the timer is beyond its configured target.</summary>
    public bool IsOvertime => this.Remaining < TimeSpan.Zero;

    /// <summary>Gets the positive amount displayed by a countdown or overtime view.</summary>
    public TimeSpan DisplayValue => this.IsOvertime ? this.Remaining.Duration() : this.Remaining;
}
