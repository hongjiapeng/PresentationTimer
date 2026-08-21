namespace PresentationTimer.Core.Models;

/// <summary>
/// Describes the observable PowerPoint connection state.
/// </summary>
public enum PresentationConnectionState
{
    /// <summary>The required PowerPoint integration is unavailable.</summary>
    Unavailable,

    /// <summary>PowerPoint is installed but is not currently running.</summary>
    NotRunning,

    /// <summary>PowerPoint is running without an open presentation.</summary>
    NoPresentation,

    /// <summary>A presentation is open without an active slide show.</summary>
    NoSlideShow,

    /// <summary>An active slide show is available for control.</summary>
    Running,

    /// <summary>A previously attached PowerPoint process disconnected.</summary>
    Disconnected,
}
