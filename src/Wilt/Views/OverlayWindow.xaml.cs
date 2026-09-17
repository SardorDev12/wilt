using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wilt.Controls;
using Wilt.Models;
using Wilt.Native;
using Wilt.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Wilt.Views;

/// <summary>
/// The always-on-top, layered, non-activating character overlay (PRD 7.0, 9).
/// The single small window the app spends its whole runtime showing.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly TimerEngine _timerEngine;
    private readonly SettingsService _settingsService;
    private readonly HistoryService _historyService;
    private readonly SoundService _soundService = new();

    private IntPtr _hwnd;
    private bool _isPointerOver;
    private ICharacterView _activeCharacter;

    public OverlayWindow(TimerEngine timerEngine, SettingsService settingsService, HistoryService historyService)
    {
        InitializeComponent();
        _timerEngine = timerEngine;
        _settingsService = settingsService;
        _historyService = historyService;

        _activeCharacter = StandingCharacter;
        _activeCharacter.SetActive(true);

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => ApplySettings(_settingsService.Current, initialPlacement: true);

        _settingsService.SettingsChanged += (_, settings) => Dispatcher.Invoke(() => ApplySettings(settings, initialPlacement: false));

        _timerEngine.Tick += (_, _) => Dispatcher.Invoke(UpdateVisualState);
        _timerEngine.PhaseStarted += (_, phase) => Dispatcher.Invoke(() => OnPhaseStarted(phase));
        _timerEngine.PhaseCompleted += (_, phase) => Dispatcher.Invoke(() => OnPhaseCompleted(phase));

        UpdateVisualState();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // PRD 10.2: topmost, layered (per-pixel alpha via AllowsTransparency already gives
        // WS_EX_LAYERED), non-activating so the overlay never steals keyboard focus.
        NativeMethods.SetExStyle(_hwnd, NativeMethods.WS_EX_NOACTIVATE, true);
        NativeMethods.SetExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW, true);
        NativeMethods.SetExStyle(_hwnd, NativeMethods.WS_EX_TOPMOST, true);

        ApplyClickThrough(_settingsService.Current.ClickThrough);
    }

    // ----- Settings application (live, PRD 9.5) -----

    private void ApplySettings(AppSettings settings, bool initialPlacement)
    {
        _timerEngine.ApplySettings(settings);

        OverlayScaleTransform.ScaleX = settings.OverlayScale;
        OverlayScaleTransform.ScaleY = settings.OverlayScale;
        Opacity = settings.OverlayOpacity;

        SwitchPose(settings.Pose);
        _activeCharacter.Skin = settings.Skin;
        _activeCharacter.WhimsyEmbellishments = settings.WhimsyEmbellishments;

        if (_hwnd != IntPtr.Zero)
        {
            ApplyClickThrough(settings.ClickThrough && !_isPointerOver);
        }

        if (initialPlacement)
        {
            PlaceOnRememberedMonitor(settings);
        }

        UpdateVisualState();
    }

    private void ApplyClickThrough(bool enabled)
    {
        NativeMethods.SetExStyle(_hwnd, NativeMethods.WS_EX_TRANSPARENT, enabled);
    }

    /// <summary>
    /// Switches which character pose is shown (PRD-inspired "Could have":
    /// user-selectable alternate poses). Both controls stay in the visual
    /// tree; only the active one is visible and has its timers running.
    /// </summary>
    private void SwitchPose(CharacterPose pose)
    {
        var desired = pose == CharacterPose.SittingAtDesk ? (ICharacterView)SittingCharacter : StandingCharacter;
        if (ReferenceEquals(desired, _activeCharacter))
        {
            return;
        }

        _activeCharacter.SetActive(false);
        ((UIElement)_activeCharacter).Visibility = Visibility.Collapsed;

        _activeCharacter = desired;
        ((UIElement)_activeCharacter).Visibility = Visibility.Visible;
        _activeCharacter.SetActive(true);
    }

    // ----- Placement / multi-monitor (PRD 9.2) -----
    //
    // System.Windows.Forms.Screen reports monitor bounds in *physical* pixels,
    // while WPF's Window.Left/Top/ActualWidth are in *device-independent*
    // units (96 DPI). On any display scaled above 100% (125%/150% is the
    // default on most laptops) mixing the two without converting places the
    // window far outside the visible screen — it renders, just off in space.
    // All placement math below converts through the window's DPI transform.

    private Rect WorkAreaToDip(Screen screen)
    {
        var wa = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var m = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = m.Transform(new Point(wa.Left, wa.Top));
        var bottomRight = m.Transform(new Point(wa.Right, wa.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private System.Drawing.Point DipToPhysicalPoint(double x, double y)
    {
        var source = PresentationSource.FromVisual(this);
        var m = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var p = m.Transform(new Point(x, y));
        return new System.Drawing.Point((int)p.X, (int)p.Y);
    }

    private void PlaceOnRememberedMonitor(AppSettings settings)
    {
        var screen = Screen.AllScreens.FirstOrDefaultById(settings.OverlayMonitorId) ?? Screen.PrimaryScreen;
        if (screen == null)
        {
            return;
        }

        if (!double.IsNaN(settings.OverlayLeft) && !double.IsNaN(settings.OverlayTop))
        {
            Left = settings.OverlayLeft;
            Top = settings.OverlayTop;
            EnsureOnScreen(screen);
        }
        else
        {
            // Default: bottom-right corner of the primary display, inset (PRD 9.2).
            var margin = 24;
            var workArea = WorkAreaToDip(screen);
            Left = workArea.Right - ActualWidth - margin;
            Top = workArea.Bottom - ActualHeight - margin;
        }
    }

    private void EnsureOnScreen(Screen screen)
    {
        var wa = WorkAreaToDip(screen);
        if (Left < wa.Left || Left > wa.Right || Top < wa.Top || Top > wa.Bottom)
        {
            Left = wa.Right - ActualWidth - 24;
            Top = wa.Bottom - ActualHeight - 24;
        }
    }

    private void SaveCurrentPlacement()
    {
        var settings = _settingsService.Current.Clone();
        settings.OverlayLeft = Left;
        settings.OverlayTop = Top;
        var physicalCenter = DipToPhysicalPoint(Left + ActualWidth / 2, Top + ActualHeight / 2);
        var screen = Screen.FromPoint(physicalCenter);
        settings.OverlayMonitorId = screen.DeviceName;
        _settingsService.Save(settings);
    }

    // ----- Dragging (PRD 9.1) -----

    private void OnCharacterMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            try
            {
                DragMove();
                SaveCurrentPlacement();
            }
            catch (InvalidOperationException)
            {
                // DragMove throws if called outside a button-down; safe to ignore.
            }
        }
    }

    // ----- Hover control strip (PRD 9.1, 9.3) -----

    private void OnMouseEnterOverlay(object sender, MouseEventArgs e)
    {
        _isPointerOver = true;
        ControlStrip.Opacity = 1;
        ControlStrip.IsHitTestVisible = true;

        // Suspend click-through while the control strip is visible/hovered so
        // users can still reach it (PRD 9.3).
        if (_hwnd != IntPtr.Zero)
        {
            ApplyClickThrough(false);
        }
    }

    private void OnMouseLeaveOverlay(object sender, MouseEventArgs e)
    {
        _isPointerOver = false;
        ControlStrip.Opacity = 0;
        ControlStrip.IsHitTestVisible = false;

        if (_hwnd != IntPtr.Zero)
        {
            ApplyClickThrough(_settingsService.Current.ClickThrough);
        }
    }

    // ----- Primary controls (PRD 9.1.1) -----

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => _timerEngine.TogglePauseResume();

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        if (_timerEngine.Progress01 < 0.15)
        {
            var result = MessageBox.Show(this, "End this session early? It's just getting started.", "Finish session",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _timerEngine.Finish();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e) => _timerEngine.Skip();

    private void OnQuitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private SettingsWindow? _settingsWindow;

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settingsService, this);
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    // ----- Visual state sync -----

    private void OnPhaseStarted(Phase phase)
    {
        UpdateVisualState();
    }

    private void OnPhaseCompleted(Phase phase)
    {
        var settings = _settingsService.Current;
        if (settings.SoundEnabled)
        {
            if (phase == Phase.Focus)
            {
                _soundService.PlayFocusComplete();
            }
            else
            {
                _soundService.PlayBreakComplete();
            }
        }

        _historyService.RecordCompletedSession(phase, (int)Math.Round(_timerEngine.PhaseDurationSeconds / 60.0),
            finishedEarly: _timerEngine.Progress01 < 0.95);
    }

    private void UpdateVisualState()
    {
        var state = _timerEngine.GetEnergyState();
        _activeCharacter.ApplyState(_timerEngine.Energy, state);

        var isInviting = _timerEngine.Phase == Phase.Inviting;
        var pendingPhase = _timerEngine.PendingPhase;

        // Ring shows *remaining* time depleting (PRD 9.1.1), so it starts full
        // and shrinks toward empty as the session progresses.
        Ring.Progress = isInviting ? 0 : 1 - _timerEngine.Progress01;

        TimeText.Text = isInviting
            ? FormatMinutes(PendingPhaseMinutes(pendingPhase))
            : FormatTime(_timerEngine.RemainingSeconds);

        FinishButton.Visibility = isInviting ? Visibility.Collapsed : Visibility.Visible;

        // Not running - whether never started, a session just ended and is
        // queued waiting on the user (auto-start is disabled - nothing times
        // down until Start is pressed), or manually paused mid-session -
        // gets a wide labeled accent pill plus a gentle pulse so it's
        // impossible to miss. Actively running gets the plain, quiet pause icon.
        var needsAttention = _timerEngine.RunState != RunState.Running;
        if (needsAttention)
        {
            PlayPauseButton.Content = isInviting ? $"▶ {PendingPhaseLabel(pendingPhase)}" : "▶ Resume";
            PlayPauseButton.ToolTip = isInviting ? PendingPhaseLabel(pendingPhase) : "Resume";
            PlayPauseButton.Style = (Style)FindResource("WiltPrimaryPillButton");
        }
        else
        {
            PlayPauseButton.Content = "⏸";
            PlayPauseButton.ToolTip = "Pause";
            PlayPauseButton.Style = (Style)FindResource("WiltIconButton");
        }

        SetPlayPausePulse(needsAttention);
    }

    private int PendingPhaseMinutes(Phase? pendingPhase)
    {
        var settings = _settingsService.Current;
        return pendingPhase switch
        {
            Phase.ShortBreak => settings.ShortBreakMinutes,
            Phase.LongBreak => settings.LongBreakMinutes,
            _ => settings.FocusMinutes,
        };
    }

    private static string PendingPhaseLabel(Phase? pendingPhase) => pendingPhase switch
    {
        Phase.ShortBreak or Phase.LongBreak => "Start Break",
        Phase.Focus => "Start Focus",
        _ => "Start",
    };

    private bool _isPulseActive;

    private void SetPlayPausePulse(bool active)
    {
        if (active == _isPulseActive)
        {
            return;
        }

        _isPulseActive = active;

        if (active)
        {
            var pulse = new DoubleAnimation(1.0, 1.14, TimeSpan.FromMilliseconds(650))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            PlayPauseScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            PlayPauseScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }
        else
        {
            PlayPauseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PlayPauseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PlayPauseScale.ScaleX = 1;
            PlayPauseScale.ScaleY = 1;
        }
    }

    private static string FormatMinutes(int minutes) => FormatTime(minutes * 60.0);

    private static string FormatTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
    }
}

internal static class ScreenExtensions
{
    public static Screen? FirstOrDefaultById(this Screen[] screens, string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return null;
        }

        foreach (var s in screens)
        {
            if (s.DeviceName == deviceId)
            {
                return s;
            }
        }

        return null;
    }
}
