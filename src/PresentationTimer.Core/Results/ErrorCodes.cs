namespace PresentationTimer.Core.Results;

/// <summary>
/// Defines stable error codes exposed across process boundaries and UI layers.
/// </summary>
public static class ErrorCodes
{
    /// <summary>The process is closing and no new application command can be accepted.</summary>
    public const string ApplicationClosing = "application.closing";

    /// <summary>The duration input is malformed, non-positive, fractional, or unsupported.</summary>
    public const string InvalidDuration = "timer.invalid_duration";

    /// <summary>The requested timer command is invalid for the current run state.</summary>
    public const string InvalidTimerState = "timer.invalid_state";

    /// <summary>The requested presentation operation is not currently available.</summary>
    public const string PresentationUnavailable = "presentation.unavailable";

    /// <summary>The selected presentation path is empty, missing, or unsupported.</summary>
    public const string PresentationInvalidFile = "presentation.invalid_file";

    /// <summary>PowerPoint could not open or start the selected presentation.</summary>
    public const string PresentationOpenFailed = "presentation.open_failed";

    /// <summary>PowerPoint temporarily rejected the requested operation because it is busy.</summary>
    public const string PresentationBusy = "presentation.busy";

    /// <summary>The attached PowerPoint automation server disconnected.</summary>
    public const string PresentationDisconnected = "presentation.disconnected";

    /// <summary>The remote session could not start.</summary>
    public const string RemoteStartFailed = "remote.start_failed";

    /// <summary>No non-loopback local IPv4 address is available for phone pairing.</summary>
    public const string RemoteNoLanAddress = "remote.no_lan_address";

    /// <summary>The supplied remote-session credential is invalid or expired.</summary>
    public const string RemoteCredentialInvalid = "remote.credential_invalid";
}
