using System.Runtime.InteropServices;

namespace Clicky.Windows.Infrastructure;

public static class DpiAwareness
{
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    public static void EnablePerMonitorDpiAwareness()
    {
        _ = SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiAwarenessContext);
}
