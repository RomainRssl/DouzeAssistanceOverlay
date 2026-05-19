# Feature Landscape

**Domain:** Real-time racing simulator overlay — Le Mans Ultimate / rFactor 2 endurance racing
**Researched:** 2026-05-19
**Milestone focus:** Fuel/energy strategy (multi-class), UI customization, performance optimization

---

## Table Stakes

Features users expect from any serious sim racing overlay. Missing = product feels incomplete or amateurish.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Fuel to add at next pit (liters/%) | Core reason to use an overlay | Low | Already exists; issue is accuracy |
| Laps of fuel remaining current stint | Immediate safety net | Low | Already exists |
| Average fuel/energy consumption per lap (rolling) | Baseline for all fuel math | Low | Rolling avg over last N laps preferred |
| Race laps/time remaining | Context for all strategy decisions | Low | Currently broken: ignores global leader |
| Current position + class position | Driver orientation | Low | Already exists |
| Multi-class standings with class colors | Le Mans format standard | Medium | Already exists |
| Relative display (cars ±5 positions) | Proximity/gap awareness | Medium | Competitive table stakes for LMU |
| Tire compound + wear per wheel | Stint length awareness | Medium | Already exists (basic) |
| Lap delta (vs best / vs target) | Pace management | Medium | Already exists (chrono panel) |
| Flag status (green/yellow/SC/red/finish) | Safety and rule compliance | Low | Must be prominent and instant |
| Pit countdown (distance to pit entry) | Timing for brake/fuel call | Low | Already exists |
| Session clock (elapsed / remaining) | Endurance race orientation | Low | Should display HH:MM:SS |

---

## Differentiators

Features that set the product apart. Not universally expected, but high value for LMU endurance racing specifically.

### Fuel Strategy — Multi-Class Race Leader Correction (CRITICAL DIFFERENTIATOR)

**Current gap:** The existing app uses a naive `RaceLapsRemaining` that ignores which car is actually leading the race globally. In LMU's timed+lap format, the race ends when the **global leader** (fastest class, P1 overall) crosses the line after the timer expires — every other class must complete that same lap count, which can add 0–2 extra laps versus a naïve estimate.

**How top tools solve this (TinyPedal model, HIGH confidence):**

TinyPedal implements an "adaptive race length" algorithm with a `finish_time_difference_threshold` (default 200 seconds, roughly one Le Mans lap). The logic:

1. Identify the **global race leader** (not class leader) from the standings.
2. Compute the leader's estimated laps remaining based on their pace and race time remaining.
3. Compute the player's own laps remaining at their own pace.
4. If the time difference between leader's estimated finish and player's estimated finish is **below the threshold** → use the player's own pace to determine laps remaining.
5. If the time difference is **above the threshold** → the leader will still be lapping; use the leader's pace to determine race end, then calculate how many player laps that represents.
6. Add a **safety car buffer**: typically +1 lap of fuel (or configurable as a fraction). Safety car periods burn fuel at ~30-50% of green-flag rate but laps still count, so net fuel saved is partially offset by the extra lap exposure.

**iRacing AutoFuel model (HIGH confidence):** Calculations are based on the leader's average lap time, predicted lap count from that pace, and your rolling average fuel consumption at last pit exit. Caution laps are excluded from consumption averages. Users set a "margin laps" integer (extra laps of safety buffer).

**Crew Chief model:** Uses a percentile setting (50 = median consumption, 100 = max observed) as the safety factor, plus a "margin laps" adder. Voice command "fuel to the end" triggers an immediate recalculation and sends fuel amount to the pit crew.

**Recommended implementation for LMU multi-class:**

```
race_end_lap = leader_laps_completed + ceil(time_remaining / leader_avg_lap_time)
player_laps_to_go = race_end_lap - player_laps_completed
fuel_to_finish = (player_laps_to_go * player_avg_consumption) - current_fuel
fuel_to_add = fuel_to_finish + (safety_margin_laps * player_avg_consumption)
```

Where `safety_margin_laps` is user-configurable (default 1.0 for endurance). This aligns with the multi-class "white flag" rule: the race ends for you when the overall leader's next crossing happens after the timer.

| Feature | Why Valuable | Complexity | Notes |
|---------|--------------|------------|-------|
| Global leader-based laps-remaining calculation | Prevents running dry on last lap | High | Must use rF2 shared memory `mLeaderLapsComplete` + `mCurrentET` + `mEndET` |
| Per-class leader tracking | Shows GT3/LMP2/Hypercar leader gaps separately | Medium | Useful for class strategy |
| Safety car fuel margin (configurable, default +1 lap) | SC periods create extra lap risk | Medium | SC detection via session flags |
| VE (Virtual Energy) to add — Hypercar-aware | VE ≠ fuel in LMU Hypercars; ratio matters | High | VE% × fuel_ratio = physical fuel; predict VE needed for N more laps |
| Short-fuel calculation for final stint | When you CAN run less than a full tank | High | Activates when laps_to_go × consumption < tank_capacity |
| Fuel save target (L/lap to save) | Extend stint by reducing pace | Medium | "Save 0.3L/lap to skip a stop" display |
| Stint planner (laps per stint, stop count) | Pre-race/in-race planning | High | Show optimal stop count given consumption and race length |
| Safety car detection indicator | Trigger for fuel recalculation | Low | Flag state from shared memory |
| Driver minimum time warning (FIA endurance rule) | LMU WEC enforces min driving time per driver | Medium | Alert when approaching swap window |

### UI Customization — Panel Layout System

All major competitors (SimHub, RaceLab, TinyPedal) allow drag-and-drop panel positioning with a persistent saved layout. RaceLab adds per-panel transparency, font size, and color control. TinyPedal uses a settings JSON that users edit directly. SimHub has a full Dash Studio visual editor.

**Pattern in the ecosystem:** Panels unlock (edit mode) → drag to position → resize → lock → config saved to JSON. Resolution-specific profiles are increasingly expected.

| Feature | Why Valuable | Complexity | Notes |
|---------|--------------|------------|-------|
| Drag-and-drop panel repositioning (edit mode) | Primary ask from PROJECT.md | Medium | Toggle edit mode; mouse drag via `WS_EX_LAYERED` hit-testing |
| Free resize per panel | Adapt to different screen sizes/VR | Medium | Min size constraints to prevent data truncation |
| Per-panel show/hide toggle | Reduce clutter for sprint vs endurance | Low | Persist in config JSON |
| Opacity/transparency per panel | Blend with game HUD | Low | Already using `AllowsTransparency`; expose as slider |
| Multiple saved layout profiles | "VR layout" vs "2D layout" vs "endurance" | Medium | Different JSON profile per preset name |
| Theme presets (dark/light/custom) | Visual preference | Low | Already has ThemeManager; expose more presets |
| Per-element color override | Team colors, personal preference | Medium | Primary colors + accent colors configurable |
| Font size scaling per panel | Accessibility, screen distance | Low | Scale factor multiplier on existing sizes |

### Essential Data for 24h-Style Endurance Racing

| Feature | Why Essential | Complexity | Notes |
|---------|---------------|------------|-------|
| Total race elapsed time (HH:MM:SS) | Crew orientation in long races | Low | Already in shared memory |
| Current stint length (time + laps) | Driver fatigue, tire management | Low | Compute from last pit exit |
| Gap to class leader / P1 overall | Dual gap display for multi-class | Medium | From standings data |
| Gap to car ahead / behind (class-specific) | Race position defense/attack | Low | Standard relative data |
| Pit stop count (own + class rivals) | Strategic context | Low | Track per driver from standings |
| Weather / track conditions indicator | Tire compound decisions | Medium | rF2 exposes ambient/track temp, rain |
| Tire temperature per wheel (inner/mid/outer) | Compound window monitoring | Medium | Available in shared memory; TinyPedal shows this |
| Pit lane open/closed status | Endurance race rules (SC period pits) | Low | Flag state inference |
| Best lap in class / overall (live) | Pace reference | Low | From standings |
| Estimated next pit lap (based on current fuel) | Proactive planning | Medium | Derived from fuel math above |
| Blue flag warning (being lapped) | LMU rule compliance | Low | From shared memory `mFlag` or relative gap |

---

## Anti-Features

Features to deliberately NOT build for this milestone.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Full SimHub compatibility / plugin system | Scope explosion; LMU/rF2 is the entire target | Stay single-sim, go deep on LMU specifics |
| Remote/cloud strategy sync | Requires network stack, auth, server | Local shared memory only per PROJECT.md constraint |
| AI race engineer (voice strategy calls) | Crew Chief already does this well; duplication | Use Crew Chief alongside; focus on data display |
| Mobile companion app | Out of scope per PROJECT.md | Focus on WPF desktop + VR |
| Telemetry session recording & post-race analysis | TinyPedal and MoTeC do this; scope mismatch | Defer; in-race real-time data is the focus |
| Setup recommendations / car tuning data | MoTeC, Strat Calculator handle this | Out of scope for overlay |
| Integrated pit radio / voice recognition | System.Speech already present; don't expand | Keep voice assistance minimal |
| Web-based strategy calculator | External tools exist (strat-calculator.online) | Link out; don't replicate |
| Multi-driver stint scheduler | TheStintLink, iRacePlan handle this better | Out of scope for in-cockpit overlay |
| Per-car telemetry channels (FFT, damper freq) | MoTeC use case, not overlay use case | Out of scope |

---

## Feature Dependencies

```
Global leader identification
    → Multi-class laps-remaining calculation
        → Fuel to add (accurate)
            → Short-fuel mode (final stint)
            → Safety car buffer (configurable)
        → VE to add (Hypercar)
            → VE percentage prediction

Safety car detection
    → Fuel recalculation trigger
    → Pit lane open/closed indicator

Panel drag-and-drop (edit mode)
    → Per-panel resize
    → Layout profile save/load
        → VR profile vs 2D profile

Tire temperature per wheel
    → Compound window indicator
    → Stint length estimate refinement
```

---

## MVP Recommendation for This Milestone

Prioritize in order:

1. **Multi-class fuel calculation with global leader correction** — The single highest-impact fix. Users are running dry or over-fueling because the current math is wrong.
2. **Virtual Energy to add for Hypercar** — LMU's primary class is Hypercar; VE math is broken per PROJECT.md.
3. **Drag-and-drop panel positioning + resize** — Explicitly requested in PROJECT.md active tasks.
4. **Safety car fuel buffer (configurable)** — Correctness requirement for 24h races.
5. **Layout profiles** — VR vs 2D is a real use case in this app.
6. **Per-panel opacity + theme presets** — Low complexity, high perceived polish.

Defer:
- **Driver minimum time warning** — Complex to implement (requires team session data); defer to next milestone.
- **Full tire temperature (inner/mid/outer per wheel)** — Useful but not in milestone scope; current basic tire data exists.
- **Weather display** — Available in shared memory but lowest priority for the three focus areas.

---

## Competitive Landscape Summary

| Tool | Fuel Strategy | Multi-Class | UI Customization | LMU/rF2 Native |
|------|--------------|-------------|-----------------|----------------|
| TinyPedal | Advanced (leader-aware, adaptive) | Yes (class + overall) | JSON config, drag-and-drop | Yes (primary target) |
| SimHub + MMO overlay | Good (plugin-based) | Yes | Full Dash Studio editor | Via plugin |
| RaceLab | Good | Partial | Per-panel transparency, color | Limited LMU support |
| Crew Chief | Excellent (voice-driven) | Partial | None (audio only) | Yes |
| Strat Calculator | Excellent (dedicated LMU tool) | Yes | Web UI (not in-game) | Yes |
| **Douze Assistance (this app)** | **Basic (broken multi-class)** | **Partial** | **Fixed positions** | **Yes (native)** |

The gap to close is fuel calculation accuracy (TinyPedal level) and UI flexibility (SimHub level), while retaining the VR-native advantage that no competitor currently matches.

---

## Sources

- TinyPedal GitHub: https://github.com/TinyPedal/TinyPedal (HIGH confidence — open source, active 2024-2025)
- TinyPedal Studio-397 Forum thread (adaptive race length algorithm): https://forum.studio-397.com/index.php?threads/tinypedal-open-source-overlay-for-rf2-pacenotes-radar-ffb-deltabest-relative-fuel-calculator.71557/ (MEDIUM confidence)
- iRacing AutoFuel official docs: https://support.iracing.com/support/solutions/articles/31000169381-how-to-use-autofuel (HIGH confidence)
- iRacing AutoFuel announcement: https://www.iracing.com/introducing-iracing-auto-fuel/ (HIGH confidence)
- Crew Chief fuel algorithm forum: https://thecrewchief.org/archive/index.php/t-3286.html (MEDIUM confidence)
- LMU Strategy Guide (OverTake): https://www.overtake.gg/news/le-mans-ultimate-strategy-guide-how-to-plan-your-pit-stops.2759/ (MEDIUM confidence)
- LMU Virtual Energy guide: https://guide.lemansultimate.com/hc/en-gb/articles/13152376674191-What-is-Virtual-Energy-NRG (HIGH confidence — official LMU docs)
- Strat Calculator features: https://strat-calculator.online/features (MEDIUM confidence)
- RaceLab features: https://racelab.app/ (MEDIUM confidence)
- SimHub Dash Studio overlays: https://github.com/SHWotever/SimHub/wiki/Dash-Studio-Overlays (MEDIUM confidence)
- Smart Race Engineer (multi-class white flag): https://www.racestrategy.app/ (MEDIUM confidence)
- rF2 Shared Memory Plugin: https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin (HIGH confidence)
