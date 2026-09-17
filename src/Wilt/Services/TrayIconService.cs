using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Wilt.Models;
using Wilt.Views;
using Application = System.Windows.Application;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;

namespace Wilt.Services;

/// <summary>
/// System tray icon with quick actions and a live tooltip showing the
/// current phase/time-remaining (PRD 8, "Must have").
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly TimerEngine _timerEngine;
    private readonly SettingsService _settingsService;
    private readonly OverlayWindow _overlayWindow;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _startPauseItem;
    private SettingsWindow? _settingsWindow;

    public TrayIconService(TimerEngine timerEngine, SettingsService settingsService, OverlayWindow overlayWindow)
    {
        _timerEngine = timerEngine;
        _settingsService = settingsService;
        _overlayWindow = overlayWindow;
    }

    public void Initialize()
    {
        var menu = new ContextMenuStrip();

        _startPauseItem = new ToolStripMenuItem("Start", null, (_, _) => _timerEngine.TogglePauseResume());
        menu.Items.Add(_startPauseItem);
        menu.Items.Add(new ToolStripMenuItem("Skip", null, (_, _) => _timerEngine.Skip()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Settings...", null, (_, _) => OpenSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit Wilt", null, (_, _) => Application.Current.Shutdown()));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Wilt",
            ContextMenuStrip = menu,
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        _timerEngine.Tick += (_, _) => UpdateTooltipAndMenu();
        _timerEngine.PhaseStarted += (_, _) => UpdateTooltipAndMenu();
        _timerEngine.PhaseCompleted += (_, phase) => ShowCompletionToast(phase);
        UpdateTooltipAndMenu();
    }

    /// <summary>
    /// Native Windows notification when a session ends, so it's still
    /// noticeable even if the overlay is out of view or behind other
    /// windows - a second channel alongside the voice/chime in SoundService.
    /// </summary>
    private void ShowCompletionToast(Phase completedPhase)
    {
        if (_notifyIcon == null)
        {
            return;
        }

        var (title, text) = completedPhase == Phase.Focus
            ? ("Focus session complete", "Time for a break.")
            : ("Break's over", "Ready to focus?");

        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settingsService, _overlayWindow);
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void UpdateTooltipAndMenu()
    {
        if (_notifyIcon == null || _startPauseItem == null)
        {
            return;
        }

        var phaseLabel = _timerEngine.Phase switch
        {
            Phase.Inviting => "Ready to start",
            Phase.Focus => "Focus",
            Phase.ShortBreak => "Short break",
            Phase.LongBreak => "Long break",
            _ => "",
        };

        var remaining = TimeSpan.FromSeconds(Math.Max(0, _timerEngine.RemainingSeconds));
        var remainingText = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        var text = _timerEngine.Phase == Phase.Inviting
            ? "Wilt — ready to start"
            : $"Wilt — {phaseLabel} — {remainingText} remaining";

        // NotifyIcon.Text has a 63-char limit.
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;

        _startPauseItem.Text = _timerEngine.RunState == RunState.Running ? "Pause" : "Start";
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
