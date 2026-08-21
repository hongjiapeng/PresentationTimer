using PresentationTimer.Remote.Security;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;
using ZXing.ImageSharp;

namespace PresentationTimer.Remote.Tests.Security;

/// <summary>Verifies the rendered QR pixels carry the exact pairing payload.</summary>
[TestClass]
public sealed class PairingQrCodeGeneratorTests
{
    /// <summary>Verifies an independent decoder recovers the source URI byte-for-byte.</summary>
    [TestMethod]
    public void CreatePng_TokenBearingUri_DecodesToExactPayload()
    {
        // Arrange
        const string payload = "http://192.168.50.23:49152/pair?t=AbC_-0123456789";

        // Act
        byte[] png = PairingQrCodeGenerator.CreatePng(payload).ToArray();
        using Image<Rgba32> image = Image.Load<Rgba32>(png);
        var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32>
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                TryHarder = true,
                TryInverted = true,
            },
        };
        Result? result = reader.Decode(image);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(payload, result.Text);
    }
}
