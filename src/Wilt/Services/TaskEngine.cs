using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Wilt.Models;

namespace Wilt.Services;

/// <summary>
/// Runtime state for Task Mode: today's work plan, the active task's
/// stopwatch, and the missed-tasks (undone, from a previous day) list.
/// Today-vs-missed is a pure live date comparison against DateTime.Today -
/// no explicit daily-rollover/migration step is needed.
/// </summary>
public class TaskEngine
{
    private readonly TaskPlanService _planService;
    private readonly DispatcherTimer _timer;
    private List<WorkTask> _tasks;

    public Guid? ActiveTaskId { get; private set; }
    public bool IsActiveRunning { get; private set; }

    public event EventHandler? Tick;
    public event EventHandler? TasksChanged;

    public TaskEngine(TaskPlanService planService)
    {
        _planService = planService;
        _tasks = _planService.Load();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>
    /// Today's plan: every task whose [Date, EndDate] range includes today -
    /// a multi-day/"weekly" task (planned across a date range in the Task
    /// Manager window) reappears here on each of its days, not just once.
    /// </summary>
    public IReadOnlyList<WorkTask> TodayTasks =>
        _tasks.Where(t => t.Date.Date <= DateTime.Today && t.EndDate.Date >= DateTime.Today).ToList();

    /// <summary>A task counts as missed only once its *whole* range has passed undone.</summary>
    public IReadOnlyList<WorkTask> MissedTasks =>
        _tasks.Where(t => t.EndDate.Date < DateTime.Today && !t.IsDone).ToList();

    /// <summary>
    /// Every task whose range overlaps the current week (Monday-Sunday
    /// containing today) - a plan-ahead view alongside the actionable
    /// Today list, oldest start date first.
    /// </summary>
    public IReadOnlyList<WorkTask> ThisWeekTasks
    {
        get
        {
            var (weekStart, weekEnd) = CurrentWeekRange();
            return _tasks.Where(t => t.Date.Date <= weekEnd && t.EndDate.Date >= weekStart)
                .OrderBy(t => t.Date)
                .ToList();
        }
    }

    private static (DateTime Start, DateTime End) CurrentWeekRange()
    {
        var today = DateTime.Today;
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var start = today.AddDays(-daysSinceMonday);
        return (start, start.AddDays(6));
    }

    public WorkTask? ActiveTask => ActiveTaskId.HasValue ? _tasks.FirstOrDefault(t => t.Id == ActiveTaskId.Value) : null;

    /// <summary>Every task regardless of date, newest first - for the Task Manager window (Settings).</summary>
    public IReadOnlyList<WorkTask> AllTasks => _tasks.OrderByDescending(t => t.Date).ToList();

    public WorkTask AddTask(string name, int plannedMinutes) => AddTask(name, plannedMinutes, DateTime.Today, DateTime.Today);

    /// <summary>A single-day task - shorthand for AddTask with Date == EndDate.</summary>
    public WorkTask AddTask(string name, int plannedMinutes, DateTime date) => AddTask(name, plannedMinutes, date, date);

    /// <summary>A task planned across a date range (the Task Manager window's "weekly" planning) - appears in every day's list from startDate through endDate.</summary>
    public WorkTask AddTask(string name, int plannedMinutes, DateTime startDate, DateTime endDate)
    {
        var task = new WorkTask
        {
            Name = name,
            PlannedMinutes = plannedMinutes,
            Date = startDate.Date,
            EndDate = endDate.Date < startDate.Date ? startDate.Date : endDate.Date,
        };
        _tasks.Add(task);
        Save();
        return task;
    }

    /// <summary>Full edit of a task's fields (Task Manager window) - distinct from the in-flow ToggleDone/MoveMissedToToday actions.</summary>
    public void UpdateTask(Guid id, string name, int plannedMinutes, DateTime startDate, DateTime endDate, bool isDone)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            return;
        }

        task.Name = name;
        task.PlannedMinutes = plannedMinutes;
        task.Date = startDate.Date;
        task.EndDate = endDate.Date < startDate.Date ? startDate.Date : endDate.Date;
        task.IsDone = isDone;

        if (isDone && ActiveTaskId == id)
        {
            ActiveTaskId = null;
            IsActiveRunning = false;
        }

        Save();
    }

    /// <summary>Removes a task outright, regardless of date - the Task Manager window's Delete.</summary>
    public void DeleteTask(Guid id)
    {
        if (ActiveTaskId == id)
        {
            ActiveTaskId = null;
            IsActiveRunning = false;
        }

        _tasks.RemoveAll(t => t.Id == id);
        Save();
    }

    public void StartTask(Guid id)
    {
        if (ActiveTaskId.HasValue && ActiveTaskId.Value != id)
        {
            PauseActive();
        }

        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null || task.IsDone)
        {
            return;
        }

        ActiveTaskId = id;
        IsActiveRunning = true;
        Save();
    }

    public void PauseActive()
    {
        if (!ActiveTaskId.HasValue)
        {
            return;
        }

        IsActiveRunning = false;
        Save();
    }

    public void FinishTask(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            return;
        }

        if (ActiveTaskId == id)
        {
            ActiveTaskId = null;
            IsActiveRunning = false;
        }

        task.IsDone = true;
        Save();
    }

    public void ToggleDone(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            return;
        }

        task.IsDone = !task.IsDone;
        if (task.IsDone && ActiveTaskId == id)
        {
            ActiveTaskId = null;
            IsActiveRunning = false;
        }

        Save();
    }

    public void Next()
    {
        var today = TodayTasks;
        var currentIndex = ActiveTaskId.HasValue ? today.ToList().FindIndex(t => t.Id == ActiveTaskId.Value) : -1;

        PauseActive();
        ActiveTaskId = null;

        var next = today.Skip(currentIndex + 1).FirstOrDefault(t => !t.IsDone)
                   ?? today.FirstOrDefault(t => !t.IsDone);

        if (next != null)
        {
            StartTask(next.Id);
        }
        else
        {
            Save();
        }
    }

    public void MoveMissedToToday(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            return;
        }

        // Collapses a passed multi-day range down to a single day (today) -
        // re-extending it to a new range is a job for the Task Manager window.
        task.Date = DateTime.Today;
        task.EndDate = DateTime.Today;
        Save();
    }

    public void DismissMissed(Guid id) => DeleteTask(id);

    private void OnTick(object? sender, EventArgs e)
    {
        if (IsActiveRunning && ActiveTaskId.HasValue)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == ActiveTaskId.Value);
            if (task != null)
            {
                task.WorkedSeconds += _timer.Interval.TotalSeconds;

                // A task that reaches its planned duration isn't auto-finished -
                // the user still decides via Finish/Done, same philosophy as
                // Pomodoro's "nothing happens without an explicit action".
                if (task.WorkedSeconds % 10 < 1)
                {
                    _planService.Save(_tasks);
                }
            }
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        _planService.Save(_tasks);
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }
}
