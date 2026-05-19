---
phase: 01-fuel-strategy-correctness
plan: 02
subsystem: data-service
tags: [csharp, fuel-strategy, dataservice, bugfix, tdd, dotnet]

requires:
  - phase: 01-01
    provides: FuelStrategyCalculator.ComputeRaceLapsLeft() signature and 7 Category=Fuel test stubs

provides:
  - Fixed GetFuelData() using global P1 leader's laps (FUEL-01)
  - Fixed UpdateEnergyAndFuelTracking() excluding SC/VSC laps from consumption average (FUEL-02)
  - All 7 Category=Fuel tests green

affects:
  - 01-03 (DataService.cs further modified to wire in configurable safety margin)

tech-stack:
  added: []
  patterns:
    - "_wasSlowPhaseThisLap bool tracking field pattern (mirrors existing _wasInPitsThisLap)"
    - "Single-pass vehicle loop combining player class scan and P1 leader scan for efficiency"

key-files:
  created: []
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/DataService.cs

key-decisions:
  - "Merged the existing player-class scan loop with new P1 leader scan into one pass — avoids O(n²) and keeps both variables in one block"
  - "Early-exit from vehicle loop when both playerClass and leaderFound are true — maintains O(n) best case"
  - "mFinishStatus==1 (DNF) excluded from leader scan; mFinishStatus==3 (finished) included — legitimate race finisher is valid leader"
  - "_wasSlowPhaseThisLap reset at lap boundary carries over if SC still active — prevents a lap crossed during SC from being counted as normal"

patterns-established:
  - "SC/slow-phase tracking: use mYellowFlagState >= 0 (sbyte) guard mirroring _wasInPitsThisLap pattern"
  - "Leader scan: mPlace==1 && mFinishStatus!=1 is the canonical P1 filter for multi-class lap delta"

requirements-completed: [FUEL-01, FUEL-02]

duration: 6min
completed: 2026-05-19
---

# Phase 01 Plan 02: FUEL-01 & FUEL-02 Targeted Fixes in DataService Summary

**Two surgical fixes to DataService.cs: global P1 leader laps for race-laps-remaining (FUEL-01) and SC/VSC lap exclusion from consumption rolling average (FUEL-02) — all 7 Category=Fuel tests green**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-05-19T12:26:00Z
- **Completed:** 2026-05-19T12:32:00Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- `GetFuelData()` now scans vehicles in one pass to find both the player's class and the global P1 leader, then delegates to `FuelStrategyCalculator.ComputeRaceLapsLeft()` with the leader's `mTotalLaps`
- `UpdateEnergyAndFuelTracking()` now tracks `_wasSlowPhaseThisLap` via `mYellowFlagState >= 0` and excludes those laps from the consumption validity gate
- 7/7 Category=Fuel tests pass green after both fixes

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix FUEL-01 — leader scan in GetFuelData()** - `d34eca3` (feat)
2. **Task 2: Fix FUEL-02 — SC/VSC lap exclusion in UpdateEnergyAndFuelTracking()** - `90802ff` (feat)

## Files Created/Modified
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/DataService.cs` — Two targeted method changes: merged vehicle loop in GetFuelData(), and _wasSlowPhaseThisLap field + SC guard in UpdateEnergyAndFuelTracking()

## Decisions Made
- Merged the existing player-class vehicle scan loop with the new P1 leader scan into a single loop with early-exit — eliminates a second O(n) pass and keeps related logic together
- DNF leaders (`mFinishStatus==1`) excluded; finished leaders (`mFinishStatus==3`) included — a car that legitimately crossed the line is a valid lap-count reference

## Deviations from Plan
None — plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- DataService.cs is in a clean state for Plan 03 to add `FuelStrategyConfig` constructor injection and wire `safetyMarginLaps`
- Both FUEL-01 and FUEL-02 test categories are green; Plan 03 will add FUEL-03 integration test

## Self-Check: PASSED

All task commits verified in git log:
- d34eca3 feat(01-02): fix FUEL-01 — GetFuelData() uses global P1 leader's laps
- 90802ff feat(01-02): fix FUEL-02 — SC/VSC lap exclusion in UpdateEnergyAndFuelTracking()

Test results verified: 7/7 Category=Fuel tests passed (0 failures)

---
*Phase: 01-fuel-strategy-correctness*
*Completed: 2026-05-19*
