using System;
using System.Text.Json.Serialization;

namespace Wilt.Models;

/// <summary>A single item in the daily work plan (Task Mode).</summary>
public class WorkTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int PlannedMinutes { get; set; }
    public double WorkedSeconds { get; set; }
    public bool IsDone { get; set; }

    /// <summary>The day (date-only, local) this task belongs to - drives the "today only, otherwise missed" split.</summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>Read-only convenience for display grids (e.g. the Task Manager window) - not persisted.</summary>
    [JsonIgnore]
    public int WorkedMinutes => (int)(WorkedSeconds / 60);
}
