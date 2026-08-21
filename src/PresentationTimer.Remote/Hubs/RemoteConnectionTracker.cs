namespace PresentationTimer.Remote.Hubs;

/// <summary>Tracks only the count of live authenticated presenter connections.</summary>
public sealed class RemoteConnectionTracker
{
    private int _count;

    internal event Action<int>? CountChanged;

    internal int Count => Volatile.Read(ref this._count);

    internal void Add()
    {
        int count = Interlocked.Increment(ref this._count);
        this.CountChanged?.Invoke(count);
    }

    internal void Remove()
    {
        int count = Math.Max(0, Interlocked.Decrement(ref this._count));
        if (count == 0)
        {
            Interlocked.Exchange(ref this._count, 0);
        }

        this.CountChanged?.Invoke(count);
    }
}
