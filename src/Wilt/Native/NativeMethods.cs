using System;
using System.Runtime.InteropServices;

namespace Wilt.Native;

/// <summary>
/// Win32 interop for the overlay's always-on-top / layered / click-through /
/// non-activating window behavior (PRD section 10.2), and for global hotkeys.
/// </summary>
internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;

    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public const int WM_HOTKEY = 0x0312;

    public static void SetExStyle(IntPtr hwnd, int flag, bool enable)
    {
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        var newStyle = enable ? (style | flag) : (style & ~flag);
        if (newStyle != style)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, newStyle);
        }
    }
}
