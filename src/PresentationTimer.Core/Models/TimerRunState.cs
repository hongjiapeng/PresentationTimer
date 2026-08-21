namespace PresentationTimer.Core.Models;

/// <summary>
/// Describes whether the presentation timer is ready, running, or paused.
/// </summary>
public enum TimerRunState
{
    /// <summary>The timer is reset and ready to start.</summary>
    Ready,

    /// <summary>The timer is accumulating elapsed time.</summary>
    Running,

    /// <summary>The timer is preserving its elapsed time until resumed.</summary>
    Paused,
}
