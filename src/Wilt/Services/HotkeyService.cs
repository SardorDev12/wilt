using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Wilt.Models;
using Wilt.Native;

namespace Wilt.Services;

/// <summary>
/// Global hotkeys for start/pause and skip (PRD "Should have", section 8).
/// Registers with RegisterHotKey against the overlay window's HWND and
/// listens for WM_HOTKEY via an HwndSource hook.
/// </summary>
public class HotkeyService : IDisposable
{
    private const int HotkeyIdStartPause = 0xB001;
    private const int HotkeyIdSkip = 0xB002;

    private readonly Window _window;
    private readonly TimerEngine _timerEngine;
    private HwndSource? _source;
    private bool _registered;

    public HotkeyService(Window window, TimerEngine timerEngine)
    {
        _window = window;
        _timerEngine = timerEngine;
    }

    public void Initialize(AppSettings settings)
    {
        var helper = new WindowInteropHelper(_window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);
        UpdateBindings(settings);
    }

    public void UpdateBindings(AppSettings settings)
    {
        Unregister();

        if (!settings.GlobalHotkeysEnabled || _source == null)
        {
            return;
        }

        var hwnd = _source.Handle;

        if (TryParseHotkey(settings.GlobalHotkeyStartPause, out var mods1, out var vk1))
        {
            NativeMethods.RegisterHotKey(hwnd, HotkeyIdStartPause, mods1, vk1);
        }

        if (TryParseHotkey(settings.GlobalHotkeySkip, out var mods2, out var vk2))
        {
            NativeMethods.RegisterHotKey(hwnd, HotkeyIdSkip, mods2, vk2);
        }

        _registered = true;
    }

    private void Unregister()
    {
        if (!_registered || _source == null)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyIdStartPause);
        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyIdSkip);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id == HotkeyIdStartPause)
            {
                _timerEngine.TogglePauseResume();
                handled = true;
            }
            else if (id == HotkeyIdSkip)
            {
                _timerEngine.Skip();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryParseHotkey(string spec, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        var parts = spec.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => NativeMethods.MOD_CONTROL,
                "alt" => NativeMethods.MOD_ALT,
                "shift" => NativeMethods.MOD_SHIFT,
                "win" or "windows" => NativeMethods.MOD_WIN,
                _ => 0u,
            };
        }

        var keyName = parts[^1];
        try
        {
            var key = (Key)Enum.Parse(typeof(Key), keyName, ignoreCase: true);
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
    }
}
