namespace PresentationTimer.Core.Models;

/// <summary>
/// Contains the managed, immutable presentation primitives visible outside the Office adapter.
/// </summary>
public sealed record PresentationSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PresentationSnapshot"/> class.
    /// </summary>
    /// <param name="connection">The current presentation connection state.</param>
    /// <param name="currentSlideIndex">The one-based current slide index, when available.</param>
    /// <param name="totalSlides">The total slide count, when available.</param>
    /// <param name="speakerNotes">Plain-text speaker notes for the current slide.</param>
    /// <param name="lastErrorCode">A stable, safe error code for the most recent failure.</param>
    public PresentationSnapshot(
        PresentationConnectionState connection,
        int? currentSlideIndex,
        int? totalSlides,
        string speakerNotes,
        string? lastErrorCode)
    {
        ArgumentNullException.ThrowIfNull(speakerNotes);
        this.Connection = connection;
        this.CurrentSlideIndex = currentSlideIndex;
        this.TotalSlides = totalSlides;
        this.SpeakerNotes = speakerNotes;
        this.LastErrorCode = lastErrorCode;
    }

    /// <summary>Gets the current presentation connection state.</summary>
    public PresentationConnectionState Connection { get; init; }

    /// <summary>Gets the one-based current slide index, when available.</summary>
    public int? CurrentSlideIndex { get; init; }

    /// <summary>Gets the total slide count, when available.</summary>
    public int? TotalSlides { get; init; }

    /// <summary>Gets plain-text speaker notes for the current slide.</summary>
    public string SpeakerNotes { get; init; }

    /// <summary>Gets a stable, safe error code for the most recent failure.</summary>
    public string? LastErrorCode { get; init; }

    /// <summary>Gets the initial snapshot used before a PowerPoint process is attached.</summary>
    public static PresentationSnapshot Initial { get; } = new PresentationSnapshot(
        PresentationConnectionState.NotRunning,
        null,
        null,
        string.Empty,
        null);

    /// <summary>
    /// Removes presentation content that is stale after a hard disconnection state.
    /// </summary>
    /// <returns>A safe snapshot with stale presentation fields cleared.</returns>
    public PresentationSnapshot WithoutStaleContent()
    {
        return this.Connection is PresentationConnectionState.Unavailable
            or PresentationConnectionState.NotRunning
            or PresentationConnectionState.NoPresentation
            or PresentationConnectionState.Disconnected
            ? this with
            {
                CurrentSlideIndex = null,
                TotalSlides = null,
                SpeakerNotes = string.Empty,
            }
            : this;
    }
}
