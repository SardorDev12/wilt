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
/// The "sitting at a desk, working" pose (PRD 7.3's sprite/vector fallback
/// rig): a hand-authored vector figure at a desk with books and a pen cup.
/// A continuous Energy value (0-100) drives the base pose (posture lean,
/// head droop, blink rate) via simple interpolation, and a separate
/// randomized one-shot "action clip" layer (PRD 7.4) plays short gestures
/// (typing, sipping coffee, yawning...) on top of it. See
/// <see cref="CharacterControl"/> for the alternate standing pose built
/// from the user's illustration.
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

    // Smoothed (lerp'd) values so posture changes read as organic rather than stepped.
    private double _smoothLean;
    private double _smoothHeadDroop;
    private double _smoothSink;

    public DeskCharacterControl()
    {
        InitializeComponent();
        ApplySkinBrushes(Skin);

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
        control.ApplySkinBrushes((CharacterSkin)e.NewValue);
    }

    private void ApplySkinBrushes(CharacterSkin skin)
    {
        // Only the vest re-colors per skin; sleeves/shirt stay cream and the
        // hat/hair/skin tone stay fixed, since those are the character's
        // identity rather than a "theme color".
        var color = skin switch
        {
            CharacterSkin.Amber => Color.FromRgb(0x6E, 0x40, 0x28),
            CharacterSkin.Mint => Color.FromRgb(0x1E, 0x4A, 0x3D),
            _ => Color.FromRgb(0x1F, 0x2A, 0x44),
        };

        TorsoBody.Fill = new SolidColorBrush(color);
    }

    /// <summary>Called every timer tick (~30fps) from the host window to drive the continuous base pose.</summary>
    public void ApplyState(double energy, EnergyState state, bool animateImmediately = false)
    {
        _currentEnergy = energy;
        _currentState = state;

        // Target posture values per state; Energy itself provides the fine-grained
        // interpolation within the Focus band so motion feels continuous (PRD 7.1).
        double targetLean, targetHeadDroop, targetSink;

        switch (state)
        {
            case EnergyState.Inviting:
                targetLean = -2; targetHeadDroop = -3; targetSink = 0;
                break;
            case EnergyState.Fresh:
                targetLean = 0; targetHeadDroop = 0; targetSink = 0;
                break;
            case EnergyState.Focused:
                targetLean = 3; targetHeadDroop = 1; targetSink = 1;
                break;
            case EnergyState.Tiring:
                targetLean = 7; targetHeadDroop = 4; targetSink = 3;
                break;
            case EnergyState.Tired:
                targetLean = 12; targetHeadDroop = 9; targetSink = 6;
                break;
            case EnergyState.Exhausted:
                targetLean = 18; targetHeadDroop = 16; targetSink = 10;
                break;
            case EnergyState.Recovering:
                // Recovers proportionally with Energy climbing back toward 100.
                var recoveredFraction = Math.Clamp(energy / 100.0, 0, 1);
                targetLean = 10 * (1 - recoveredFraction);
                targetHeadDroop = 8 * (1 - recoveredFraction);
                targetSink = 5 * (1 - recoveredFraction);
                break;
            case EnergyState.Reset:
                targetLean = -1; targetHeadDroop = -2; targetSink = 0;
                break;
            case EnergyState.Paused:
                // Hold at whatever the current smoothed pose is (frozen tiredness, PRD 7.0/7.1).
                targetLean = _smoothLean; targetHeadDroop = _smoothHeadDroop; targetSink = _smoothSink;
                break;
            default:
                targetLean = 0; targetHeadDroop = 0; targetSink = 0;
                break;
        }

        var lerp = animateImmediately ? 1.0 : 0.06;
        _smoothLean += (targetLean - _smoothLean) * lerp;
        _smoothHeadDroop += (targetHeadDroop - _smoothHeadDroop) * lerp;
        _smoothSink += (targetSink - _smoothSink) * lerp;

        TorsoRotate.Angle = _smoothLean;
        TorsoTranslate.Y = _smoothSink;
        HeadRotate.Angle = _smoothLean * 0.6 + _smoothHeadDroop;

        // Eyes droop (partially close) toward Exhausted, independent of the blink loop.
        var droopFloor = state switch
        {
            EnergyState.Exhausted => 0.25,
            EnergyState.Tired => 0.55,
            EnergyState.Tiring => 0.8,
            _ => 1.0,
        };
        if (Math.Abs(EyeLeftScale.ScaleY - droopFloor) > 0.01 && !_isBlinking)
        {
            EyeLeftScale.ScaleY = droopFloor;
            EyeRightScale.ScaleY = droopFloor;
        }

        ZzzMotif.Opacity = (WhimsyEmbellishments && state == EnergyState.Exhausted) ? 0.8 : 0.0;
    }

    private bool _isBlinking;

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

        // A single close-then-open keyframe timeline per eye, so there's no
        // handoff conflict between a separate "close" and "open" animation
        // fighting over the same property.
        var blink = new DoubleAnimationUsingKeyFrames();
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(0.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))));

        // Release the animation clock once it finishes so plain property
        // assignments in ApplyState (e.g. the Exhausted droop floor) take
        // effect again afterward, instead of staying pinned by a filling clock.
        blink.Completed += (_, _) =>
        {
            _isBlinking = false;
            EyeLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            EyeRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        };

        EyeLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
        EyeRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
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

    private void Play(ActionClip clip)
    {
        switch (clip)
        {
            case ActionClip.Typing:
                AnimateArm(FrontArmRotate, -8, 8, 260, repeats: 3);
                break;
            case ActionClip.SipCoffee:
                AnimateSipCoffee();
                break;
            case ActionClip.QuickStretch:
            case ActionClip.StandAndStretch:
                AnimateArm(BackArmRotate, 0, -40, 500, repeats: 1, autoReverse: true);
                break;
            case ActionClip.Yawn:
                AnimateYawn();
                break;
            case ActionClip.RubEyes:
                AnimateArm(FrontArmRotate, 0, -25, 350, repeats: 1, autoReverse: true);
                break;
            case ActionClip.RestChinOnHand:
                AnimateArm(FrontArmRotate, 0, -30, 600, repeats: 1, autoReverse: false, returnAfterMs: 2500);
                break;
            case ActionClip.NeckRoll:
                AnimateHeadRoll();
                break;
            case ActionClip.JotNote:
                AnimateArm(FrontArmRotate, 0, -10, 200, repeats: 4);
                break;
            case ActionClip.GlanceSideMonitor:
            case ActionClip.GlanceAtStart:
            case ActionClip.BlankStare:
                AnimateHeadGlance();
                break;
            case ActionClip.AdjustGlasses:
                AnimateArm(FrontArmRotate, 0, -15, 250, repeats: 1, autoReverse: true);
                break;
            case ActionClip.DrinkWater:
                AnimateSipCoffee();
                break;
            case ActionClip.WalkOffAndBack:
            case ActionClip.LookOutWindow:
                AnimateHeadGlance();
                break;
            case ActionClip.BeckonGesture:
                AnimateArm(BackArmRotate, 0, -20, 300, repeats: 2, autoReverse: true);
                break;
            case ActionClip.TidyDesk:
                AnimateArm(FrontArmRotate, 0, 12, 300, repeats: 2, autoReverse: true);
                break;
            case ActionClip.BreatheLoop:
            case ActionClip.FootTap:
            case ActionClip.PenTwirl:
                // Subtle idle fidget; a small head bob reads as "still alive" without being showy.
                AnimateHeadGlance();
                break;
        }
    }

    private void AnimateArm(RotateTransform transform, double from, double to, int durationMs, int repeats,
        bool autoReverse = false, int returnAfterMs = 0)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            AutoReverse = autoReverse,
            RepeatBehavior = new RepeatBehavior(repeats),
        };

        if (returnAfterMs > 0)
        {
            anim.Completed += (_, _) =>
            {
                var returnAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300))
                {
                    BeginTime = TimeSpan.FromMilliseconds(returnAfterMs),
                };
                transform.BeginAnimation(RotateTransform.AngleProperty, returnAnim);
            };
        }
        else
        {
            anim.Completed += (_, _) => transform.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        transform.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void AnimateHeadGlance()
    {
        var anim = new DoubleAnimation(0, 12, TimeSpan.FromMilliseconds(400))
        {
            AutoReverse = true,
        };
        anim.Completed += (_, _) => HeadTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        HeadTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void AnimateHeadRoll()
    {
        var anim = new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
        };
        var current = HeadRotate.Angle;
        anim.From = current;
        anim.To = current + 10;
        anim.Completed += (_, _) => HeadRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        HeadRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void AnimateYawn()
    {
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(900) };
        var mouthFadeIn = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        var mouthFadeOut = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(900) };

        var sb = new Storyboard();
        Storyboard.SetTarget(fadeIn, MouthYawn);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
        Storyboard.SetTarget(fadeOut, MouthYawn);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        Storyboard.SetTarget(mouthFadeIn, MouthNeutral);
        Storyboard.SetTargetProperty(mouthFadeIn, new PropertyPath(OpacityProperty));
        Storyboard.SetTarget(mouthFadeOut, MouthNeutral);
        Storyboard.SetTargetProperty(mouthFadeOut, new PropertyPath(OpacityProperty));
        sb.Children.Add(fadeIn);
        sb.Children.Add(fadeOut);
        sb.Children.Add(mouthFadeIn);
        sb.Children.Add(mouthFadeOut);
        sb.Begin();

        AnimateArm(FrontArmRotate, 0, -35, 400, repeats: 1, autoReverse: true);
    }

    private void AnimateSipCoffee()
    {
        var raise = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(400)) { AutoReverse = true };
        raise.Completed += (_, _) => FrontArmRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        FrontArmRotate.BeginAnimation(RotateTransform.AngleProperty, raise);

        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(650) };
        var sb = new Storyboard();
        Storyboard.SetTarget(fadeIn, CoffeeCup);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
        Storyboard.SetTarget(fadeOut, CoffeeCup);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        sb.Children.Add(fadeIn);
        sb.Children.Add(fadeOut);
        sb.Begin();
    }
}
