---
phase: 02-ui-customization
plan: "03"
subsystem: ui
tags: [themes, json, theme-manager, wpf, csharp, preset-deploy]

# Dependency graph
requires:
  - phase: 02-ui-customization plan 01
    provides: ThemePresetTests.cs RED scaffold (EnsurePresetThemesExistIn contract)
provides:
  - ThemeManager.EnsurePresetThemesExist() — deploys 3 preset themes at startup
  - ThemeManager.EnsurePresetThemesExistIn(dir) — testable overload for unit tests
  - WritePresetIfAbsent() — no-overwrite pattern preserving user edits
  - 3 preset theme definitions: Racing Clair, Bleu Nuit LMP2, Minimaliste Mono
  - App.xaml.cs startup wiring — preset deploy before Load() call
affects:
  - 02-04 (ThemesTab auto-discovers JSON files; 4 themes now available)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - WritePresetIfAbsent: File.Exists check before write — preserves user-edited theme JSON
    - EnsurePresetThemesExistIn(dir) overload pattern — production vs test directory injection
    - C# raw string literals (triple-quote) for embedded JSON in Build* methods
    - Preset theme JSON uses nested colors/effects sections matching ApplyJson() parser

key-files:
  created: []
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Helpers/ThemeManager.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/App.xaml.cs

key-decisions:
  - "Preset JSON uses nested colors/effects sections (not flat) to match ApplyJson() parser — plan reference was inaccurate"
  - "EnsurePresetThemesExistIn(dir) overload enables hermetic unit test isolation without touching %AppData%"
  - "WritePresetIfAbsent ensures re-launch does not overwrite user-edited preset files"

patterns-established:
  - "WritePresetIfAbsent: guard with File.Exists before any preset write"
  - "Testable overload pattern: production method calls overload with default directory"

requirements-completed: [UI-03]

# Metrics
duration: 15min
completed: 2026-05-19
---

# Phase 02 Plan 03: Theme Preset Deploy Summary

**ThemeManager extended with EnsurePresetThemesExist() + 3 Build* methods that deploy racing-clair.json, bleu-nuit-lmp2.json, and minimaliste-mono.json at startup without overwriting user edits — wired in App.xaml.cs before Load().**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-19T16:10:00Z
- **Completed:** 2026-05-19T16:25:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added `EnsurePresetThemesExist()` and `EnsurePresetThemesExistIn(dir)` to ThemeManager with WritePresetIfAbsent no-overwrite pattern
- Implemented 3 preset themes as embedded JSON via C# raw string literals: Racing Clair (light), Bleu Nuit LMP2 (blue night), Minimaliste Mono (monochrome)
- Wired `EnsurePresetThemesExist()` in App.xaml.cs `OnStartup` after `EnsureDefaultThemeExists()` and before `Load()`
- ThemePresetTests compilation errors resolved — the method contract is now satisfied

## Task Commits

Each task was committed atomically:

1. **Task 1: Add EnsurePresetThemesExistIn + 3 Build* methods to ThemeManager** - `841ce35` (feat)
2. **Task 2: Wire EnsurePresetThemesExist in App.xaml.cs startup** - `730c549` (feat)

**Plan metadata:** committed with SUMMARY.md

## Files Created/Modified
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Helpers/ThemeManager.cs` - Added EnsurePresetThemesExist, EnsurePresetThemesExistIn, WritePresetIfAbsent, BuildRacingClair, BuildBleuNuitLmp2, BuildMinimalisteMono
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/App.xaml.cs` - Added EnsurePresetThemesExist() call after EnsureDefaultThemeExists() in OnStartup

## Decisions Made
- **JSON schema uses nested sections**: The plan's reference JSON showed a flat format, but the actual `ApplyJson()` parser reads from nested `colors` / `effects` sections. Used the correct nested format so themes are actually loadable at runtime.
- **EnsurePresetThemesExistIn(dir) overload pattern**: The production method delegates to the testable overload, enabling ThemePresetTests to run against a temp directory without touching %AppData%.
- **WritePresetIfAbsent**: `File.Exists(path)` guard before any write — user edits to preset files survive across app launches.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Preset JSON format corrected from flat to nested**
- **Found during:** Task 1 (examining ApplyJson() parser in ThemeManager.cs)
- **Issue:** Plan reference showed flat JSON (e.g., `"background": "#F5F5F5"` at top level), but `ApplyJson()` reads from `data["colors"]["background"]`. Flat format would silently load defaults for all fields.
- **Fix:** Used nested `colors` + `effects` sections matching the parser, with correct field names (`hypercar`/`lmp2`/`lmgt`/`gt3` not `classHypercar` etc.)
- **Files modified:** ThemeManager.cs (Build* methods)
- **Verification:** Reviewed against ApplyJson() line-by-line; main project builds 0 errors
- **Committed in:** 841ce35 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug in plan reference JSON schema)
**Impact on plan:** Essential for themes to actually apply at runtime. No scope creep.

## Issues Encountered

**Test project cannot build during this plan:** The test project (LMUOverlay.Tests) has pre-existing compile errors from VrProfileTests.cs, SnapGridTests.cs, and ColorOverrideTests.cs — RED scaffolds from 02-01 that reference `VrProfileHelper`, `SnapGridHelper`, `ColorOverrideHelper`, and `OverlaySettings.VrPosX/VrPosY/VrWidth/VrHeight`, none of which exist yet (they are implemented in 02-02 and 02-04). This prevents running ThemePresetTests in isolation. Verification was done by:
1. Confirming zero ThemeManager/ThemePreset errors in the build output
2. Confirming the main project builds with 0 errors
3. Inspecting test source — the 4 ThemePresetTests cases call only `EnsurePresetThemesExistIn()` which is now implemented

ThemePresetTests will go fully GREEN once 02-02 and 02-04 are implemented (making the test project compilable).

## Next Phase Readiness
- ThemeManager now deploys 4 themes on startup (endurance-noir + 3 presets)
- GetAvailableThemes() will return all 4 JSON files automatically
- ThemesTab auto-discovers JSON files — no ThemesTab changes required
- Ready for 02-04 (VR profiles) — the remaining Wave 1 plan

---
*Phase: 02-ui-customization*
*Completed: 2026-05-19*
