using System;
using System.Windows.Threading;
using Wilt.Models;
using Wilt.Native;

namespace Wilt.Services;

/// <summary>
/// Auto-pauses a running Focus session (not breaks - being away from the
/// keyboard/mouse during a break is expected) or an actively-running task
/// (Task Mode) after no system-wide keyboard or mouse input for a
/// configurable timeout (PRD 8, "Could have": idle detection), and
/// auto-resumes once input comes back - but only for a pause *this* service
/// caused, never overriding a pause the user made themselves. Both are
/// checked independently of which view mode the overlay currently shows,
/// since switching modes doesn't necessarily stop whichever engine was
/// already running.
/// </summary>
public class IdleDetectionService
{
    private readonly TimerEngine _timerEngine;
    private readonly TaskEngine _taskEngine;
    private readonly DispatcherTimer _pollTimer;
    private bool _focusPausedByIdle;
    private bool _taskPausedByIdle;

    public bool Enabled { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    public IdleDetectionService(TimerEngine timerEngine, TaskEngine taskEngine)
    {
        _timerEngine = timerEngine;
        _taskEngine = taskEngine;
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10),
        };
        _pollTimer.Tick += (_, _) => Check();
        _pollTimer.Start();
    }

    private void Check()
    {
        if (!Enabled)
        {
            return;
        }

        var idle = NativeMethods.GetIdleTime();

        CheckFocusSession(idle);
        CheckActiveTask(idle);
    }

    private void CheckFocusSession(TimeSpan idle)
    {
        // Focus only: being away from the keyboard/mouse during a break is
        // expected (that's the point of the break), so idle detection
        // shouldn't stall it out.
        var isActiveFocusSession = _timerEngine.Phase == Phase.Focus && !_timerEngine.AwaitingAcknowledgment;

        if (!isActiveFocusSession)
        {
            _focusPausedByIdle = false;
            return;
        }

        if (_timerEngine.RunState == RunState.Running && idle >= Timeout)
        {
            _timerEngine.TogglePauseResume();
            _focusPausedByIdle = true;
        }
        else if (_focusPausedByIdle && idle < Timeout)
        {
            if (_timerEngine.RunState == RunState.Paused)
            {
                _timerEngine.TogglePauseResume();
            }

            _focusPausedByIdle = false;
        }
    }

    private void CheckActiveTask(TimeSpan idle)
    {
        if (!_taskEngine.ActiveTaskId.HasValue)
        {
            _taskPausedByIdle = false;
            return;
        }

        if (_taskEngine.IsActiveRunning && idle >= Timeout)
        {
            _taskEngine.PauseActive();
            _taskPausedByIdle = true;
        }
        else if (_taskPausedByIdle && idle < Timeout)
        {
            if (!_taskEngine.IsActiveRunning)
            {
                _taskEngine.StartTask(_taskEngine.ActiveTaskId.Value);
            }

            _taskPausedByIdle = false;
        }
    }
}
