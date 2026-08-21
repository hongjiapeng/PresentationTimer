using System.Diagnostics;
using PresentationTimer.Core.Contracts;

namespace PresentationTimer.Core.Timing;

/// <summary>
/// Implements monotonic timing over <see cref="Stopwatch.GetTimestamp()"/>.
/// </summary>
public sealed class StopwatchMonotonicClock : IMonotonicClock
{
    /// <inheritdoc/>
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <inheritdoc/>
    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
        Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);
}
