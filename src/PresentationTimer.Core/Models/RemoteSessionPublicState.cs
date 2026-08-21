using System.Collections.Immutable;

namespace PresentationTimer.Core.Models;

/// <summary>
/// Contains token-free remote-session state safe to show on desktop and browser clients.
/// </summary>
public sealed record RemoteSessionPublicState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteSessionPublicState"/> class.
    /// </summary>
    /// <param name="status">The host lifecycle status.</param>
    /// <param name="candidateUrls">The current token-free phone-reachable candidate URLs.</param>
    /// <param name="selectedUrl">The currently selected token-free URL.</param>
    /// <param name="authenticatedConnectionCount">The number of authenticated hub connections.</param>
    /// <param name="lastErrorCode">A stable, safe error code for the most recent failure.</param>
    public RemoteSessionPublicState(
        RemoteSessionStatus status,
        ImmutableArray<Uri> candidateUrls,
        Uri? selectedUrl,
        int authenticatedConnectionCount,
        string? lastErrorCode)
    {
        this.Status = status;
        this.CandidateUrls = candidateUrls;
        this.SelectedUrl = selectedUrl;
        this.AuthenticatedConnectionCount = authenticatedConnectionCount;
        this.LastErrorCode = lastErrorCode;
    }

    /// <summary>Gets the host lifecycle status.</summary>
    public RemoteSessionStatus Status { get; init; }

    /// <summary>Gets the current token-free phone-reachable candidate URLs.</summary>
    public ImmutableArray<Uri> CandidateUrls { get; init; }

    /// <summary>Gets the currently selected token-free URL.</summary>
    public Uri? SelectedUrl { get; init; }

    /// <summary>Gets the number of authenticated hub connections.</summary>
    public int AuthenticatedConnectionCount { get; init; }

    /// <summary>Gets a stable, safe error code for the most recent failure.</summary>
    public string? LastErrorCode { get; init; }

    /// <summary>Gets the initial state with no active listener or browser credentials.</summary>
    public static RemoteSessionPublicState Initial { get; } = new RemoteSessionPublicState(
        RemoteSessionStatus.Stopped,
        ImmutableArray<Uri>.Empty,
        null,
        0,
        null);
}
