using System;
using System.Media;
using System.Speech.Synthesis;

namespace Wilt.Services;

/// <summary>
/// End-of-session / end-of-break audio cues (PRD 8, "Must have"): a soft
/// chime followed by a short spoken line via Windows' built-in speech
/// synthesizer (SAPI), so the end of a session is announced rather than
/// just beeped - no bundled audio assets required.
/// </summary>
public class SoundService
{
    private readonly SpeechSynthesizer? _synth;
    private readonly Random _random = new();

    private static readonly string[] FocusCompleteLines =
    {
        "Nice focus session. Time for a break.",
        "Great work. You've earned a break.",
        "Focus session complete. Go stretch your legs.",
    };

    private static readonly string[] BreakCompleteLines =
    {
        "Break's over. Ready to focus?",
        "Break's done. Let's get back to it.",
        "Time to get back to focus.",
    };

    public SoundService()
    {
        try
        {
            _synth = new SpeechSynthesizer();
            _synth.SetOutputToDefaultAudioDevice();
            _synth.Rate = -1;
            _synth.Volume = 85;
        }
        catch (Exception)
        {
            // No speech engine/voice installed: fall back to the chime alone.
            _synth = null;
        }
    }

    public void PlayFocusComplete()
    {
        SystemSounds.Asterisk.Play();
        Speak(FocusCompleteLines);
    }

    public void PlayBreakComplete()
    {
        SystemSounds.Exclamation.Play();
        Speak(BreakCompleteLines);
    }

    private void Speak(string[] lines)
    {
        if (_synth == null)
        {
            return;
        }

        _synth.SpeakAsyncCancelAll();
        _synth.SpeakAsync(lines[_random.Next(lines.Length)]);
    }
}
