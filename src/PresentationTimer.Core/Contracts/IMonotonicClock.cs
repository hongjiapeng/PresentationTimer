namespace PresentationTimer.Core.Contracts;

/// <summary>
/// Supplies timestamps from a clock unaffected by wall-clock adjustments.
/// </summary>
public interface IMonotonicClock
{
    /// <summary>
    /// Gets the current monotonic timestamp.
    /// </summary>
    /// <returns>An opaque monotonic timestamp.</returns>
    long GetTimestamp();

    /// <summary>
    /// Calculates elapsed time between two timestamps from this clock.
    /// </summary>
    /// <param name="startingTimestamp">The earlier timestamp.</param>
    /// <param name="endingTimestamp">The later timestamp.</param>
    /// <returns>The elapsed duration.</returns>
    TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp);
}
