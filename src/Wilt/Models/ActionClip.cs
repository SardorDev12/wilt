namespace Wilt.Models;

/// <summary>
/// A short, one-shot action clip that can be layered on top of the continuous
/// Energy-driven base pose (PRD section 7.4), e.g. "sip coffee", "yawn".
/// </summary>
public enum ActionClip
{
    // Focus-session actions (weighted higher at high Energy)
    Typing,
    SipCoffee,
    JotNote,
    GlanceSideMonitor,
    AdjustGlasses,
    QuickStretch,

    // Tiring/tired-range actions (weighted higher as Energy drops)
    Yawn,
    RubEyes,
    RestChinOnHand,
    NeckRoll,
    BlankStare,

    // Break actions
    StandAndStretch,
    WalkOffAndBack,
    DrinkWater,
    LookOutWindow,

    // Idle/paused actions
    BreatheLoop,
    FootTap,
    PenTwirl,

    // Inviting actions (no session active)
    GlanceAtStart,
    BeckonGesture,
    TidyDesk,
}
