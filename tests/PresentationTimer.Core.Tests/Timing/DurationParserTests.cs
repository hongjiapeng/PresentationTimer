using PresentationTimer.Core.Results;
using PresentationTimer.Core.Timing;

namespace PresentationTimer.Core.Tests.Timing;

/// <summary>
/// Tests presenter duration parsing and validation.
/// </summary>
[TestClass]
public sealed class DurationParserTests
{
    /// <summary>
    /// Verifies that a fifteen-minute clock-style value is accepted.
    /// </summary>
    [TestMethod]
    public void Parse_FifteenMinuteClockValue_ReturnsFifteenMinutes()
    {
        // Act
        OperationResult<TimeSpan> result = DurationParser.Parse("15:00");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(TimeSpan.FromMinutes(15), result.Value);
    }

    /// <summary>
    /// Verifies that a plain integer is interpreted as minutes.
    /// </summary>
    [TestMethod]
    public void Parse_PlainInteger_ReturnsMinutes()
    {
        // Act
        OperationResult<TimeSpan> result = DurationParser.Parse("90");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(TimeSpan.FromMinutes(90), result.Value);
    }

    /// <summary>
    /// Verifies rejection of zero, negative, malformed, fractional, and unsupported inputs.
    /// </summary>
    /// <param name="text">The invalid text.</param>
    [TestMethod]
    [DataRow("0")]
    [DataRow("00:00")]
    [DataRow("-1")]
    [DataRow("not-time")]
    [DataRow("0.5")]
    [DataRow("10:99")]
    [DataRow("100:00:00")]
    public void Parse_InvalidValue_ReturnsStableValidationFailure(string text)
    {
        // Act
        OperationResult<TimeSpan> result = DurationParser.Parse(text);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.InvalidDuration, result.ErrorCode);
        Assert.AreEqual(default, result.Value);
    }
}
