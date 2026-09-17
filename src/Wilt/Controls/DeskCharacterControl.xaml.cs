using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wilt.Models;

namespace Wilt.Controls;

/// <summary>
/// The "sitting at a desk, working" pose: the same character illustrated
/// seated and writing, driven by the same rig technique as the standing
/// pose (<see cref="CharacterControl"/>) since it's likewise a single
/// flattened image with no separable limbs:
///  - a continuous Energy-driven whole-body transform (lean, sink, breathe)
///    for the base pose, applied every timer tick;
///  - a randomized one-shot "action clip" layer (PRD 7.4) that plays small
///    whole-body gestures (a bounce, a tilt, a stretch) plus an eyelid
///    overlay for blinking/yawning, since the eyes are baked into the image.
/// </summary>
public partial class DeskCharacterControl : System.Windows.Controls.UserControl, ICharacterView
{
    public static readonly DependencyProperty SkinProperty = DependencyProperty.Register(
        nameof(Skin), typeof(CharacterSkin), typeof(DeskCharacterControl),
        new PropertyMetadata(CharacterSkin.Slate, OnSkinChanged));

    public static readonly DependencyProperty WhimsyEmbellishmentsProperty = DependencyProperty.Register(
        nameof(WhimsyEmbellishments), typeof(bool), typeof(DeskCharacterControl),
        new PropertyMetadata(false));

    public CharacterSkin Skin
    {
        get => (CharacterSkin)GetValue(SkinProperty);
        set => SetValue(SkinProperty, value);
    }

    public bool WhimsyEmbellishments
    {
        get => (bool)GetValue(WhimsyEmbellishmentsProperty);
        set => SetValue(WhimsyEmbellishmentsProperty, value);
    }

    private readonly DispatcherTimer _actionTimer;
    private readonly DispatcherTimer _blinkTimer;
    private readonly Random _random = new();
    private ActionClip? _lastAction;
    private EnergyState _currentState = EnergyState.Inviting;
    private double _currentEnergy = 100;
    private bool _isBlinking;

    // Smoothed (lerp'd) values so posture changes read as organic rather than stepped.
    private double _smoothLean;
    private double _smoothSink;
    private double _smoothBreath = 1.0;

    public DeskCharacterControl()
    {
        InitializeComponent();
        ApplyGlowColor(Skin);

        _actionTimer = new DispatcherTimer();
        _actionTimer.Tick += (_, _) => PlayRandomAction();
        ScheduleNextAction();

        _blinkTimer = new DispatcherTimer();
        _blinkTimer.Tick += (_, _) => Blink();
        ScheduleNextBlink();

        Loaded += (_, _) => ApplyState(_currentEnergy, _currentState, animateImmediately: true);
    }

    /// <summary>Starts/stops the blink and action-clip timers (this pose isn't the active one otherwise).</summary>
    public void SetActive(bool active)
    {
        if (active)
        {
            _actionTimer.Start();
            _blinkTimer.Start();
        }
        else
        {
            _actionTimer.Stop();
            _blinkTimer.Stop();
        }
    }

    private static void OnSkinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DeskCharacterControl)d;
        control.ApplyGlowColor((CharacterSkin)e.NewValue);
    }

    private void ApplyGlowColor(CharacterSkin skin)
    {
        // The artwork itself isn't re-tinted (it's a fixed illustration); the
        // skin choice instead re-colors the ambient glow behind the character.
        var color = skin switch
        {
            CharacterSkin.Amber => Color.FromRgb(0xC9, 0x7A, 0x3A),
            CharacterSkin.Mint => Color.FromRgb(0x4E, 0x9B, 0x84),
            _ => Color.FromRgb(0x2E, 0x7D, 0x6B),
        };

        GlowStopInner.Color = Color.FromArgb(0x55, color.R, color.G, color.B);
        GlowStopOuter.Color = Color.FromArgb(0x00, color.R, color.G, color.B);
    }

    /// <summary>Called every timer tick (~30fps) from the host window to drive the continuous base pose.</summary>
    public void ApplyState(double energy, EnergyState state, bool animateImmediately = false)
    {
        _currentEnergy = energy;
        _currentState = state;

        double targetLean, targetSink;

        switch (state)
        {
            case EnergyState.Inviting:
                targetLean = -1.5; targetSink = 0;
                break;
            case EnergyState.Fresh:
                targetLean = 0; targetSink = 0;
                break;
            case EnergyState.Focused:
                targetLean = 2; targetSink = 1;
                break;
            case EnergyState.Tiring:
                targetLean = 4.5; targetSink = 3;
                break;
            case EnergyState.Tired:
                targetLean = 8; targetSink = 6;
                break;
            case EnergyState.Exhausted:
                targetLean = 13; targetSink = 10;
                break;
            case EnergyState.Recovering:
                var recoveredFraction = Math.Clamp(energy / 100.0, 0, 1);
                targetLean = 9 * (1 - recoveredFraction);
                targetSink = 7 * (1 - recoveredFraction);
                break;
            case EnergyState.Reset:
                targetLean = -0.5; targetSink = 0;
                break;
            case EnergyState.Paused:
                // Hold at whatever the current smoothed pose is (frozen tiredness, PRD 7.0/7.1).
                targetLean = _smoothLean; targetSink = _smoothSink;
                break;
            default:
                targetLean = 0; targetSink = 0;
                break;
        }

        var lerp = animateImmediately ? 1.0 : 0.06;
        _smoothLean += (targetLean - _smoothLean) * lerp;
        _smoothSink += (targetSink - _smoothSink) * lerp;

        BodyRotate.Angle = _smoothLean;
        BodyTranslate.Y = _smoothSink;

        // Slow breathing pulse, subtler than the action-clip layer's bounces.
        var breathPhase = Math.Sin(Environment.TickCount64 / 1400.0) * 0.006;
        var targetBreath = 1.0 + breathPhase;
        _smoothBreath += (targetBreath - _smoothBreath) * (animateImmediately ? 1.0 : 0.15);
        BodyScale.ScaleX = _smoothBreath;
        BodyScale.ScaleY = _smoothBreath;

        // Eyes droop (partially close) toward Exhausted, independent of the blink loop.
        var droopFloor = state switch
        {
            EnergyState.Exhausted => 0.7,
            EnergyState.Tired => 0.4,
            EnergyState.Tiring => 0.15,
            _ => 0.0,
        };
        if (!_isBlinking)
        {
            EyeLidLeftScale.ScaleY = droopFloor;
            EyeLidRightScale.ScaleY = droopFloor;
        }

        TiredOverlay.Opacity = state switch
        {
            EnergyState.Tiring => 0.06,
            EnergyState.Tired => 0.14,
            EnergyState.Exhausted => 0.24,
            _ => 0.0,
        };

        ZzzMotif.Opacity = (WhimsyEmbellishments && state == EnergyState.Exhausted) ? 0.8 : 0.0;
    }

    private void ScheduleNextBlink()
    {
        // Blink rate slows as Energy drops (PRD 7.2): fresher states blink more often.
        var baseSeconds = _currentState switch
        {
            EnergyState.Fresh => 3.0,
            EnergyState.Focused => 3.5,
            EnergyState.Tiring => 5.0,
            EnergyState.Tired => 6.5,
            EnergyState.Exhausted => 8.0,
            _ => 4.0,
        };
        _blinkTimer.Interval = TimeSpan.FromSeconds(baseSeconds + _random.NextDouble() * 2.0);
    }

    private void Blink()
    {
        ScheduleNextBlink();
        if (_isBlinking)
        {
            return;
        }

        _isBlinking = true;

        var blink = new DoubleAnimationUsingKeyFrames();
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))));

        blink.Completed += (_, _) =>
        {
            _isBlinking = false;
            EyeLidLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            EyeLidRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            // Re-apply the current droop floor now that the blink clock released the property.
            ApplyState(_currentEnergy, _currentState, animateImmediately: true);
        };

        EyeLidLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
        EyeLidRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
    }

    private void ScheduleNextAction()
    {
        // Minimum interval between action clips so the character doesn't look twitchy (PRD 7.4).
        _actionTimer.Interval = TimeSpan.FromSeconds(20 + _random.NextDouble() * 25);
    }

    private static readonly Dictionary<EnergyState, ActionClip[]> ActionPools = new()
    {
        [EnergyState.Inviting] = new[] { ActionClip.GlanceAtStart, ActionClip.BeckonGesture, ActionClip.TidyDesk },
        [EnergyState.Fresh] = new[] { ActionClip.Typing, ActionClip.SipCoffee, ActionClip.JotNote, ActionClip.GlanceSideMonitor, ActionClip.AdjustGlasses, ActionClip.QuickStretch },
        [EnergyState.Focused] = new[] { ActionClip.Typing, ActionClip.Typing, ActionClip.SipCoffee, ActionClip.JotNote, ActionClip.GlanceSideMonitor },
        [EnergyState.Tiring] = new[] { ActionClip.Typing, ActionClip.NeckRoll, ActionClip.QuickStretch, ActionClip.RubEyes, ActionClip.AdjustGlasses },
        [EnergyState.Tired] = new[] { ActionClip.Yawn, ActionClip.RubEyes, ActionClip.RestChinOnHand, ActionClip.NeckRoll, ActionClip.BlankStare },
        [EnergyState.Exhausted] = new[] { ActionClip.Yawn, ActionClip.RestChinOnHand, ActionClip.BlankStare, ActionClip.RubEyes },
        [EnergyState.Recovering] = new[] { ActionClip.StandAndStretch, ActionClip.WalkOffAndBack, ActionClip.DrinkWater, ActionClip.LookOutWindow },
        [EnergyState.Paused] = new[] { ActionClip.BreatheLoop, ActionClip.FootTap, ActionClip.PenTwirl },
        [EnergyState.Reset] = new[] { ActionClip.QuickStretch },
    };

    private void PlayRandomAction()
    {
        ScheduleNextAction();

        if (!ActionPools.TryGetValue(_currentState, out var pool) || pool.Length == 0)
        {
            return;
        }

        // Avoid immediate repeats (PRD 7.4).
        var candidates = pool.Where(a => a != _lastAction).ToArray();
        if (candidates.Length == 0)
        {
            candidates = pool;
        }

        var chosen = candidates[_random.Next(candidates.Length)];
        _lastAction = chosen;
        Play(chosen);
    }

    /// <summary>
    /// Maps each action clip to one of a handful of whole-body gestures,
    /// since the flattened artwork has no separable limbs to animate
    /// independently. Variety comes from which gesture plays and how often,
    /// keyed to the current Energy band (PRD 7.4), not per-clip uniqueness.
    /// </summary>
    private void Play(ActionClip clip)
    {
        switch (clip)
        {
            case ActionClip.Typing:
            case ActionClip.JotNote:
            case ActionClip.TidyDesk:
                Bounce();
                break;
            case ActionClip.SipCoffee:
            case ActionClip.DrinkWater:
            case ActionClip.GlanceSideMonitor:
            case ActionClip.GlanceAtStart:
            case ActionClip.LookOutWindow:
            case ActionClip.WalkOffAndBack:
                Glance();
                break;
            case ActionClip.QuickStretch:
            case ActionClip.StandAndStretch:
            case ActionClip.BeckonGesture:
                Stretch();
                break;
            case ActionClip.Yawn:
                Yawn();
                break;
            case ActionClip.RubEyes:
            case ActionClip.RestChinOnHand:
            case ActionClip.BlankStare:
                SlowBlinkHold();
                break;
            case ActionClip.NeckRoll:
            case ActionClip.AdjustGlasses:
                Tilt();
                break;
            case ActionClip.BreatheLoop:
            case ActionClip.FootTap:
            case ActionClip.PenTwirl:
                Bounce(small: true);
                break;
        }
    }

    private void Bounce(bool small = false)
    {
        var amount = small ? 2.0 : 4.0;
        var anim = new DoubleAnimation(_smoothSink, _smoothSink - amount, TimeSpan.FromMilliseconds(160))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(small ? 1 : 2),
        };
        anim.Completed += (_, _) => BodyTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        BodyTranslate.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    private void Glance()
    {
        var current = BodyRotate.Angle;
        var anim = new DoubleAnimation(current, current + (_random.Next(2) == 0 ? 6 : -6), TimeSpan.FromMilliseconds(420))
        {
            AutoReverse = true,
        };
        anim.Completed += (_, _) => BodyRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        BodyRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void Tilt()
    {
        var current = BodyRotate.Angle;
        var anim = new DoubleAnimation(current, current - 5, TimeSpan.FromMilliseconds(500))
        {
            AutoReverse = true,
        };
        anim.Completed += (_, _) => BodyRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        BodyRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void Stretch()
    {
        var scaleAnim = new DoubleAnimation(_smoothBreath, _smoothBreath * 1.04, TimeSpan.FromMilliseconds(450))
        {
            AutoReverse = true,
        };
        scaleAnim.Completed += (_, _) =>
        {
            BodyScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            BodyScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        };
        BodyScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        BodyScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        var riseAnim = new DoubleAnimation(_smoothSink, _smoothSink - 6, TimeSpan.FromMilliseconds(450))
        {
            AutoReverse = true,
        };
        riseAnim.Completed += (_, _) => BodyTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        BodyTranslate.BeginAnimation(TranslateTransform.YProperty, riseAnim);
    }

    private void SlowBlinkHold()
    {
        if (_isBlinking)
        {
            return;
        }

        _isBlinking = true;

        // Hold the "eyes mostly closed" pose for a beat before reopening.
        var hold = new DoubleAnimationUsingKeyFrames();
        hold.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
        hold.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900))));
        hold.KeyFrames.Add(new LinearDoubleKeyFrame(_currentEnergy > 25 ? 0.0 : 0.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1150))));
        hold.Completed += (_, _) =>
        {
            _isBlinking = false;
            EyeLidLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            EyeLidRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ApplyState(_currentEnergy, _currentState, animateImmediately: true);
        };

        EyeLidLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, hold);
        EyeLidRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, hold);
    }

    private void Yawn()
    {
        SlowBlinkHold();
        Tilt();
    }
}
