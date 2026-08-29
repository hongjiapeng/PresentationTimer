using PresentationTimer.Core.Results;

namespace PresentationTimer.PowerPoint.Interop;

internal static class PresentationFilePath
{
    private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(
        [".ppt", ".pptx", ".pptm", ".pps", ".ppsx"],
        StringComparer.OrdinalIgnoreCase);

    internal static OperationResult<string> Validate(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return InvalidFile();
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return InvalidFile();
        }

        return File.Exists(normalizedPath) &&
            SupportedExtensions.Contains(Path.GetExtension(normalizedPath))
            ? OperationResult.Success(normalizedPath)
            : InvalidFile();
    }

    private static OperationResult<string> InvalidFile() =>
        OperationResult.Failure<string>(
            ErrorCodes.PresentationInvalidFile,
            "Select an existing PowerPoint presentation file.");
}
