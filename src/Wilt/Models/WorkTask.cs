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

    /// <summary>
    /// The first day (date-only, local) this task is due to appear in the
    /// daily plan. Kept named "Date" (not "StartDate") so existing
    /// tasks.json files on disk still deserialize into it unchanged -
    /// TaskPlanService fills in EndDate for anything saved before it existed.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>
    /// The last day this task appears in the daily plan - equal to Date for
    /// an ordinary single-day task, later for a multi-day/"weekly" one
    /// planned across a date range. The task shows up in every day's list
    /// from Date through EndDate inclusive, and counts as missed only once
    /// EndDate has passed without being marked done.
    /// </summary>
    public DateTime EndDate { get; set; } = DateTime.MinValue;

    /// <summary>Read-only convenience for display grids (e.g. the Task Manager window) - not persisted.</summary>
    [JsonIgnore]
    public int WorkedMinutes => (int)(WorkedSeconds / 60);
}
