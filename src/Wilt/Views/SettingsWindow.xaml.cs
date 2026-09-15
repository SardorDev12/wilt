using System;
using System.Windows;
using System.Windows.Controls;
using Wilt.Models;
using Wilt.Services;

namespace Wilt.Views;

/// <summary>
/// Settings window (PRD 8, 9.5). Every control applies its change live via
/// SettingsService.Save, which raises SettingsChanged and is picked up
/// immediately by the overlay/timer engine — nothing here requires an app
/// or session restart, except launch-on-startup which is called out to the
/// user as effectively instant to the registry but not to the currently
/// running instance's own startup behavior.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly HistoryService _historyService = new();
    private bool _isInitializing = true;

    public SettingsWindow(SettingsService settingsService, OverlayWindow overlayWindow)
    {
        InitializeComponent();
        _settingsService = settingsService;
        Owner = overlayWindow.IsLoaded ? overlayWindow : null;

        LoadFromSettings(_settingsService.Current);
        RefreshStats();
        _isInitializing = false;
    }

    private void LoadFromSettings(AppSettings s)
    {
        FocusMinutesBox.Text = s.FocusMinutes.ToString();
        ShortBreakMinutesBox.Text = s.ShortBreakMinutes.ToString();
        LongBreakMinutesBox.Text = s.LongBreakMinutes.ToString();
        SessionsUntilLongBreakBox.Text = s.SessionsUntilLongBreak.ToString();

        OverlaySizeSlider.Value = s.OverlayScale;
        OverlayOpacitySlider.Value = s.OverlayOpacity;
        ClickThroughCheckBox.IsChecked = s.ClickThrough;
        WhimsyCheckBox.IsChecked = s.WhimsyEmbellishments;
        SoundEnabledCheckBox.IsChecked = s.SoundEnabled;
        LaunchOnStartupCheckBox.IsChecked = s.LaunchOnStartup;
        GlobalHotkeysCheckBox.IsChecked = s.GlobalHotkeysEnabled;

        foreach (ComboBoxItem item in SkinComboBox.Items)
        {
            if ((string)item.Tag == s.Skin.ToString())
            {
                SkinComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void RefreshStats()
    {
        StatsText.Text = $"Focus sessions — today: {_historyService.CountFocusSessionsToday()}, " +
                          $"this week: {_historyService.CountFocusSessionsThisWeek()}, " +
                          $"all time: {_historyService.CountFocusSessionsTotal()}";
    }

    private AppSettings CurrentDraft() => _settingsService.Current.Clone();

    private void OnTimerFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        if (!TryParsePositiveInt(FocusMinutesBox.Text, out var focus) ||
            !TryParsePositiveInt(ShortBreakMinutesBox.Text, out var shortBreak) ||
            !TryParsePositiveInt(LongBreakMinutesBox.Text, out var longBreak) ||
            !TryParsePositiveInt(SessionsUntilLongBreakBox.Text, out var sessions))
        {
            return; // Wait for a valid value before applying (PRD 9.5 rescales cleanly, so don't apply mid-typing garbage).
        }

        var s = CurrentDraft();
        s.FocusMinutes = focus;
        s.ShortBreakMinutes = shortBreak;
        s.LongBreakMinutes = longBreak;
        s.SessionsUntilLongBreak = sessions;
        _settingsService.Save(s);
    }

    private static bool TryParsePositiveInt(string text, out int value)
    {
        return int.TryParse(text, out value) && value > 0 && value <= 1440;
    }

    private void OnOverlaySizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing)
        {
            return;
        }

        var s = CurrentDraft();
        s.OverlayScale = OverlaySizeSlider.Value;
        _settingsService.Save(s);
    }

    private void OnOverlayOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing)
        {
            return;
        }

        var s = CurrentDraft();
        s.OverlayOpacity = OverlayOpacitySlider.Value;
        _settingsService.Save(s);
    }

    private void OnSkinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || SkinComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var s = CurrentDraft();
        s.Skin = Enum.Parse<CharacterSkin>((string)item.Tag);
        _settingsService.Save(s);
    }

    private void OnBehaviorChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        var s = CurrentDraft();
        s.ClickThrough = ClickThroughCheckBox.IsChecked == true;
        s.WhimsyEmbellishments = WhimsyCheckBox.IsChecked == true;
        s.SoundEnabled = SoundEnabledCheckBox.IsChecked == true;
        s.GlobalHotkeysEnabled = GlobalHotkeysCheckBox.IsChecked == true;
        _settingsService.Save(s);
    }

    private void OnLaunchOnStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        var enabled = LaunchOnStartupCheckBox.IsChecked == true;
        var s = CurrentDraft();
        s.LaunchOnStartup = enabled;
        _settingsService.Save(s);
        StartupService.Apply(enabled);
    }
}
