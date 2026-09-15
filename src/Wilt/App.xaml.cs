using System;
using System.Threading;
using System.Windows;
using Wilt.Services;
using Wilt.Views;

namespace Wilt;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private TrayIconService? _trayIconService;
    private HotkeyService? _hotkeyService;
    private OverlayWindow? _overlayWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "Wilt.SingleInstance.6b6f3a2e-7e02-4a7a-8f2e-1a7f5a5a3b10", out var isNew);
        if (!isNew)
        {
            // Another instance is already running; bail out quietly.
            Shutdown();
            return;
        }

        var settingsService = new SettingsService();
        var settings = settingsService.Load();

        var historyService = new HistoryService();

        var timerEngine = new TimerEngine(settings);

        _overlayWindow = new OverlayWindow(timerEngine, settingsService, historyService);
        _overlayWindow.Show();

        _trayIconService = new TrayIconService(timerEngine, settingsService, _overlayWindow);
        _trayIconService.Initialize();

        _hotkeyService = new HotkeyService(_overlayWindow, timerEngine);
        _hotkeyService.Initialize(settings);

        settingsService.SettingsChanged += (_, s) => _hotkeyService.UpdateBindings(s);

        StartupService.Apply(settings.LaunchOnStartup);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
