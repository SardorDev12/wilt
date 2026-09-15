namespace Wilt.Models;

/// <summary>The current phase of the Pomodoro session lifecycle.</summary>
public enum Phase
{
    /// <summary>App is running, no session has been started yet.</summary>
    Inviting,
    Focus,
    ShortBreak,
    LongBreak,
}

/// <summary>Whether the active phase's timer is running or held.</summary>
public enum RunState
{
    Idle,
    Running,
    Paused,
}

/// <summary>Energy-driven visual state of the character (PRD section 7.2).</summary>
public enum EnergyState
{
    Inviting,
    Fresh,
    Focused,
    Tiring,
    Tired,
    Exhausted,
    Recovering,
    Reset,
    Paused,
}
