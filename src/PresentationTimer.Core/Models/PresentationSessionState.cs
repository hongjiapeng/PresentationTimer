namespace PresentationTimer.Core.Models;

/// <summary>
/// Represents the single immutable state read by desktop and remote clients.
/// </summary>
public sealed record PresentationSessionState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PresentationSessionState"/> class.
    /// </summary>
    /// <param name="revision">A monotonically increasing state revision.</param>
    /// <param name="observedAtUtc">The diagnostic wall-clock observation time.</param>
    /// <param name="presentation">The presentation state slice.</param>
    /// <param name="timer">The timer state slice.</param>
    /// <param name="remote">The remote-session state slice.</param>
    public PresentationSessionState(
        long revision,
        DateTimeOffset observedAtUtc,
        PresentationSnapshot presentation,
        TimerSnapshot timer,
        RemoteSessionPublicState remote)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(remote);
        this.Revision = revision;
        this.ObservedAtUtc = observedAtUtc;
        this.Presentation = presentation;
        this.Timer = timer;
        this.Remote = remote;
    }

    /// <summary>Gets the monotonically increasing state revision.</summary>
    public long Revision { get; init; }

    /// <summary>Gets the diagnostic wall-clock observation time.</summary>
    public DateTimeOffset ObservedAtUtc { get; init; }

    /// <summary>Gets the presentation state slice.</summary>
    public PresentationSnapshot Presentation { get; init; }

    /// <summary>Gets the timer state slice.</summary>
    public TimerSnapshot Timer { get; init; }

    /// <summary>Gets the remote-session state slice.</summary>
    public RemoteSessionPublicState Remote { get; init; }
}
