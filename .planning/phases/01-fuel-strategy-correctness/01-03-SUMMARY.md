---
phase: 01-fuel-strategy-correctness
plan: 03
subsystem: data-service
tags: [csharp, fuel-strategy, dataservice, config, wpf, slider, dotnet]

requires:
  - phase: 01-02
    provides: DataService.cs with FUEL-01 and FUEL-02 fixed; 7 Category=Fuel tests green

provides:
  - FuelStrategyConfig class with SafetyMarginLaps (default 1.0) in OverlayConfig.cs
  - AppConfig.FuelStrategy property with null-guard in ConfigService.Load()
  - DataService constructor injection pattern (FuelStrategyConfig replaces SAFETY_MARGIN_LAPS const)
  - Safety margin slider in FuelStrategy settings panel (range 0-3, step F1), persisted to config.json
  - FUEL-03 completed: safety margin fully configurable without restart

affects:
  - Phase 2 UI work (any overlay settings pattern builds on this AddSlider/constructor injection approach)

tech-stack:
  added: []
  patterns:
    - "Constructor injection of sub-config into service (FuelStrategyConfig into DataService)"
    - "??= null-guard pattern in ConfigService.Load() for new sub-config objects"
    - "AddSlider + _configService.Save() inline lambda for persisted settings in MainWindow"

key-files:
  created: []
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/ConfigService.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/DataService.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/OverlayManager.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/MainWindow.xaml.cs

key-decisions:
  - "Constructor injection over property injection for FuelStrategyConfig — DataService is constructed once at startup, injection at construction time guarantees the field is always initialized"
  - "Slider range 0-3 laps with F1 format — covers sprint (0.5) through endurance (2.0) use cases without allowing unreasonable values"
  - "Config persisted immediately on slider change via _configService.Save() — matches existing overlay settings persistence pattern throughout MainWindow"

patterns-established:
  - "Sub-config constructor injection: pass AppConfig sub-object to service constructor; store as readonly field; use directly in formulas"
  - "Settings slider: AddSep() + AddSlider(label, current, min, max, onChange lambda, format) — onChange updates config and calls _configService.Save()"

requirements-completed: [FUEL-03]

duration: 15min
completed: 2026-05-19
---

# Phase 01 Plan 03: FUEL-03 Configurable Safety Margin Summary

**FuelStrategyConfig constructor-injected into DataService replacing SAFETY_MARGIN_LAPS const; slider in FuelStrategy settings panel (0-3 laps, persisted to config.json) — FUEL-03 complete**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-19T12:35:00Z
- **Completed:** 2026-05-19T13:00:00Z
- **Tasks:** 3 (2 auto + 1 human-verify checkpoint)
- **Files modified:** 5

## Accomplishments
- `FuelStrategyConfig` class added to `OverlayConfig.cs` with `SafetyMarginLaps` property (default 1.0); `AppConfig.FuelStrategy` wired throughout
- `DataService` constructor updated to accept `FuelStrategyConfig`; `private const double SAFETY_MARGIN_LAPS = 1.0` removed; both formula references replaced with `_fuelConfig.SafetyMarginLaps`
- `MainWindow.xaml.cs` FuelStrategy settings block now exposes a slider "Marge de securite (tours)" (range 0.0–3.0, F1 step); value persists to `%APPDATA%\DouzeAssistance\config.json` on change
- Human-verify checkpoint passed: slider visible at default 1.0, value 0.5 persisted after close/reopen, config.json updated correctly

## Task Commits

Each task was committed atomically:

1. **Task 1: Add FuelStrategyConfig and thread through model and services** - `93a92f2` (feat)
2. **Task 2: Add safety margin slider to FuelStrategy settings panel** - `2a85b98` (feat)
3. **Task 3: Verify safety margin slider in settings UI** - human-verify checkpoint (approved by user)

## Files Created/Modified
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs` — Added `FuelStrategyConfig` class and `AppConfig.FuelStrategy` property
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/ConfigService.cs` — Added `cfg.FuelStrategy ??= new FuelStrategyConfig()` null-guard in `Load()`
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/DataService.cs` — Removed `SAFETY_MARGIN_LAPS` const; added `_fuelConfig` readonly field; updated constructor; replaced two formula references
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/OverlayManager.cs` — Updated `new DataService(_reader)` to `new DataService(_reader, config.FuelStrategy)`
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/MainWindow.xaml.cs` — Added `if (key == "FuelStrategy")` block with `AddSlider` for safety margin

## Decisions Made
- Constructor injection chosen over property injection: `DataService` is instantiated once at startup via `OverlayManager`, so injecting at construction time is cleaner and ensures the field is always initialized
- Slider range 0–3 laps with F1 format covers realistic scenarios (0.5 sprint, 1.0 default, 1.5–2.0 endurance) without allowing absurd values
- Config persisted immediately on each slider change (`_configService.Save(_config)` inline) — consistent with all other overlay settings sliders in `MainWindow`

## Deviations from Plan
None — plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three FUEL bugs are resolved: FUEL-01 (leader laps), FUEL-02 (SC exclusion), FUEL-03 (configurable safety margin)
- Phase 1 (Fuel Strategy Correctness) is complete — 7/7 Category=Fuel tests green
- Phase 2 (UI) can proceed; the constructor injection pattern and AddSlider/save lambda established here are reusable for any new overlay settings

## Self-Check: PASSED

Commits verified in git log:
- `93a92f2` feat(01-03): add FuelStrategyConfig and thread through model and services
- `2a85b98` feat(01-03): add safety margin slider to FuelStrategy settings panel

Build: 0 errors (2 warnings — pre-existing ClosedXML version mismatch, unrelated)
Tests: 7/7 Category=Fuel passed

---
*Phase: 01-fuel-strategy-correctness*
*Completed: 2026-05-19*
