using PresentationTimer.PowerPoint.Notes;

namespace PresentationTimer.Core.Tests.PowerPoint;

/// <summary>
/// Verifies pure speaker-note text normalization.
/// </summary>
[TestClass]
public sealed class SpeakerNotesNormalizerTests
{
    /// <summary>Verifies empty note bodies produce empty text.</summary>
    [TestMethod]
    public void Normalize_WithNoMeaningfulBodies_ReturnsEmptyText()
    {
        Assert.AreEqual(string.Empty, SpeakerNotesNormalizer.Normalize(new string?[] { null, string.Empty, "  " }));
    }

    /// <summary>Verifies mixed PowerPoint line endings become platform line endings.</summary>
    [TestMethod]
    public void Normalize_WithMixedLineEndings_PreservesLinesUsingPlatformConvention()
    {
        string result = SpeakerNotesNormalizer.Normalize(
            new List<string?> { "first\r\nsecond\rthird\nfourth" });

        Assert.AreEqual(string.Join(Environment.NewLine, "first", "second", "third", "fourth"), result);
    }

    /// <summary>Verifies multiple note placeholders remain distinguishable.</summary>
    [TestMethod]
    public void Normalize_WithMultipleBodies_SeparatesThemWithBlankLine()
    {
        string result = SpeakerNotesNormalizer.Normalize(new List<string?> { "alpha", "beta" });

        Assert.AreEqual($"alpha{Environment.NewLine}{Environment.NewLine}beta", result);
    }

    /// <summary>Verifies markup-like characters are preserved as inert plain text.</summary>
    [TestMethod]
    public void Normalize_WithMarkupLikeText_ReturnsItVerbatimAsText()
    {
        const string Input = "<img src=x onerror=alert(1)> & notes";

        Assert.AreEqual(Input, SpeakerNotesNormalizer.Normalize(new List<string?> { Input }));
    }
}
