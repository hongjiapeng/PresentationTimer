using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace PresentationTimer.App.Imaging;

internal static class PairingBitmapFactory
{
    internal static async Task<BitmapImage> CreateAsync(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(pngBytes);
            _ = await writer.StoreAsync();
            _ = writer.DetachStream();
        }

        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}
