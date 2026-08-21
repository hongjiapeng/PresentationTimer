namespace PresentationTimer.Core.Tests.Fakes;

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new (2026, 8, 21, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => this._utcNow;

    public void Advance(TimeSpan duration)
    {
        this._utcNow += duration;
    }
}
