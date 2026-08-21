using PresentationTimer.Core.Models;

namespace PresentationTimer.Remote.Dtos;

/// <summary>
/// Defines the complete token-free presenter state allowed to cross the browser boundary.
/// </summary>
public sealed record PresenterStateDto
{
    /// <summary>Initializes a new instance of the <see cref="PresenterStateDto"/> class.</summary>
    /// <param name="revision">The aggregate revision.</param>
    /// <param name="presentationStatus">The safe presentation status.</param>
    /// <param name="currentSlideIndex">The one-based current slide index.</param>
    /// <param name="totalSlides">The current presentation slide count.</param>
    /// <param name="speakerNotes">The current plain-text speaker notes.</param>
    /// <param name="timerStatus">The timer run-state name.</param>
    /// <param name="isOvertime">Whether the timer is in overtime.</param>
    /// <param name="timerDisplaySeconds">The positive whole-second timer value.</param>
    public PresenterStateDto(
        long revision,
        string presentationStatus,
        int? currentSlideIndex,
        int? totalSlides,
        string speakerNotes,
        string timerStatus,
        bool isOvertime,
        long timerDisplaySeconds)
    {
        this.Revision = revision;
        this.PresentationStatus = presentationStatus;
        this.CurrentSlideIndex = currentSlideIndex;
        this.TotalSlides = totalSlides;
        this.SpeakerNotes = speakerNotes;
        this.TimerStatus = timerStatus;
        this.IsOvertime = isOvertime;
        this.TimerDisplaySeconds = timerDisplaySeconds;
    }

    /// <summary>Gets the aggregate state revision.</summary>
    public long Revision { get; }

    /// <summary>Gets the safe presentation readiness name.</summary>
    public string PresentationStatus { get; }

    /// <summary>Gets the one-based current slide index.</summary>
    public int? CurrentSlideIndex { get; }

    /// <summary>Gets the current presentation slide count.</summary>
    public int? TotalSlides { get; }

    /// <summary>Gets current speaker notes as plain text.</summary>
    public string SpeakerNotes { get; }

    /// <summary>Gets the timer run-state name.</summary>
    public string TimerStatus { get; }

    /// <summary>Gets a value indicating whether the displayed timer value is overtime.</summary>
    public bool IsOvertime { get; }

    /// <summary>Gets the positive whole-second timer display value.</summary>
    public long TimerDisplaySeconds { get; }

    /// <summary>Projects the allow-listed browser shape from aggregate application state.</summary>
    /// <param name="state">The authoritative aggregate state.</param>
    /// <returns>A browser-safe full snapshot.</returns>
    public static PresenterStateDto FromState(PresentationSessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new PresenterStateDto(
            state.Revision,
            state.Presentation.Connection.ToString(),
            state.Presentation.CurrentSlideIndex,
            state.Presentation.TotalSlides,
            state.Presentation.SpeakerNotes,
            state.Timer.RunState.ToString(),
            state.Timer.IsOvertime,
            checked((long)state.Timer.DisplayValue.TotalSeconds));
    }
}
