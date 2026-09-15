using System.Media;

namespace Wilt.Services;

/// <summary>
/// End-of-session / end-of-break audio cues (PRD 8, "Must have"). Uses the
/// system notification sounds by default so the app ships with zero binary
/// assets; swap in a custom .wav under Resources/Sounds and play it via
/// SoundPlayer if a bespoke, softer cue is desired later.
/// </summary>
public class SoundService
{
    public void PlayFocusComplete()
    {
        SystemSounds.Asterisk.Play();
    }

    public void PlayBreakComplete()
    {
        SystemSounds.Exclamation.Play();
    }
}
