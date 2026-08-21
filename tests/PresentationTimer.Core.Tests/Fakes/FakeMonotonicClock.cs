using PresentationTimer.Core.Contracts;

namespace PresentationTimer.Core.Tests.Fakes;

internal sealed class FakeMonotonicClock : IMonotonicClock
{
    private long _timestamp;

    public long GetTimestamp() => this._timestamp;

    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
        TimeSpan.FromTicks(endingTimestamp - startingTimestamp);

    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        this._timestamp = checked(this._timestamp + duration.Ticks);
    }
}
