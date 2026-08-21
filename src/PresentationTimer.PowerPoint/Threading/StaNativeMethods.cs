using System.Runtime.InteropServices;

namespace PresentationTimer.PowerPoint.Threading;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeMessage
{
    public readonly nint WindowHandle;
    public readonly uint Id;
    public readonly nuint WParam;
    public readonly nint LParam;
    public readonly uint Time;
    public readonly int PointX;
    public readonly int PointY;
    public readonly uint Private;
}

internal static class StaNativeMethods
{
    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(nint reserved);

    [DllImport("ole32.dll")]
    internal static extern void OleUninitialize();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetMessage(
        out NativeMessage message,
        nint windowHandle,
        uint minimum,
        uint maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PeekMessage(
        out NativeMessage message,
        nint windowHandle,
        uint minimum,
        uint maximum,
        uint removeMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(in NativeMessage message);

    [DllImport("user32.dll")]
    internal static extern nint DispatchMessage(in NativeMessage message);
}
