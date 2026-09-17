using System;
using System.Windows.Threading;
using Wilt.Models;
using Wilt.Native;

namespace Wilt.Services;

/// <summary>
/// Auto-pauses a running session after no system-wide keyboard or mouse
/// input for a configurable timeout (PRD 8, "Could have": idle detection),
/// and auto-resumes once input comes back - but only for a pause *this*
/// service caused, never overriding a pause the user made themselves.
/// </summary>
public class IdleDetectionService
{
    private readonly TimerEngine _timerEngine;
    private readonly DispatcherTimer _pollTimer;
    private bool _pausedByIdle;

    public bool Enabled { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    public IdleDetectionService(TimerEngine timerEngine)
    {
        _timerEngine = timerEngine;
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
        var isActiveSession = _timerEngine.Phase != Phase.Inviting;

        if (!isActiveSession)
        {
            _pausedByIdle = false;
            return;
        }

        if (_timerEngine.RunState == RunState.Running && idle >= Timeout)
        {
            _timerEngine.TogglePauseResume();
            _pausedByIdle = true;
        }
        else if (_pausedByIdle && idle < Timeout)
        {
            if (_timerEngine.RunState == RunState.Paused)
            {
                _timerEngine.TogglePauseResume();
            }

            _pausedByIdle = false;
        }
    }
}
