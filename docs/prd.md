# Product Requirements Document: "Wilt" — A Pomodoro Timer That Gets Tired With You

**Status:** Draft v1.0
**Author:** Product spec prepared for Sardor
**Date:** September 15, 2026
**Platform:** Windows (desktop)

---

## 1. Summary

Wilt is a lightweight Windows desktop Pomodoro timer that lives as a small always-on-top character overlay, visible over any other application. Instead of a numeric countdown or a progress bar, the primary feedback mechanism is a subtly animated human character who visibly gets more tired — slower blinks, slouching posture, yawns, rubbing eyes — as a work session approaches its end. When the break starts, the character recovers energy (stretching, sipping water); when a new focus session begins, it resets to an alert, upright state.

The goal is to replace the cold, clinical feel of a countdown clock with an empathetic, ambient visual cue that mirrors the user's own fatigue curve, nudging them toward a break without an alarm or a popup interrupting their flow.

## 2. Problem Statement

Existing Pomodoro apps (Forest, TomatoTimer, native OS widgets, etc.) communicate time almost exclusively through numbers or a shrinking bar. That works, but it is easy to tune out — numbers require a conscious read, and a countdown gives no *felt* sense of urgency until it hits zero and the alarm fires. Users who are deep in focus either ignore the timer until the harsh end-of-session alert breaks their concentration, or they clock-watch, which undermines the point of using a timer at all.

A character that visibly tires mirrors something the user already intuitively understands (a person getting sleepy) and delivers a soft, continuous, peripheral signal: "you have been at this a while," "you're almost due for a break," "it's time to stop." It's designed to be glanced at, not read.

## 3. Goals

- Give users an ambient, non-intrusive sense of time passing during a focus session, without requiring them to read numbers.
- Make the moment a break is due feel earned and visible, reducing reliance on jarring alarms.
- Keep the app genuinely unobtrusive: low CPU/GPU footprint, small on-screen footprint, never steals focus or blocks input to the app underneath it.
- Ship a lovable, well-crafted MVP a solo developer (or very small team) can realistically build and maintain as a free indie project.

## 4. Non-Goals (v1)

- No team/collaboration features, no accounts, no cloud sync.
- No monetization, subscriptions, or paid character packs in v1 (see Section 13 for future optionality).
- No mobile or macOS/Linux clients in v1 — Windows only.
- No task management, to-do lists, or project tracking. Wilt is a timer, not a productivity suite.
- No AI-driven adaptive scheduling in v1 (e.g., auto-adjusting session length based on behavior). Possible future exploration, not MVP.

## 5. Target Users

**Primary persona — "The Deep-Work Freelancer/Knowledge Worker"**
Works alone at a desktop most of the day (developers, writers, analysts, designers). Already uses or has tried Pomodoro-style techniques. Finds most timer apps either too naggy (loud alarms, modal popups) or too easy to ignore (a tiny menu-bar countdown). Wants something that sits quietly in their peripheral vision.

**Secondary persona — "The Timer-App Collector"**
Enjoys novel, well-designed productivity tools and habitually tries new Pomodoro apps. Attracted by a distinctive visual gimmick and likely to share it if it's charming and well executed (relevant for indie/organic distribution, e.g., Product Hunt, r/productivity, Twitter/X).

## 6. Product Vision & Differentiation

| Existing approach | Wilt's approach |
|---|---|
| Numeric countdown / progress bar | A living character whose visible energy level *is* the countdown |
| Loud alarm at session end | Character visibly "runs out of steam" — the end is anticipated, not sprung on you |
| Full window or menu-bar icon | Small, always-on-top floating overlay that sits above any app, draggable anywhere on screen |
| Generic across all timer apps | A distinct, ownable visual identity/mascot users can get attached to |

The single differentiating bet of this product is the animation concept. Everything else (timer logic, settings, session history) is deliberately conventional and should not distract engineering effort from getting the character and its overlay behavior right.

## 7. Core Concept: The Energy-Driven Character

The character is not a set of discrete "tired / not tired" sprites triggered at fixed checkpoints. It is driven by a single continuous **Energy** value (0–100) that decays over the course of a focus session and recovers during breaks. All visual states are just how the character looks at a given Energy value, which keeps the animation feeling organic rather than stepped.

### 7.0 Lifecycle & visibility — visible for the whole time the app is open

Wilt is a single small overlay widget (plus a tray icon and an on-demand settings window). The character is **on screen for the entire time the app is running** — from launch to quit — regardless of whether a Pomodoro is currently active and regardless of whether the user is actively looking at it. It is not something that appears only during a session and hides the rest of the time; it is a persistent small companion, and its whole value as an "encourage me to use the Pomodoro technique" nudge depends on being visible even when idle, unwatched, in the background.

- **App running, no session started (Inviting):** The overlay is visible, showing the idle "Inviting" state (see 7.2) — a gentle, low-energy prompt that invites the user to hit Start, rather than a tiredness cue (there's no session yet, so nothing to be tired from).
- **Session running (focus or break):** The overlay remains visible, animating through the Energy-driven states in 7.2.
- **Paused:** The overlay remains visible, frozen into a held "paused" pose (a subtle idle-breathing loop at the current Energy level — see 7.4), clearly distinguishable from the actively-animating state so the user can tell at a glance that the timer isn't running.
- **Session/Pomodoro finished:** The character plays a brief closing animation (e.g., the Reset "ready" beat, or a wind-down beat if the user manually hit Finish early — see 9.1) and then settles back into the "Inviting" state rather than disappearing — it stays on screen, ready to encourage the next session.
- **App closed/quit:** This is the only point the overlay disappears entirely. Closing the overlay window (if the user exposes a close affordance) should be treated as quitting the app, not as "hiding the character until the next session," to avoid a confusing in-between state.

The overlay is deliberately small (Section 9.1) precisely so that being always-present doesn't read as intrusive — it's meant to sit quietly in peripheral vision like a sticky note, not demand attention.

### 7.1 Energy curve

- At the start of a focus session, Energy = 100.
- Energy decays over the session duration. Default curve is linear, but the easing should be tunable (see 7.3) — a slight ease-in (slow initial decay, faster drop in the last third) reads as more natural than strict linear decay and is the recommended default.
- During a short/long break, Energy recovers back toward 100 over the break duration.
- Pausing the timer freezes Energy at its current value (the character holds its current tiredness, e.g. a subtle idle-tired loop, rather than continuing to animate).

### 7.2 Visual states (subtle realism art direction)

Character is a simple, semi-realistic person at a desk, rendered in a clean flat/vector style (think: minimal editorial illustration, not cartoon-mascot exaggeration). Consistent with the "subtle realism" direction, expressions and posture changes should be small and believable rather than slapstick.

| Energy range | State name | Visual behavior |
|---|---|---|
| N/A (app open, no session running) | Inviting | Character is idle at the desk, glancing toward the Start control, an occasional stretch or "ready when you are" gesture — designed to nudge the user toward starting a session, not to signal fatigue |
| 100–75 | Fresh | Upright posture, normal blink rate, occasional small productive gestures (typing, sipping coffee) |
| 75–50 | Focused | Slight forward lean, steady blink rate, in-the-zone body language |
| 50–25 | Tiring | Shoulders start to drop, blink rate slows, an occasional stretch/neck-roll micro-animation |
| 25–10 | Tired | Visible slouch, yawns periodically, rubs eyes, rests chin on hand |
| 10–0 | Exhausted | Head nodding, eyes drooping/closing, slow-motion movements — clearly "done," signaling a break is overdue |
| Break (recovering) | Recovering | Stands up/stretches, drinks water, gradually straightens posture as Energy climbs back up |
| Session complete (break → focus transition) | Reset | Brief upright "ready" animation (small confident nod) at the start of a new focus session |

### 7.3 Recommended implementation approach

Use **Rive** (rive.app) for the character. Rive's state-machine model supports exactly this pattern natively: a single numeric input (e.g. `energy`, 0–100) drives continuous blend animation across an artist-authored rig, rather than switching between disconnected sprite states. This gives smooth, organic-feeling transitions with a reasonable art/animation budget for a solo indie project, and Rive ships a lightweight native runtime that embeds well in a Windows app (via its C++/Win32 or .NET runtime) with low CPU/GPU overhead — important since this app runs continuously in the background.

Alternative if Rive's learning curve is too high for v1: a hand-authored sprite-sheet/frame-based animation (via a WPF `Storyboard` or a simple frame-timer) with 5–7 discrete states and crossfades between them. This is more work to make feel organic but is simpler to build with an artist who only delivers static frames.

### 7.4 Built-in action layer — the character is not just a tiredness gauge

Energy state (7.2) controls posture and overall "vibe," but the character should not be a single calm loop that only ever slouches lower. Layered on top of the Energy-driven base pose is a library of short, randomized **action clips** that fire periodically throughout a session, so the character reads as alive rather than as a progress bar wearing a costume:

- **Focus-session actions** (weighted more likely at higher Energy, but not exclusively): typing, sipping coffee, jotting a note, glancing at a second monitor, adjusting glasses, a quick stretch.
- **Tiring/tired-range actions** (weighted more likely as Energy drops): yawning, rubbing eyes, resting chin on hand, slow neck-roll, staring blankly for a beat.
- **Break actions:** standing up, stretching arms overhead, walking off-screen and back, drinking water, looking out a window.
- **Idle/paused actions:** a slow breathing loop plus an occasional small fidget (foot tap, pen twirl) so "paused" still feels alive rather than frozen solid.
- **"Inviting" actions** (app open, no session active): glancing at the Start control, a small beckoning gesture, tidying the desk — friendly, low-energy encouragement rather than tiredness cues, since Energy/tiredness is only meaningful once a session is running.

Implementation notes:
- Actions are selected from a weighted random pool keyed to the current Energy band (7.2) and session phase (focus/break/paused), with a minimum interval between action clips (e.g., every 20–45 seconds) so the character doesn't look twitchy.
- In Rive, this maps naturally to a second state-machine layer (or a set of one-shot animation "triggers") blended over the continuous Energy-driven base layer, rather than replacing it.
- Action selection should avoid immediate repeats (don't play "sip coffee" twice in a row) to keep the loop feeling natural over a full workday.
- This is a **Must have** for launch, not a stretch goal — it's what keeps the app watchable/charming beyond the first day of novelty, and it's core to "the character is not always calm."

## 8. Feature Scope (MVP)

Prioritized using MoSCoW.

**Must have**
- Core Pomodoro timer logic: configurable focus duration (default 25 min), short break (default 5 min), long break (default 15 min after N sessions, default N=4).
- **The character overlay is visible for the entire time the app is running** (see 7.0) — idle "Inviting" state when no session is active, live Energy-driven state during a session — not just while a Pomodoro is in progress. It disappears only when the app itself is quit.
- An integrated clock/time display built into the character widget itself (see 9.1) with **Start**, **Pause/Resume**, and **Finish** controls attached directly to it — the user should never need to leave the overlay to control the current session.
- Energy-driven animation as described in Section 7, synced to the active timer phase.
- Built-in randomized action layer (Section 7.4) so the character performs varied idle/working/tired actions, not a single calm loop.
- System tray icon with quick actions (start/pause, open settings, quit) and current phase/time-remaining on hover tooltip.
- Settings window: durations for focus/short break/long break, sessions-until-long-break, overlay size, overlay opacity, click-through toggle (see 9.3), sound on/off, launch-on-startup toggle, character skin.
- **Settings apply dynamically** — changes take effect immediately, including mid-session (see 9.5); nothing requires an app restart or session restart to take effect.
- End-of-session and end-of-break audio cue (soft, non-jarring by default; user-replaceable/mutable).
- Session persistence across app restarts (if the app is closed mid-session, restore or gracefully reset — decide behavior, default: reset to idle and notify).

**Should have**
- Local session history/stats (sessions completed today/this week), viewable in a simple stats panel — no accounts, stored locally only.
- Multiple character "skins" bundled for free (e.g., 2–3 alternate simple character designs) to give users a choice without needing monetization infrastructure.
- Multi-monitor awareness: overlay remembers which monitor/position it was placed on.
- Keyboard shortcut(s) for start/pause/skip (global hotkey).

**Could have**
- Idle detection (if the user is AFK, offer to auto-pause) — nice affordance, not essential to the core loop.
- Light/dark variants of the character to match system theme.
- Ambient embellishments during "Exhausted" state (e.g., a small "coffee cup" or "zzz" motif) as an optional toggle for users who want more whimsy — kept off by default to preserve "subtle realism."

**Won't have (v1)**
- Cloud sync, accounts, cross-device history.
- Paid content, in-app purchases.
- Task/project integration.
- Non-Windows builds.

## 9. UX Requirements — The Overlay

This is the part of the spec that most differentiates Wilt from a normal app and deserves the most care.

### 9.1 Behavior

- The overlay is a small, borderless, non-rectangular-looking window (transparent background, only the character, its immediate shadow/desk, and the clock element below are visible — no window chrome, no title bar).
- It stays on top of all other windows, including full-screen apps where feasible (see 10.3 for the Windows-specific caveat on exclusive full-screen apps like games).
- It does not steal keyboard focus when it appears, updates, or animates. Clicking through to the app underneath should be possible when the user enables "click-through" mode (Section 9.3).
- Default size is small (recommend ~120–180px character height) so it reads as a peripheral companion, not a window competing for attention. User-resizable within a defined range (e.g., 80–300px) in settings.
- Draggable by left-click-and-hold directly on the character; position is remembered per monitor.
- Right-click (or hover) reveals a minimal control strip: skip, open settings, quit — kept separate from the primary clock controls described below so the surface stays clean until the user actually wants more than start/pause/finish.

### 9.1.1 The clock element

Because the character overlay is on screen at all times the app is running (7.0), the Pomodoro's start/pause/finish controls live directly on the widget rather than in a separate window the user has to go find:

- A compact clock/dial sits with the character — recommended treatment is a thin circular progress ring around or beneath the character (time remaining depletes the ring, echoing the Energy depletion of the character itself) with the remaining time as text in the center or just below. While no session is running (the "Inviting" state), the ring is simply empty/full-idle rather than showing a countdown.
- **Start:** a clear play affordance shown directly on the widget during the "Inviting" state (and echoed on the tray icon) — this is the primary call to action of the always-visible character.
- **Pause/Resume:** a single toggle control on the clock element; pausing freezes both the timer and the character's Energy (7.0).
- **Finish:** ends the current session immediately (counts it as complete rather than abandoned, or prompts to confirm if ended very early — exact threshold is a design decision for build time), triggering the character's closing animation and hiding the overlay per the lifecycle in 7.0.
- These three controls should be reachable with a single click each directly on the clock element, without needing the hover-only control strip in 9.1 — they are the primary interaction, not a secondary one.

### 9.2 Placement & multi-monitor

- Default placement: bottom-right corner of the primary display, inset from the edges (configurable margin).
- If the user drags it to a secondary monitor, remember that monitor + relative position; if that monitor is later disconnected, fall back gracefully to the primary display rather than rendering off-screen.

### 9.3 Click-through mode

- Optional setting: when enabled, mouse clicks pass through the overlay to whatever is underneath it, and only a designated tray icon / hotkey is used for control. This matters for users who position the character over content they still want to interact with (e.g., a corner of their browser).
- Implemented via `WS_EX_TRANSPARENT` combined with `WS_EX_LAYERED`; the control strip (9.1) should temporarily suspend click-through while visible/hovered so users can still reach it.

### 9.4 Distraction budget

Because this app is designed to sit on screen all day, every animation choice should be evaluated against a "would this pull my eye away from my work" test. Blinking, breathing, and posture shifts should be slow and subtle in the Fresh/Focused states, escalating gradually — the character should be something the user *checks in on*, not something that flickers in peripheral vision uninvited.

### 9.5 Dynamic settings

Because the app is meant to be tweaked in-flow rather than configured once and forgotten, settings changes should apply live:

- Changing focus/break durations while a session is running rescales cleanly: the recommended behavior is to preserve elapsed **proportion** (e.g., if the user is 40% through a 25-minute session and changes the duration to 30 minutes, the session becomes 40% through 30 minutes) rather than either ignoring the change until next session or causing a jarring jump in Energy.
- Visual settings (overlay size, opacity, character skin, click-through) apply immediately and live, with no need to reopen or restart the overlay.
- Sound and click-through toggles apply on the very next event (next tick, next click) with no restart required.
- The only setting that reasonably requires a restart is launch-on-startup (a Windows registry/startup-folder change), which should be communicated as such if it's not instant.

## 10. Technical Requirements

### 10.1 Platform & stack

- **Target OS:** Windows 10 (21H2+) and Windows 11.
- **Recommended stack:** WPF or WinUI 3 (.NET 8) for the shell (tray icon, settings window, timer logic, window management), with the Rive .NET/native runtime (or equivalent) embedded for character rendering. This combination keeps the always-on-running footprint small compared to an Electron-based approach, which matters more here than in a typical app because Wilt is expected to run continuously for an entire workday.
- If team familiarity or timeline favors a web-tech stack instead, Tauri (Rust shell + web view for rendering) is an acceptable alternative that still keeps resource usage far lower than Electron — but native WPF/WinUI is preferred for the tightest control over always-on-top/layered-window behavior, which are raw Win32 concerns.

### 10.2 Always-on-top overlay implementation

- Use the `WS_EX_TOPMOST` extended window style (in WPF, `Topmost="True"`) to keep the overlay above other windows.
- Use `WS_EX_LAYERED` with per-pixel alpha (via WPF's `AllowsTransparency` + transparent background, or a `DwmExtendFrameIntoClientArea` approach) so only the character silhouette is visible, not a rectangular window.
- Use `WS_EX_NOACTIVATE` so the window never steals focus when shown, redrawn, or clicked outside its control strip.
- Add `WS_EX_TRANSPARENT` conditionally for the click-through setting (9.3), toggled at runtime.

### 10.3 Known Windows constraints to document for users

- Applications running in **exclusive full-screen mode** (many games, some video players) can legitimately cover topmost windows — this is an OS-level limitation, not a bug. The PRD should set the expectation (in-app and in any store listing) that the overlay is guaranteed visible over windowed and borderless-windowed apps, with full-screen-exclusive apps as a known exception.
- Should be tested against common "always-on-top-unfriendly" scenarios: full-screen presentations (PowerPoint slideshow mode), full-screen video (YouTube/Netflix in a browser), and remote desktop/screen-share sessions, since these are realistic contexts where a knowledge worker's overlay might get hidden or (in screen-share) unintentionally visible to others.

### 10.4 Performance / non-functional requirements

- Idle CPU usage target: under 1–2% average on a mid-range machine while animating continuously — this is a background app running all day, so efficiency is a first-class requirement, not an afterthought.
- Memory footprint target: under ~150MB resident.
- Animation should target 30fps (60fps is unnecessary for the subtlety of motion described here and would waste battery/CPU on laptops with no perceptible benefit).
- App should launch in under 2 seconds and be safe to set as a startup app without noticeably affecting boot time.
- Battery-awareness: on laptops running on battery, consider throttling animation frame rate slightly (e.g., 15–20fps) — a "should have," not launch-blocking.

### 10.5 Data & privacy

- All data (settings, local session history) stored locally (e.g., a local SQLite file or simple JSON/local app-data store). No account creation, no telemetry beyond an optional, clearly-disclosed, opt-in crash-reporting mechanism if the developer wants basic reliability signal.
- No network requests required for the app to function at all in v1.

## 11. Success Metrics

Since this is scoped as a free indie project with no monetization in v1, "success" should be framed around adoption and engagement quality rather than revenue:

- Organic installs / downloads (from itch.io, GitHub releases, Microsoft Store, or wherever it's distributed).
- Day-7 and Day-30 retention (does someone still have it running a week/month later — a reasonable proxy given it's a background utility, not a session-based app).
- Qualitative signal: unsolicited social shares, reviews, or community posts referencing the "tired character" hook specifically — this is the product's core bet, so its reception is the clearest read on whether the concept works.
- Crash-free session rate (should be very high — this app has no excuse to be unstable given its narrow scope).

## 12. Risks & Open Questions

| Risk / question | Notes |
|---|---|
| Will the animation read as charming rather than gimmicky after repeated daily exposure? | Mitigate by keeping motion subtle (Section 9.4) and shipping alternate skins early so fatigue with one character doesn't mean fatigue with the product. |
| Art/animation production is the highest-skill, highest-cost part of the build for a solo/small indie team. | Consider commissioning a single well-executed Rive rig rather than spreading budget across many characters for v1; polish one before adding variety. |
| Always-on-top + layered + non-activating windows have historically had edge-case bugs across different Windows versions and DPI/multi-monitor setups. | Budget explicit QA time across multi-monitor and mixed-DPI configurations; this is the highest technical-risk area of the app, more so than the timer logic itself. |
| Full-screen-exclusive app coverage (games, some video) is a hard OS limitation. | Set expectations clearly in product copy rather than over-promising "always visible, no matter what." |
| Distribution channel decision (Microsoft Store vs. direct download/installer vs. itch.io) affects packaging and update mechanism. | Needs a decision before build; Microsoft Store adds review/certification overhead but built-in discovery and auto-update; direct distribution is faster to ship but requires building your own updater. |

## 13. Future Considerations (explicitly out of scope for v1)

- Optional paid character packs or cosmetic themes if the free version gains traction (freemium was considered and deliberately deferred per current scope, but the architecture — modular character "skins" — should not preclude this later).
- macOS port (the always-on-top/layered-window logic would need a full native rewrite using `NSWindow` levels, since this is Win32-specific).
- Adaptive session lengths based on historical focus patterns.
- Optional ambient soundscape that also "tires" alongside the character (e.g., background sound subtly fading).

## 14. Suggested Milestones

1. **Prototype (2–3 weeks):** Timer logic + always-on-top/layered/click-through overlay window working with a placeholder static image, to de-risk the Windows overlay mechanics (the highest technical risk) before investing in animation.
2. **Core animation (3–5 weeks):** Rive character rig integrated, energy-driven state machine wired to real timer state, one complete character skin, subtle-realism art direction finalized.
3. **MVP polish (2 weeks):** Settings window, tray icon, sounds, session persistence, multi-monitor handling.
4. **Private beta (1–2 weeks):** Small group of target users (freelancers/knowledge workers) testing across different monitor/DPI setups and daily workflows; validate the distraction budget (9.4) and the emotional read of the animation.
5. **Public launch:** Ship on chosen distribution channel(s); instrument the success metrics in Section 11.

---

*This PRD reflects a Windows-only, free-indie-project scope as specified. Sections 12–13 flag the decisions (distribution channel, art budget, future monetization) that most affect timeline and should be resolved before or during the prototype milestone.*
