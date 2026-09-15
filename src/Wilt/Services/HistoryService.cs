using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wilt.Models;

namespace Wilt.Services;

/// <summary>
/// Local-only session history (PRD "Should have": stats panel). Stored as
/// JSON in %AppData%\Wilt\history.json, no accounts/cloud sync.
/// </summary>
public class HistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private HistoryData _data;

    public HistoryService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wilt");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "history.json");
        _data = LoadFromDisk();
    }

    private HistoryData LoadFromDisk()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<HistoryData>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // Corrupt/unreadable history file: start fresh rather than crashing.
        }

        return new HistoryData();
    }

    public void RecordCompletedSession(Phase phase, int durationMinutes, bool finishedEarly)
    {
        _data.Sessions.Add(new CompletedSession
        {
            CompletedAtUtc = DateTime.UtcNow,
            Phase = phase,
            DurationMinutes = durationMinutes,
            FinishedEarly = finishedEarly,
        });

        // Keep the file from growing unbounded; ~1 year of heavy use.
        if (_data.Sessions.Count > 20000)
        {
            _data.Sessions = _data.Sessions.Skip(_data.Sessions.Count - 20000).ToList();
        }

        Save();
    }

    public int CountFocusSessionsToday()
    {
        var today = DateTime.UtcNow.Date;
        return _data.Sessions.Count(s => s.Phase == Phase.Focus && !s.FinishedEarly && s.CompletedAtUtc.Date == today);
    }

    public int CountFocusSessionsThisWeek()
    {
        var startOfWeek = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        return _data.Sessions.Count(s => s.Phase == Phase.Focus && !s.FinishedEarly && s.CompletedAtUtc.Date >= startOfWeek);
    }

    public int CountFocusSessionsTotal() => _data.Sessions.Count(s => s.Phase == Phase.Focus && !s.FinishedEarly);

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception)
        {
            // Best-effort persistence.
        }
    }
}
