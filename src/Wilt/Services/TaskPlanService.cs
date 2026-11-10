using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Wilt.Models;

namespace Wilt.Services;

/// <summary>
/// Loads/saves the daily work plan (Task Mode) as local JSON
/// (%AppData%\Wilt\tasks.json), mirroring SettingsService/HistoryService.
/// </summary>
public class TaskPlanService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private List<WorkTask> _tasks;

    public TaskPlanService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wilt");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "tasks.json");
        _tasks = LoadFromDisk();
    }

    public List<WorkTask> Load() => _tasks;

    private List<WorkTask> LoadFromDisk()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<List<WorkTask>>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // Corrupt/unreadable tasks file: start fresh rather than crashing.
        }

        return new List<WorkTask>();
    }

    public void Save(List<WorkTask> tasks)
    {
        _tasks = tasks;
        try
        {
            var json = JsonSerializer.Serialize(tasks, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception)
        {
            // Best-effort persistence.
        }
    }
}
