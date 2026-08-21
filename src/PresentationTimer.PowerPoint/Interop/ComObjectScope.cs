using System.Runtime.InteropServices;

namespace PresentationTimer.PowerPoint.Interop;

/// <summary>
/// Owns transient RCWs acquired by one adapter operation and releases them once in reverse order.
/// </summary>
/// <remarks>
/// The long-lived application root and COM event arguments are deliberately excluded because their
/// lifetimes are managed by the controller and PowerPoint connection-point callbacks respectively.
/// </remarks>
internal sealed class ComObjectScope : IDisposable
{
    private readonly HashSet<object> _knownObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
    private readonly List<object> _ownedObjects = new List<object>();

    public T Track<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Marshal.IsComObject(value) && this._knownObjects.Add(value))
        {
            this._ownedObjects.Add(value);
        }

        return value;
    }

    public void Dispose()
    {
        for (int index = this._ownedObjects.Count - 1; index >= 0; index--)
        {
            _ = Marshal.FinalReleaseComObject(this._ownedObjects[index]);
        }

        this._ownedObjects.Clear();
        this._knownObjects.Clear();
    }
}
