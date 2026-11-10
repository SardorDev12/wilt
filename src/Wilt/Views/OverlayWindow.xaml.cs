using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Wilt.Controls;
using Wilt.Models;
using Wilt.Native;
using Wilt.Services;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
    private readonly TaskEngine _taskEngine;
    private readonly SoundService _soundService = new();

    private IntPtr _hwnd;
    private ICharacterView _activeCharacter;
    private ClickThroughIndicatorWindow? _clickThroughIndicator;
    private bool _missedPanelOpen;

    // The currently-running task's row label, updated every tick (see
    // UpdateTaskClock) instead of waiting for the next full RenderTasks() -
    // TasksChanged only fires on explicit actions (start/pause/add/...), not
    // once a second, so the row's "worked/planned" minutes otherwise never
    // advanced while a task was actively running.
    private TextBlock? _activeTaskRowText;
    private WorkTask? _activeTaskRowTask;
    private int _activeTaskRowNumber;

    public OverlayWindow(TimerEngine timerEngine, SettingsService settingsService, HistoryService historyService,
        TaskEngine taskEngine)
    {
        InitializeComponent();
        _timerEngine = timerEngine;
        _settingsService = settingsService;
        _historyService = historyService;
        _taskEngine = taskEngine;

        _activeCharacter = StandingCharacter;
        _activeCharacter.SetActive(true);

        // Always shows the pause bars (it's only ever visible while a phase
        // is actively running - see UpdateVisualState), so it's set once here
        // rather than toggled per-tick like TaskPauseButton.
        PauseButton.Content = BuildPlayPauseIcon(showPause: true);

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => ApplySettings(_settingsService.Current, initialPlacement: true);
        LocationChanged += (_, _) => PositionClickThroughIndicator();
        SizeChanged += (_, _) => PositionClickThroughIndicator();

        _settingsService.SettingsChanged += (_, settings) => Dispatcher.Invoke(() => ApplySettings(settings, initialPlacement: false));

        _timerEngine.Tick += (_, _) => Dispatcher.Invoke(UpdateVisualState);
        _timerEngine.PhaseStarted += (_, phase) => Dispatcher.Invoke(() => OnPhaseStarted(phase));
        _timerEngine.PhaseCompleted += (_, phase) => Dispatcher.Invoke(() => OnPhaseCompleted(phase));

        _taskEngine.Tick += (_, _) => Dispatcher.Invoke(UpdateTaskClock);
        _taskEngine.TasksChanged += (_, _) => Dispatcher.Invoke(RenderTasks);

        UpdateVisualState();
        RenderTasks();
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
            ApplyClickThrough(settings.ClickThrough);
        }

        // Only the lock glyph itself changes with click-through state - every
        // other button keeps its place and its normal appearance. Drawn as a
        // small vector icon rather than an emoji character: the lock/unlock
        // and pause/play glyphs (U+1F512/1F513, U+23F8) render inconsistently
        // across Windows emoji font versions, which is what made the lock
        // icon appear to "duplicate" into the pause button's slot.
        ClickThroughButton.Content = BuildLockIcon(settings.ClickThrough);
        ClickThroughButton.ToolTip = settings.ClickThrough ? "Disable click-through" : "Enable click-through";
        TaskClickThroughButton.Content = BuildLockIcon(settings.ClickThrough);
        TaskClickThroughButton.ToolTip = ClickThroughButton.ToolTip;

        // Settle which view mode's layout is active *before* positioning the
        // click-through indicator against it - it picks the active mode's
        // own lock button, so it needs that mode's visibility already set.
        SwitchViewMode(settings.ViewMode);
        SetClickThroughIndicatorVisible(settings.ClickThrough);

        if (initialPlacement)
        {
            PlaceOnRememberedMonitor(settings);
        }

        UpdateVisualState();
    }

    // ----- Task Mode (view-mode switch lives in Settings; PRD-inspired decisions:
    // default view is Tasks, switching to Tasks pauses any running Pomodoro
    // session, and the character never appears in Task Mode) -----

    private void SwitchViewMode(ViewMode mode)
    {
        var isTasks = mode == ViewMode.Tasks;

        // AwaitingAcknowledgment also reports RunState.Running (the clock
        // keeps counting into overtime) - but TogglePauseResume() would
        // treat that as acknowledging and silently start the next phase,
        // which switching view modes should never do.
        if (isTasks && _timerEngine.RunState == RunState.Running && !_timerEngine.AwaitingAcknowledgment)
        {
            _timerEngine.TogglePauseResume();
        }

        PomodoroModeHost.Visibility = isTasks ? Visibility.Collapsed : Visibility.Visible;
        TaskModeHost.Visibility = isTasks ? Visibility.Visible : Visibility.Collapsed;

        // Task Mode folds settings/click-through/quit into its own always-visible
        // top bar (one row, no hover needed) instead of the Pomodoro hover strip.
        ControlStrip.Visibility = isTasks ? Visibility.Collapsed : Visibility.Visible;

        if (isTasks)
        {
            RenderTasks();
        }
    }

    private void ApplyClickThrough(bool enabled)
    {
        NativeMethods.SetExStyle(_hwnd, NativeMethods.WS_EX_TRANSPARENT, enabled);
    }

    // ----- Click-through toggle (PRD 9.3) -----
    //
    // WS_EX_TRANSPARENT makes a window invisible to mouse input at the Win32
    // level for its entire client area - there's no way to exempt one button
    // inside it. So once click-through is on, nothing in this window
    // (including this same control strip) can be clicked, hover included.
    // ClickThroughIndicatorWindow is a separate small window, positioned
    // over where the control strip sits, that's never made click-through
    // itself - the one thing guaranteed to still work, so the user is never
    // stuck with an unreachable overlay.

    private void OnClickThroughToggleClick(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Current.Clone();
        settings.ClickThrough = !settings.ClickThrough;
        _settingsService.Save(settings);
    }

    private void SetClickThroughIndicatorVisible(bool visible)
    {
        if (visible)
        {
            if (_clickThroughIndicator == null)
            {
                _clickThroughIndicator = new ClickThroughIndicatorWindow(_settingsService);
                // Its own size isn't known until first layout; reposition once it is.
                _clickThroughIndicator.SizeChanged += (_, _) => PositionClickThroughIndicator();
            }

            _clickThroughIndicator.Show();
            PositionClickThroughIndicator();
        }
        else
        {
            _clickThroughIndicator?.Hide();
        }
    }

    private void PositionClickThroughIndicator()
    {
        if (_clickThroughIndicator is not { IsVisible: true })
        {
            return;
        }

        // Center the indicator exactly over whichever button is actually the
        // click-through toggle for the current view mode, rather than
        // assuming it sits at the window's horizontal center - that only
        // held for Pomodoro's old, symmetric control strip. Task Mode's top
        // bar leads with a wide clock label, which shifts its icon row well
        // off the window's center, and the indicator was landing on the
        // nearest other icon (the pause/start button) instead.
        var activeButton = TaskModeHost.Visibility == Visibility.Visible ? (UIElement)TaskClickThroughButton : ClickThroughButton;

        var topLeftPhysical = activeButton.PointToScreen(new Point(0, 0));
        var source = PresentationSource.FromVisual(this);
        var m = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeftDip = m.Transform(topLeftPhysical);

        var centerX = topLeftDip.X + activeButton.RenderSize.Width / 2;
        var centerY = topLeftDip.Y + activeButton.RenderSize.Height / 2;

        _clickThroughIndicator.Left = centerX - _clickThroughIndicator.ActualWidth / 2;
        _clickThroughIndicator.Top = centerY - _clickThroughIndicator.ActualHeight / 2;
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
    //
    // Shared by the Pomodoro character and, since there's no character to
    // grab in Task Mode, the Task Mode top bar and task-list capsule -
    // whichever empty (non-button) area the user clicks.

    private void OnDragHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

    // ----- Hover control strip (PRD 9.1) -----
    //
    // These only fire while this window isn't click-through - once it is,
    // the OS stops delivering mouse input to it entirely (see the
    // click-through section below), so there's nothing to suspend/restore
    // here anymore; ClickThroughIndicatorWindow handles that case instead.

    private void OnMouseEnterOverlay(object sender, MouseEventArgs e)
    {
        ControlStrip.Opacity = 1;
        ControlStrip.IsHitTestVisible = true;
    }

    private void OnMouseLeaveOverlay(object sender, MouseEventArgs e)
    {
        ControlStrip.Opacity = 0;
        ControlStrip.IsHitTestVisible = false;
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

    // ----- Task Mode controls -----

    private void OnTaskNextClick(object sender, RoutedEventArgs e) => _taskEngine.Next();

    private void OnTaskPauseClick(object sender, RoutedEventArgs e)
    {
        if (_taskEngine.IsActiveRunning)
        {
            _taskEngine.PauseActive();
        }
        else if (_taskEngine.ActiveTaskId.HasValue)
        {
            _taskEngine.StartTask(_taskEngine.ActiveTaskId.Value);
        }
    }

    private void OnMissedBadgeClick(object sender, RoutedEventArgs e)
    {
        _missedPanelOpen = !_missedPanelOpen;
        RenderTasks();
    }

    // Stops the click from bubbling up to the Tasks card's own
    // MouseLeftButtonDown drag handler, which otherwise calls DragMove()
    // and swallows the matching MouseUp before OnAddTaskRowClick ever sees
    // it - the cause of "+ Add task" needing several clicks to register.
    private void OnInteractiveRowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void OnAddTaskRowClick(object sender, MouseButtonEventArgs e)
    {
        AddTaskRow.Visibility = Visibility.Collapsed;
        AddTaskForm.Visibility = Visibility.Visible;

        // The overlay is WS_EX_NOACTIVATE (PRD 10.2: never steals keyboard
        // focus from whatever app you're using) - great for a passive
        // character, but it means the window can never actually become the
        // focused one, so typed keystrokes kept going to whatever app was
        // last active instead of this textbox. Temporarily allow activation
        // just while the form is open, and explicitly take focus.
        SetTextInputCapable(true);
        Activate();
        NewTaskNameBox.Focus();
        Keyboard.Focus(NewTaskNameBox);
    }

    private void CloseAddTaskForm()
    {
        AddTaskForm.Visibility = Visibility.Collapsed;
        AddTaskRow.Visibility = Visibility.Visible;
        NewTaskNameBox.Text = "";
        NewTaskMinutesBox.Text = "";
        SetTextInputCapable(false);
    }

    /// <summary>Toggles WS_EX_NOACTIVATE off/on around text entry - see OnAddTaskRowClick.</summary>
    private void SetTextInputCapable(bool enabled)
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.SetExStyle(_hwnd, NativeMethods.WS_EX_NOACTIVATE, !enabled);
        }
    }

    private void OnCancelAddTaskClick(object sender, RoutedEventArgs e) => CloseAddTaskForm();

    private void OnAddTaskFieldKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnConfirmAddTaskClick(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            CloseAddTaskForm();
        }
    }

    private void OnMinutesPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void OnMinutesPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Paste &&
            (!System.Windows.Clipboard.ContainsText() || !System.Windows.Clipboard.GetText().All(char.IsDigit)))
        {
            e.Handled = true;
        }
    }

    private void OnRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (AddTaskForm.Visibility != Visibility.Visible)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && !AddTaskForm.IsAncestorOf(source))
        {
            CloseAddTaskForm();
        }
    }

    private void OnConfirmAddTaskClick(object sender, RoutedEventArgs e)
    {
        var name = NewTaskNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || !int.TryParse(NewTaskMinutesBox.Text, out var minutes) || minutes <= 0)
        {
            return;
        }

        _taskEngine.AddTask(name, minutes);
        CloseAddTaskForm();
    }

    private static string FormatTaskLabel(WorkTask task, int number) =>
        $"{number}. {task.Name} — {(int)(task.WorkedSeconds / 60)}/{task.PlannedMinutes}";

    private void UpdateTaskClock()
    {
        var active = _taskEngine.ActiveTask;
        TaskClockText.Text = active != null ? FormatTime(active.WorkedSeconds) : "00:00";
        TaskPauseButton.Content = BuildPlayPauseIcon(_taskEngine.IsActiveRunning);
        TaskPauseButton.ToolTip = _taskEngine.IsActiveRunning ? "Pause" : "Start";
        TaskPauseButton.IsEnabled = active != null;

        // The row list itself only rebuilds on TasksChanged (an explicit
        // action), not once a second, so the active row's own "worked/
        // planned" label is refreshed here instead - otherwise it looked
        // frozen while a task was actively running.
        if (_activeTaskRowText != null && _activeTaskRowTask != null && _taskEngine.ActiveTaskId == _activeTaskRowTask.Id)
        {
            _activeTaskRowText.Text = FormatTaskLabel(_activeTaskRowTask, _activeTaskRowNumber);
        }
    }

    private void RenderTasks()
    {
        _activeTaskRowText = null;
        _activeTaskRowTask = null;

        UpdateTaskClock();

        var today = _taskEngine.TodayTasks;
        var missed = _taskEngine.MissedTasks;

        NoTasksText.Visibility = today.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TaskListScroll.Visibility = today.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        TaskRowsPanel.Children.Clear();
        for (var i = 0; i < today.Count; i++)
        {
            TaskRowsPanel.Children.Add(BuildTaskRow(today[i], i + 1));
        }

        MissedBadgeHost.Visibility = missed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        MissedCountText.Text = missed.Count.ToString();

        MissedPanel.Visibility = _missedPanelOpen && missed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        MissedRowsPanel.Children.Clear();
        foreach (var task in missed)
        {
            MissedRowsPanel.Children.Add(BuildMissedRow(task));
        }
    }

    private UIElement BuildTaskRow(WorkTask task, int number)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var isActive = _taskEngine.ActiveTaskId == task.Id;
        var textBrush = (Brush)FindResource(task.IsDone ? "WiltTextDimBrush" : "WiltTextBrush");

        var nameText = new TextBlock
        {
            Text = FormatTaskLabel(task, number),
            Foreground = textBrush,
            FontSize = 12,
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (task.IsDone)
        {
            nameText.TextDecorations = TextDecorations.Strikethrough;
        }

        if (isActive)
        {
            _activeTaskRowText = nameText;
            _activeTaskRowTask = task;
            _activeTaskRowNumber = number;
        }

        Grid.SetColumn(nameText, 0);
        Grid.SetColumnSpan(nameText, 2);
        grid.Children.Add(nameText);

        var startPauseButton = new Button
        {
            Content = isActive && _taskEngine.IsActiveRunning ? "⏸" : "▶",
            Style = (Style)FindResource("WiltIconButton"),
            ToolTip = isActive && _taskEngine.IsActiveRunning ? "Pause" : "Start",
            IsEnabled = !task.IsDone,
            Margin = new Thickness(4, 0, 0, 0),
        };
        startPauseButton.Click += (_, _) =>
        {
            if (isActive && _taskEngine.IsActiveRunning)
            {
                _taskEngine.PauseActive();
            }
            else
            {
                _taskEngine.StartTask(task.Id);
            }
        };
        Grid.SetColumn(startPauseButton, 2);
        grid.Children.Add(startPauseButton);

        var finishButton = new Button
        {
            Content = "⏹",
            Style = (Style)FindResource("WiltIconButton"),
            ToolTip = "Finish",
            IsEnabled = !task.IsDone,
            Margin = new Thickness(4, 0, 0, 0),
        };
        finishButton.Click += (_, _) => _taskEngine.FinishTask(task.Id);
        Grid.SetColumn(finishButton, 3);
        grid.Children.Add(finishButton);

        var doneCheck = new CheckBox
        {
            IsChecked = task.IsDone,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };
        doneCheck.Checked += (_, _) => _taskEngine.ToggleDone(task.Id);
        doneCheck.Unchecked += (_, _) => _taskEngine.ToggleDone(task.Id);
        Grid.SetColumn(doneCheck, 4);
        grid.Children.Add(doneCheck);

        var row = new Border
        {
            Background = (Brush)FindResource(isActive ? "WiltAccentDimBrush" : "WiltRowChipBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 6, 6),
            Margin = new Thickness(0, 0, 0, 6),
            Child = grid,
        };
        row.MouseLeftButtonDown += OnInteractiveRowMouseLeftButtonDown;
        return row;
    }

    private UIElement BuildMissedRow(WorkTask task)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameText = new TextBlock
        {
            Text = $"{task.Name} ({task.Date:MMM d})",
            Foreground = (Brush)FindResource("WiltTextBrush"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(nameText, 0);
        grid.Children.Add(nameText);

        var moveButton = new Button
        {
            Content = "↩",
            Style = (Style)FindResource("WiltIconButton"),
            ToolTip = "Move to today",
            Margin = new Thickness(4, 0, 0, 0),
        };
        moveButton.Click += (_, _) => _taskEngine.MoveMissedToToday(task.Id);
        Grid.SetColumn(moveButton, 1);
        grid.Children.Add(moveButton);

        var dismissButton = new Button
        {
            Content = "✕",
            Style = (Style)FindResource("WiltIconButton"),
            ToolTip = "Dismiss",
            Margin = new Thickness(4, 0, 0, 0),
        };
        dismissButton.Click += (_, _) => _taskEngine.DismissMissed(task.Id);
        Grid.SetColumn(dismissButton, 2);
        grid.Children.Add(dismissButton);

        var row = new Border
        {
            Background = (Brush)FindResource("WiltRowChipBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 6, 6),
            Margin = new Thickness(0, 0, 0, 6),
            Child = grid,
        };
        row.MouseLeftButtonDown += OnInteractiveRowMouseLeftButtonDown;
        return row;
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
        var awaitingAck = _timerEngine.AwaitingAcknowledgment;
        var pendingPhase = _timerEngine.PendingPhase;

        // Ring shows *remaining* time depleting (PRD 9.1.1), so it starts full
        // and shrinks toward empty as the session progresses. Once a phase
        // runs into overtime there's nothing left to deplete - keep it empty.
        Ring.Progress = isInviting || awaitingAck ? 0 : 1 - _timerEngine.Progress01;

        SessionsCounterText.Text = $"{_timerEngine.SessionsCompletedSinceLongBreak}/{_settingsService.Current.SessionsUntilLongBreak}";

        TimeText.Text = isInviting
            ? FormatMinutes(PendingPhaseMinutes(pendingPhase))
            : awaitingAck
                ? "+" + FormatTime(_timerEngine.OvertimeSeconds)
                : FormatTime(_timerEngine.RemainingSeconds);

        CompletionBanner.Visibility = awaitingAck ? Visibility.Visible : Visibility.Collapsed;
        if (awaitingAck)
        {
            CompletionBannerText.Text = _timerEngine.Phase == Phase.Focus
                ? "Focus session complete!"
                : "Break complete!";
        }

        // While click-through is on, this window can't be clicked at all (see
        // the click-through section below) - showing Finish/Pause as if they
        // were still usable would be misleading, so hide them entirely.
        var clickThrough = _settingsService.Current.ClickThrough;
        FinishButton.Visibility = !clickThrough && !isInviting ? Visibility.Visible : Visibility.Collapsed;

        // Not running, or a phase just ran out and is awaiting acknowledgment
        // (the clock is still counting overtime, but nothing new starts
        // without an explicit click) - shows the wide labeled accent pill
        // over the character. Actively running a phase shows just the plain,
        // quiet pause icon in the clock row instead.
        var needsAttention = _timerEngine.RunState != RunState.Running || awaitingAck;
        BigStartButton.Visibility = needsAttention ? Visibility.Visible : Visibility.Collapsed;
        BigStartButton.Content = isInviting || awaitingAck ? $"▶ {PendingPhaseLabel(pendingPhase)}" : "▶ Resume";
        BigStartButton.ToolTip = isInviting || awaitingAck ? PendingPhaseLabel(pendingPhase) : "Resume";

        PauseButton.Visibility = !clickThrough && !needsAttention ? Visibility.Visible : Visibility.Collapsed;
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

    private static string FormatMinutes(int minutes) => FormatTime(minutes * 60.0);

    private static string FormatTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
    }

    // ----- Small vector icons -----
    //
    // Hand-drawn instead of emoji characters (▶/⏸, 🔒/🔓): those specific
    // codepoints render inconsistently across Windows' installed emoji font
    // version, which is what made the lock icon appear to show up a second
    // time in the pause button's slot. A Path/Rectangle built from plain
    // lines renders identically everywhere. Each call returns a fresh
    // element - a UIElement can only ever be the content of one Button.

    private static UIElement BuildPlayPauseIcon(bool showPause)
    {
        var geometry = Geometry.Parse(showPause
            ? "M0,0 H3 V12 H0 Z M7,0 H10 V12 H7 Z" // two bars
            : "M0,0 L10,6 L0,12 Z"); // triangle
        var path = new Path
        {
            Data = geometry,
            Fill = (Brush)Application.Current.Resources["WiltTextBrush"],
        };
        return new Viewbox { Width = 11, Height = 12, Child = path };
    }

    private static UIElement BuildLockIcon(bool unlocked)
    {
        // A single filled Path, same proven approach as BuildPlayPauseIcon -
        // the previous version's shackle was a Stroke-only Path, which never
        // actually rendered, leaving just the body rectangle (reading as a
        // plain floppy-disk-like square that looked identical either way).
        // Left leg + top bar stay fixed; only the right leg moves - down to
        // meet the body when locked, lifted clear of it when unlocked.
        var rightLeg = unlocked ? "M7.8,0.5 H9 V3.7 H7.8 Z" : "M7.8,2 H9 V5.5 H7.8 Z";
        var data =
            "M3,2 H4.2 V5.5 H3 Z " +   // left leg of the shackle
            "M3,2 H9 V3.2 H3 Z " +     // top bar of the shackle
            rightLeg +                 // right leg - the part that moves
            "M2,5 H10 V11 H2 Z";       // body

        var path = new Path
        {
            Data = Geometry.Parse(data),
            Fill = (Brush)Application.Current.Resources["WiltTextBrush"],
        };
        return new Viewbox { Width = 12, Height = 12, Child = path };
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
