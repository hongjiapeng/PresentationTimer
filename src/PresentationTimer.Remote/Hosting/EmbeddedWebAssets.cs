using System.Reflection;

namespace PresentationTimer.Remote.Hosting;

internal static class EmbeddedWebAssets
{
    internal static byte[] Read(string relativeName)
    {
        string resourceName = $"PresentationTimer.Remote.wwwroot.{relativeName.Replace('/', '.')}";
        using Stream stream = typeof(EmbeddedWebAssets).Assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Embedded web asset '{relativeName}' is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
