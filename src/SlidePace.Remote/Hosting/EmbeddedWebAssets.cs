using System.Reflection;

namespace SlidePace.Remote.Hosting;

internal static class EmbeddedWebAssets
{
    internal static byte[] Read(string relativeName)
    {
        string resourceName = $"SlidePace.Remote.wwwroot.{relativeName.Replace('/', '.')}";
        using Stream stream = typeof(EmbeddedWebAssets).Assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Embedded web asset '{relativeName}' is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
