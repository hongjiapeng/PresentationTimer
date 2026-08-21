using System.Runtime.InteropServices;

namespace PresentationTimer.PowerPoint.Interop;

internal enum ActiveObjectStatus
{
    Attached,
    Unavailable,
    NotRunning,
    Failed,
}

internal sealed record ActiveObjectResult
{
    public ActiveObjectResult(ActiveObjectStatus status, object? instance, int hResult)
    {
        this.Status = status;
        this.Instance = instance;
        this.HResult = hResult;
    }

    public ActiveObjectStatus Status { get; }

    public object? Instance { get; }

    public int HResult { get; }
}

/// <summary>Resolves an already-running COM object without activation.</summary>
internal interface IActiveObjectResolver
{
    /// <summary>Resolves an active object registered under the supplied ProgID.</summary>
    /// <param name="programmaticIdentifier">The COM programmatic identifier.</param>
    /// <returns>A categorized native lookup result.</returns>
    ActiveObjectResult Resolve(string programmaticIdentifier);
}

/// <summary>
/// Resolves an already-running COM automation server without activating a new process.
/// </summary>
internal sealed class ActiveObjectResolver : IActiveObjectResolver
{
    private const int ClassNotRegistered = unchecked((int)0x80040154);
    private const int InvalidClassString = unchecked((int)0x800401F3);
    private const int MonikerUnavailable = unchecked((int)0x800401E3);

    public ActiveObjectResult Resolve(string programmaticIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programmaticIdentifier);

        int classResult = CLSIDFromProgID(programmaticIdentifier, out Guid classIdentifier);
        if (classResult == ClassNotRegistered || classResult == InvalidClassString)
        {
            return new ActiveObjectResult(ActiveObjectStatus.Unavailable, null, classResult);
        }

        if (classResult < 0)
        {
            return new ActiveObjectResult(ActiveObjectStatus.Failed, null, classResult);
        }

        int activeResult = GetActiveObject(in classIdentifier, 0, out object? instance);
        if (activeResult == MonikerUnavailable)
        {
            return new ActiveObjectResult(ActiveObjectStatus.NotRunning, null, activeResult);
        }

        return activeResult < 0 || instance is null
            ? new ActiveObjectResult(ActiveObjectStatus.Failed, null, activeResult)
            : new ActiveObjectResult(ActiveObjectStatus.Attached, instance, activeResult);
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string programmaticIdentifier, out Guid classIdentifier);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(
        in Guid classIdentifier,
        nint reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
}
