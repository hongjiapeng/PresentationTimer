using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Contracts;

/// <summary>
/// Provides the single command gateway and aggregate state for desktop and remote callers.
/// </summary>
public interface IPresentationSessionService
{
    /// <summary>Occurs when desktop-only pairing material changes or must be withdrawn.</summary>
    event Action<DesktopPairingDescriptor?>? PairingChanged;

    /// <summary>Occurs after a component slice is merged into aggregate state.</summary>
    event Action<PresentationSessionState>? StateChanged;

    /// <summary>Gets the latest immutable aggregate state.</summary>
    PresentationSessionState State { get; }

    /// <summary>Atomically rejects subsequent application commands during coordinated shutdown.</summary>
    void BeginShutdown();

    /// <summary>Detaches the service from timer and infrastructure events during coordinated shutdown.</summary>
    void DetachEvents();

    /// <summary>Parses and configures the timer target.</summary>
    /// <param name="durationText">A duration in minutes, minutes:seconds, or hours:minutes:seconds.</param>
    /// <returns>The resulting timer snapshot or validation failure.</returns>
    OperationResult<TimerSnapshot> ConfigureTimer(string? durationText);

    /// <summary>Starts the ready timer.</summary>
    /// <returns>The resulting timer snapshot or an invalid-state failure.</returns>
    OperationResult<TimerSnapshot> StartTimer();

    /// <summary>Pauses the running timer.</summary>
    /// <returns>The resulting timer snapshot or an invalid-state failure.</returns>
    OperationResult<TimerSnapshot> PauseTimer();

    /// <summary>Resumes the paused timer.</summary>
    /// <returns>The resulting timer snapshot or an invalid-state failure.</returns>
    OperationResult<TimerSnapshot> ResumeTimer();

    /// <summary>Resets the timer to the configured target.</summary>
    /// <returns>The resulting ready timer snapshot.</returns>
    OperationResult<TimerSnapshot> ResetTimer();

    /// <summary>Gets a freshly calculated timer snapshot and merges it when changed.</summary>
    /// <returns>The current timer snapshot.</returns>
    TimerSnapshot RefreshTimer();

    /// <summary>Starts presentation monitoring.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>A task representing the operation.</returns>
    Task StartPresentationMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a user-selected presentation read-only and starts its slide show.</summary>
    /// <param name="filePath">The local presentation path selected by the desktop user.</param>
    /// <param name="cancellationToken">Cancels before submission.</param>
    /// <returns>The structured open result.</returns>
    Task<OperationResult> OpenPresentationAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>Navigates exactly once to the next slide.</summary>
    /// <param name="cancellationToken">Cancels before submission.</param>
    /// <returns>The structured navigation result.</returns>
    Task<OperationResult> NextSlideAsync(CancellationToken cancellationToken = default);

    /// <summary>Navigates exactly once to the previous slide.</summary>
    /// <param name="cancellationToken">Cancels before submission.</param>
    /// <returns>The structured navigation result.</returns>
    Task<OperationResult> PreviousSlideAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts a fresh remote session.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>Desktop-only pairing material or a structured failure.</returns>
    Task<OperationResult<DesktopPairingDescriptor>> StartRemoteSessionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Ends the active remote session and revokes its credentials.</summary>
    /// <param name="cancellationToken">Cancels the bounded stop wait.</param>
    /// <returns>A task representing the operation.</returns>
    Task EndRemoteSessionAsync(CancellationToken cancellationToken = default);
}
