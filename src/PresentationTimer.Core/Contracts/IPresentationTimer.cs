using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Contracts;

/// <summary>
/// Controls and calculates the authoritative presentation timer.
/// </summary>
public interface IPresentationTimer
{
    /// <summary>Occurs immediately after a timer command changes state.</summary>
    event Action<TimerSnapshot>? StateChanged;

    /// <summary>Gets a freshly calculated timer snapshot.</summary>
    TimerSnapshot State { get; }

    /// <summary>Configures a new target while the timer is ready.</summary>
    /// <param name="target">A positive, whole-second target duration.</param>
    /// <returns>The resulting snapshot or a validation failure.</returns>
    OperationResult<TimerSnapshot> Configure(TimeSpan target);

    /// <summary>Starts a ready timer.</summary>
    /// <returns>The resulting snapshot or an invalid-state failure.</returns>
    OperationResult<TimerSnapshot> Start();

    /// <summary>Pauses a running timer.</summary>
    /// <returns>The resulting snapshot or an invalid-state failure.</returns>
    OperationResult<TimerSnapshot> Pause();

    /// <summary>Resumes a paused timer.</summary>
    /// <returns>The resulting snapshot or an invalid-state failure.</returns>
    OperationResult<TimerSnapshot> ResumeTimer();

    /// <summary>Resets any timer state to its configured target.</summary>
    /// <returns>The resulting ready snapshot.</returns>
    OperationResult<TimerSnapshot> Reset();

    /// <summary>Calculates a current snapshot without changing timer state.</summary>
    /// <returns>The current timer snapshot.</returns>
    TimerSnapshot Snapshot();
}
