using System;
using System.Windows;
using System.Windows.Interop;
using Wilt.Native;
using Wilt.Services;

namespace Wilt.Views;

/// <summary>
/// A tiny always-on-top, always-clickable companion to OverlayWindow, shown
/// only while click-through is enabled. WS_EX_TRANSPARENT makes a window
/// invisible to mouse input at the Win32 level for its *entire* client area
/// - there's no way to exempt one button inside a click-through window - so
/// once OverlayWindow goes click-through, nothing in it (including its own
/// control strip) can be clicked anymore, hover included. This window is
/// never given that style, so it's the one thing guaranteed to still work,
/// letting the user turn click-through back off without ever getting stuck.
/// </summary>
public partial class ClickThroughIndicatorWindow : Window
{
    private readonly SettingsService _settingsService;

    public ClickThroughIndicatorWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        // Non-activating and excluded from alt-tab/taskbar, same as the main
        // overlay - but WS_EX_TRANSPARENT is deliberately never applied here.
        NativeMethods.SetExStyle(hwnd, NativeMethods.WS_EX_NOACTIVATE, true);
        NativeMethods.SetExStyle(hwnd, NativeMethods.WS_EX_TOOLWINDOW, true);
        NativeMethods.SetExStyle(hwnd, NativeMethods.WS_EX_TOPMOST, true);
    }

    private void OnDisableClick(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Current.Clone();
        settings.ClickThrough = false;
        _settingsService.Save(settings);
    }
}
