namespace PresentationTimer.Core.Models;

/// <summary>
/// Describes the lifecycle of the local presenter remote session.
/// </summary>
public enum RemoteSessionStatus
{
    /// <summary>No remote listener or credential is active.</summary>
    Stopped,

    /// <summary>The remote host is starting.</summary>
    Starting,

    /// <summary>The remote host is ready for pairing and commands.</summary>
    Ready,

    /// <summary>The remote host failed to start or remain healthy.</summary>
    Failed,

    /// <summary>The remote host is revoking credentials and stopping.</summary>
    Stopping,
}
