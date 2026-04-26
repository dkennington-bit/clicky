using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Clicky.Windows.Input;

public sealed class GlobalPushToTalkHotkeyListener : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftControl = 0xA2;
    private const int VkRightControl = 0xA3;
    private const int VkLeftMenu = 0xA4;
    private const int VkRightMenu = 0xA5;

    private readonly LowLevelKeyboardProcedure lowLevelKeyboardProcedure;
    private IntPtr hookHandle = IntPtr.Zero;
    private bool isControlDown;
    private bool isAltDown;
    private bool isPressed;

    public event EventHandler? PushToTalkPressed;
    public event EventHandler? PushToTalkReleased;

    public GlobalPushToTalkHotkeyListener()
    {
        lowLevelKeyboardProcedure = HandleKeyboardHook;
    }

    public void Start()
    {
        if (hookHandle != IntPtr.Zero)
        {
            return;
        }

        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessModule? currentModule = currentProcess.MainModule;
        IntPtr moduleHandle = currentModule is null
            ? IntPtr.Zero
            : GetModuleHandle(currentModule.ModuleName);

        hookHandle = SetWindowsHookEx(WhKeyboardLl, lowLevelKeyboardProcedure, moduleHandle, 0);
        if (hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install the global push-to-talk keyboard hook.");
        }
    }

    public void Dispose()
    {
        if (hookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(hookHandle);
            hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HandleKeyboardHook(int hookCode, IntPtr wordParameter, IntPtr longParameter)
    {
        if (hookCode >= 0)
        {
            int virtualKeyCode = Marshal.ReadInt32(longParameter);
            bool isKeyDown = wordParameter == WmKeyDown || wordParameter == WmSysKeyDown;
            bool isKeyUp = wordParameter == WmKeyUp || wordParameter == WmSysKeyUp;

            if (virtualKeyCode is VkControl or VkLeftControl or VkRightControl)
            {
                isControlDown = isKeyDown || (!isKeyUp && isControlDown);
            }

            if (virtualKeyCode is VkMenu or VkLeftMenu or VkRightMenu)
            {
                isAltDown = isKeyDown || (!isKeyUp && isAltDown);
            }

            UpdatePushToTalkState();
        }

        return CallNextHookEx(hookHandle, hookCode, wordParameter, longParameter);
    }

    private void UpdatePushToTalkState()
    {
        bool shouldBePressed = isControlDown && isAltDown;
        if (shouldBePressed == isPressed)
        {
            return;
        }

        isPressed = shouldBePressed;
        if (isPressed)
        {
            PushToTalkPressed?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PushToTalkReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private delegate IntPtr LowLevelKeyboardProcedure(int hookCode, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProcedure lowLevelKeyboardProcedure, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int hookCode, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
