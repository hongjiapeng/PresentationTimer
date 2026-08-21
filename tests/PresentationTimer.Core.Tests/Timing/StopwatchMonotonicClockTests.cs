using System.Diagnostics;
using PresentationTimer.Core.Timing;

namespace PresentationTimer.Core.Tests.Timing;

/// <summary>
/// Tests timestamp conversion in the production monotonic clock adapter.
/// </summary>
[TestClass]
public sealed class StopwatchMonotonicClockTests
{
    /// <summary>
    /// Verifies that one stopwatch frequency interval converts to one second.
    /// </summary>
    [TestMethod]
    public void GetElapsedTime_OneFrequencyInterval_ReturnsOneSecond()
    {
        // Arrange
        var clock = new StopwatchMonotonicClock();

        // Act
        TimeSpan elapsed = clock.GetElapsedTime(0, Stopwatch.Frequency);

        // Assert
        Assert.AreEqual(TimeSpan.FromSeconds(1), elapsed);
    }
}
