using System;
using System.Windows.Threading;
using Wilt.Models;

namespace Wilt.Services;

/// <summary>
/// Drives the Pomodoro session state machine: Phase, RunState, elapsed time and
/// the continuous Energy value (PRD sections 7.1, 8). Ticks on a DispatcherTimer
/// so it can be consumed directly by WPF bindings/animation code.
/// </summary>
public class TimerEngine
{
    private readonly DispatcherTimer _timer;
    private AppSettings _settings;
    private int _sessionsCompletedSinceLongBreak;

    public Phase Phase { get; private set; } = Phase.Inviting;
    public RunState RunState { get; private set; } = RunState.Idle;

    /// <summary>Total duration of the current phase, in seconds.</summary>
    public double PhaseDurationSeconds { get; private set; }

    /// <summary>Elapsed time within the current phase, in seconds. Fractional for smooth animation.</summary>
    public double ElapsedSeconds { get; private set; }

    /// <summary>0-100. 100 = fully rested. Decays during Focus, recovers during breaks.</summary>
    public double Energy { get; private set; } = 100;

    public event EventHandler? Tick;
    public event EventHandler<Phase>? PhaseCompleted;
    public event EventHandler<Phase>? PhaseStarted;

    public TimerEngine(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0), // 30fps per PRD 10.4
        };
        _timer.Tick += OnTick;
    }

    public double RemainingSeconds => Math.Max(0, PhaseDurationSeconds - ElapsedSeconds);

    public double Progress01 => PhaseDurationSeconds <= 0 ? 0 : Math.Clamp(ElapsedSeconds / PhaseDurationSeconds, 0, 1);

    public void ApplySettings(AppSettings settings)
    {
        var previousDuration = PhaseDurationSeconds;
        _settings = settings;

        if (RunState != RunState.Idle || Phase != Phase.Inviting)
        {
            var newDuration = GetConfiguredDurationSeconds(Phase);
            if (newDuration > 0 && previousDuration > 0 && Math.Abs(newDuration - previousDuration) > 0.01)
            {
                // PRD 9.5: preserve elapsed *proportion*, not absolute elapsed seconds.
                var proportion = Progress01;
                PhaseDurationSeconds = newDuration;
                ElapsedSeconds = proportion * newDuration;
            }
        }
    }

    public void Start()
    {
        if (Phase == Phase.Inviting)
        {
            BeginPhase(Phase.Focus);
        }

        RunState = RunState.Running;
        _timer.Start();
    }

    public void TogglePauseResume()
    {
        if (Phase == Phase.Inviting)
        {
            Start();
            return;
        }

        if (RunState == RunState.Running)
        {
            RunState = RunState.Paused;
        }
        else if (RunState == RunState.Paused)
        {
            RunState = RunState.Running;
        }
    }

    /// <summary>
    /// Ends the current session immediately, counted as complete rather than
    /// abandoned, then returns to Inviting (PRD 9.1.1) — a deliberate stop,
    /// unlike natural completion or Skip which both auto-advance.
    /// </summary>
    public void Finish()
    {
        if (Phase == Phase.Inviting)
        {
            return;
        }

        var completed = Phase;
        _timer.Stop();
        PhaseCompleted?.Invoke(this, completed);

        Phase = Phase.Inviting;
        RunState = RunState.Idle;
        ElapsedSeconds = 0;
        PhaseDurationSeconds = 0;
        Energy = 100;
        PhaseStarted?.Invoke(this, Phase.Inviting);
    }

    /// <summary>Skips straight to the next phase in rotation (used by tray/hotkey "skip").</summary>
    public void Skip()
    {
        if (Phase == Phase.Inviting)
        {
            Start();
            return;
        }

        AdvanceToNextPhase();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (RunState == RunState.Running)
        {
            ElapsedSeconds += _timer.Interval.TotalSeconds;
            UpdateEnergy();

            if (ElapsedSeconds >= PhaseDurationSeconds && PhaseDurationSeconds > 0)
            {
                AdvanceToNextPhase();
            }
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateEnergy()
    {
        var t = Progress01;
        switch (Phase)
        {
            case Phase.Focus:
                // Slight ease-in: slow initial decay, faster drop in the last third (PRD 7.1).
                Energy = 100 * (1 - EaseInCubicish(t));
                break;
            case Phase.ShortBreak:
            case Phase.LongBreak:
                Energy = Math.Min(100, Energy + (100.0 / Math.Max(1, PhaseDurationSeconds)) * _timer.Interval.TotalSeconds);
                break;
            default:
                break;
        }
    }

    private static double EaseInCubicish(double t)
    {
        // Blend of linear and cubic so the last third decays faster than the first two thirds.
        return (0.55 * t) + (0.45 * t * t * t);
    }

    /// <summary>
    /// Advances from the current phase to the next one in rotation (Focus ->
    /// break -> Focus...), used by natural tick-based completion and by
    /// Skip. Always keeps the timer running, unlike Finish which stops at
    /// Inviting.
    /// </summary>
    private void AdvanceToNextPhase()
    {
        var completed = Phase;
        _timer.Stop();
        PhaseCompleted?.Invoke(this, completed);

        if (completed == Phase.Focus)
        {
            _sessionsCompletedSinceLongBreak++;
            var nextPhase = _sessionsCompletedSinceLongBreak >= _settings.SessionsUntilLongBreak
                ? Phase.LongBreak
                : Phase.ShortBreak;

            if (nextPhase == Phase.LongBreak)
            {
                _sessionsCompletedSinceLongBreak = 0;
            }

            BeginPhase(nextPhase);
        }
        else
        {
            BeginPhase(Phase.Focus);
        }

        RunState = RunState.Running;
        _timer.Start();
    }

    private void BeginPhase(Phase phase)
    {
        Phase = phase;
        ElapsedSeconds = 0;
        PhaseDurationSeconds = GetConfiguredDurationSeconds(phase);
        Energy = phase == Phase.Focus ? 100 : Energy; // breaks recover from whatever Energy currently is
        PhaseStarted?.Invoke(this, phase);
    }

    private double GetConfiguredDurationSeconds(Phase phase) => phase switch
    {
        Phase.Focus => _settings.FocusMinutes * 60.0,
        Phase.ShortBreak => _settings.ShortBreakMinutes * 60.0,
        Phase.LongBreak => _settings.LongBreakMinutes * 60.0,
        _ => 0,
    };

    public EnergyState GetEnergyState()
    {
        if (Phase == Phase.Inviting)
        {
            return EnergyState.Inviting;
        }

        if (RunState == RunState.Paused)
        {
            return EnergyState.Paused;
        }

        if (Phase == Phase.ShortBreak || Phase == Phase.LongBreak)
        {
            return EnergyState.Recovering;
        }

        // Focus phase, driven purely by Energy band (PRD 7.2).
        return Energy switch
        {
            >= 75 => EnergyState.Fresh,
            >= 50 => EnergyState.Focused,
            >= 25 => EnergyState.Tiring,
            >= 10 => EnergyState.Tired,
            _ => EnergyState.Exhausted,
        };
    }
}
