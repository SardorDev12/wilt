# Wilt — A Pomodoro Timer That Gets Tired With You

Wilt is a Windows desktop Pomodoro timer that lives as a small, always-on-top
character overlay. Instead of a numeric countdown, the primary feedback is a
character who visibly tires as a focus session goes on — slouching, slowing
blinks, yawning — and recovers energy during breaks.

This repository contains the MVP implementation described in the product
requirements document (see `docs/prd.md`).

## Stack

- **.NET 8 / WPF** (`net8.0-windows`) for the shell: overlay window, timer
  engine, tray icon, settings window.
- **Win32 interop** (via `Native/NativeMethods.cs`) for the always-on-top,
  layered, click-through, non-activating overlay window behavior
  (`WS_EX_TOPMOST`, `WS_EX_LAYERED`, `WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE`).
- **Character rendering**: a custom illustration (`Resources/Characters/student.png`)
  driven by a single continuous `Energy` (0–100) value that transforms the
  whole figure (lean, sink, breathe) plus an eyelid overlay for blinking,
  with a randomized action-clip layer of whole-body gestures layered on top
  — the raster equivalent of the sprite/`Storyboard`-based fallback the PRD
  calls out in section 7.3 as an alternative to a licensed Rive rig. The
  `CharacterControl` is the single integration point, so swapping in a real
  Rive `.riv` rig (or a different illustration) later only touches that one
  control.

## Project layout

```
src/Wilt/
  App.xaml(.cs)              App entry point, single-instance guard
  Models/                    AppSettings, SessionState, Phase, ActionClip
  Services/                  Timer engine, settings/history persistence,
                              startup registration, global hotkeys, tray icon
  Native/                    Win32 interop for the overlay window styles
  Controls/                  CharacterControl (the animated rig), ProgressRing
  Views/                     OverlayWindow, SettingsWindow, StatsPanel
```

## Building (Visual Studio not required)

WPF only builds/runs on Windows, so this must be built on a Windows machine.
You only need the free **.NET 8 SDK** — no Visual Studio install required:

1. Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
   (pick the "SDK" x64 installer, not "Runtime").
2. Open PowerShell/cmd in this repo folder and either:
   - Double-click / run `build.bat` — produces a single self-contained
     `publish\Wilt.exe` you can run directly, no .NET install needed on the
     machine that runs it; or
   - Run it manually for a quick dev build instead:
     ```
     dotnet build Wilt.sln
     dotnet run --project src/Wilt/Wilt.csproj
     ```

If `dotnet build`/`build.bat` reports errors, copy the full output — that's
the fastest way to get a fix, since this repo was authored without access to
a Windows machine to compile it directly.

## Status

MVP implementing the "Must have" scope from the PRD:

- Core Pomodoro timer (focus/short break/long break, configurable durations
  and sessions-until-long-break).
- Always-on-screen character overlay (Inviting / Fresh / Focused / Tiring /
  Tired / Exhausted / Recovering / Reset / Paused states) driven by a
  continuous Energy value, with a randomized action-clip layer.
- A single play/pause control (plus Finish) and a circular time-remaining
  halo built into the overlay widget. Auto-start is disabled: when a
  session ends on its own, nothing times down until the user acts - the
  widget goes back to idle with a pulsing, clearly labeled "Start Focus" /
  "Start Break" button (Skip remains an explicit "move on now" action and
  does auto-continue).
- System tray icon with quick actions and a live tooltip.
- Settings window (durations, sessions-until-long-break, overlay size,
  opacity, click-through, sound, launch-on-startup, character skin) that
  applies changes live, including mid-session proportional duration rescale.
- End-of-session/break audio cue: a soft, non-jarring system chime (no
  spoken/voice output).
- Local JSON-based settings + session history persistence, no network calls.
- Should-have extras: local stats panel, multiple skins, per-monitor
  position memory, global hotkeys for start/pause/skip.
- Could-have extras: idle/AFK auto-pause for Focus sessions only, not
  breaks (checks system-wide keyboard/mouse input via GetLastInputInfo,
  configurable timeout), and a "2/4" sessions-
  completed-toward-next-long-break counter next to the progress ring.
