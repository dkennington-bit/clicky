using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clicky.Windows.Infrastructure;

public static class NativeWindowUtilities
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int SwpNoActivate = 0x0010;
    private const int SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTopmost = new(-1);

    public static void MakeWindowClickThroughAndTopmost(Window window, Rectangle pixelBounds)
    {
        var windowInteropHelper = new WindowInteropHelper(window);
        IntPtr windowHandle = windowInteropHelper.Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        int extendedWindowStyle = GetWindowLong(windowHandle, GwlExStyle);
        _ = SetWindowLong(
            windowHandle,
            GwlExStyle,
            extendedWindowStyle | WsExTransparent | WsExToolWindow | WsExLayered);

        _ = SetWindowPos(
            windowHandle,
            HwndTopmost,
            pixelBounds.Left,
            pixelBounds.Top,
            pixelBounds.Width,
            pixelBounds.Height,
            SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfterWindowHandle,
        int x,
        int y,
        int width,
        int height,
        int flags);
}
