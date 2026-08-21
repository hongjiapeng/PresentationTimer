using System.Collections.Immutable;

namespace PresentationTimer.Core.Models;

/// <summary>Contains one desktop-only, labeled pairing choice for an active LAN endpoint.</summary>
public sealed record DesktopPairingCandidate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopPairingCandidate"/> class.
    /// </summary>
    /// <param name="interfaceLabel">The user-facing network interface label.</param>
    /// <param name="pairingUri">The token-bearing pairing URI for this endpoint.</param>
    /// <param name="qrPayload">The exact payload encoded in the candidate QR.</param>
    public DesktopPairingCandidate(string interfaceLabel, Uri pairingUri, string qrPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceLabel);
        ArgumentNullException.ThrowIfNull(pairingUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(qrPayload);
        this.InterfaceLabel = interfaceLabel;
        this.PairingUri = pairingUri;
        this.QrPayload = qrPayload;
    }

    /// <summary>Gets the user-facing network interface label.</summary>
    public string InterfaceLabel { get; init; }

    /// <summary>Gets the token-bearing pairing URI.</summary>
    public Uri PairingUri { get; init; }

    /// <summary>Gets the exact QR payload.</summary>
    public string QrPayload { get; init; }

    /// <summary>Gets the locally generated QR PNG bytes.</summary>
    public ImmutableArray<byte> QrPng { get; init; } = ImmutableArray<byte>.Empty;
}
