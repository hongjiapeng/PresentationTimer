using System.Collections.Immutable;
using QRCoder;

namespace PresentationTimer.Remote.Security;

internal static class PairingQrCodeGenerator
{
    internal static ImmutableArray<byte> CreatePng(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        return ImmutableArray.Create(qrCode.GetGraphic(8));
    }
}
