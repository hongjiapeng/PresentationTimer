namespace PresentationTimer.PowerPoint.Notes;

/// <summary>
/// Normalizes PowerPoint note placeholder text without interpreting it as markup.
/// </summary>
public static class SpeakerNotesNormalizer
{
    /// <summary>
    /// Joins meaningful note bodies with one blank line and normalizes line endings.
    /// </summary>
    /// <param name="noteBodies">Plain-text body and vertical-body placeholder values.</param>
    /// <returns>Normalized plain text.</returns>
    public static string Normalize(IEnumerable<string?> noteBodies)
    {
        ArgumentNullException.ThrowIfNull(noteBodies);
        string[] bodies = noteBodies
            .Where(static body => !string.IsNullOrWhiteSpace(body))
            .Select(static body => NormalizeLineEndings(body!).Trim())
            .Where(static body => body.Length > 0)
            .ToArray();

        return string.Join(Environment.NewLine + Environment.NewLine, bodies);
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
