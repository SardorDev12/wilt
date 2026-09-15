using System;
using System.IO;
using System.Text.Json;
using Wilt.Models;

namespace Wilt.Services;

/// <summary>
/// Loads/saves AppSettings as local JSON (%AppData%\Wilt\settings.json), no
/// accounts/cloud sync per PRD 10.5. Raises SettingsChanged so every live
/// consumer (overlay, timer engine, tray, hotkeys) can apply changes
/// immediately per PRD 9.5.
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private AppSettings _current;

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wilt");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        _current = LoadFromDisk();
    }

    public AppSettings Current => _current;

    public AppSettings Load() => _current;

    private AppSettings LoadFromDisk()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // Corrupt/unreadable settings file: fall back to defaults rather than crashing.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        _current = settings;
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception)
        {
            // Best-effort persistence; app keeps working in-memory even if disk write fails.
        }

        SettingsChanged?.Invoke(this, settings);
    }
}
