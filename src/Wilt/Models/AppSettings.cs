namespace Wilt.Models;

public enum CharacterSkin
{
    Slate,
    Amber,
    Mint,
}

/// <summary>Which character rig the overlay displays.</summary>
public enum CharacterPose
{
    /// <summary>Standing, full-body illustration (<see cref="Wilt.Controls.CharacterControl"/>).</summary>
    Standing,

    /// <summary>Seated at a desk, working (<see cref="Wilt.Controls.DeskCharacterControl"/>).</summary>
    SittingAtDesk,
}

/// <summary>User-configurable settings, persisted as JSON in %AppData%\Wilt\settings.json.</summary>
public class AppSettings
{
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsUntilLongBreak { get; set; } = 4;

    public double OverlayScale { get; set; } = 1.0; // 0.2 .. 8.0, base ~150px tall at 1.0
    public double OverlayOpacity { get; set; } = 1.0; // 0.2 .. 1.0
    public bool ClickThrough { get; set; } = false;
    public bool SoundEnabled { get; set; } = true;
    public bool LaunchOnStartup { get; set; } = false;
    public CharacterSkin Skin { get; set; } = CharacterSkin.Slate;
    public CharacterPose Pose { get; set; } = CharacterPose.Standing;

    public bool WhimsyEmbellishments { get; set; } = false;

    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public string? OverlayMonitorId { get; set; }

    public string GlobalHotkeyStartPause { get; set; } = "Ctrl+Alt+P";
    public string GlobalHotkeySkip { get; set; } = "Ctrl+Alt+S";
    public bool GlobalHotkeysEnabled { get; set; } = true;

    /// <summary>Auto-pause a running session after no keyboard/mouse input system-wide for IdleTimeoutMinutes.</summary>
    public bool IdleDetectionEnabled { get; set; } = true;
    public int IdleTimeoutMinutes { get; set; } = 5;

    public AppSettings Clone()
    {
        return (AppSettings)MemberwiseClone();
    }
}
