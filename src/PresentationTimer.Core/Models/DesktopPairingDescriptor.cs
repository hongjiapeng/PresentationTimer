using System.Collections.Immutable;

namespace PresentationTimer.Core.Models;

/// <summary>
/// Contains desktop-only pairing material that must never enter shared presenter state.
/// </summary>
public sealed record DesktopPairingDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopPairingDescriptor"/> class.
    /// </summary>
    /// <param name="pairingUri">The token-bearing URI opened by a phone during pairing.</param>
    /// <param name="qrPayload">The exact payload encoded in the displayed QR code.</param>
    public DesktopPairingDescriptor(Uri pairingUri, string qrPayload)
    {
        ArgumentNullException.ThrowIfNull(pairingUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(qrPayload);
        this.PairingUri = pairingUri;
        this.QrPayload = qrPayload;
    }

    /// <summary>Gets the token-bearing URI opened by a phone during pairing.</summary>
    public Uri PairingUri { get; init; }

    /// <summary>Gets the exact payload encoded in the displayed QR code.</summary>
    public string QrPayload { get; init; }

    /// <summary>Gets the locally generated QR PNG bytes for desktop display.</summary>
    public ImmutableArray<byte> QrPng { get; init; } = ImmutableArray<byte>.Empty;

    /// <summary>Gets all distinct, labeled desktop pairing choices for the active session.</summary>
    public ImmutableArray<DesktopPairingCandidate> Candidates { get; init; } =
        ImmutableArray<DesktopPairingCandidate>.Empty;
}
