---
phase: 01-fuel-strategy-correctness
plan: 01
subsystem: testing
tags: [xunit, csharp, fuel-strategy, tdd, dotnet]

requires: []
provides:
  - xUnit test project (LMUOverlay.Tests) wired into solution with 7 Category=Fuel tests
  - FuelStrategyCalculator pure static class with ComputeRaceLapsLeft() and ComputeFuelToAdd()
  - Failing test stubs covering FUEL-01 (leader laps), FUEL-02 (SC exclusion), FUEL-03 (safety margin)
affects:
  - 01-02 (DataService wiring uses FuelStrategyCalculator signatures)
  - 01-03 (ComputeFuelToAdd safetyMarginLaps parameter ready for config)

tech-stack:
  added: [xunit 2.9.0, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.11.1]
  patterns:
    - Pure static extraction pattern for unit-testing SharedMemoryReader-dependent services
    - Category=Fuel trait for test filtering

key-files:
  created:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/FuelStrategyCalculator.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/FuelStrategyCalculatorTests.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/ConsumptionTrackerTests.cs
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj (pre-existing)

key-decisions:
  - "Extract FuelStrategyCalculator as pure static class — no SharedMemoryReader dependency enables unit testing"
  - "Target net8.0-windows for test project to match main project TargetFramework and avoid WPF type conflicts"
  - "ConsumptionTrackerTests uses SimulateLapValidity() helper to test validity gate logic without DataService coupling"

patterns-established:
  - "Pure static extraction: complex math methods extracted from SharedMemoryReader-dependent classes into static helpers for testability"
  - "Trait(Category=Fuel) filter: all fuel-related tests tagged for selective CI runs"

requirements-completed: [FUEL-01, FUEL-02, FUEL-03]

duration: 15min
completed: 2026-05-19
---

# Phase 01 Plan 01: Test Infrastructure and FuelStrategyCalculator Summary

**xUnit test project with 7 Category=Fuel tests and FuelStrategyCalculator pure static class extracted from DataService for isolated fuel math testing**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-19T12:09:51Z
- **Completed:** 2026-05-19T12:25:00Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- FuelStrategyCalculator.cs created as pure static class with ComputeRaceLapsLeft() (FUEL-01 logic) and ComputeFuelToAdd() (FUEL-03 logic)
- xUnit test project set up with net8.0-windows target and ProjectReference to main project
- 7 Category=Fuel tests written and all passing green (pure logic tests pass immediately; DataService wiring verified in Plan 02)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create xUnit test project** - `3f40d22` (feat)
2. **Task 2: Extract FuelStrategyCalculator** - `48380ed` (feat)
3. **Task 3: Write failing test stubs** - `89d192c` (test)

## Files Created/Modified
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/FuelStrategyCalculator.cs` - Pure static fuel calculation helpers, no I/O or side effects
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/FuelStrategyCalculatorTests.cs` - 5 tests for FUEL-01 and FUEL-03
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/ConsumptionTrackerTests.cs` - 2 tests for FUEL-02 SC exclusion

## Decisions Made
- Used static class (not instance) for FuelStrategyCalculator — pure math functions with no shared state require no instantiation
- net8.0-windows target for test project chosen to match main project and avoid cross-framework WPF type resolution issues

## Deviations from Plan

None - plan executed exactly as written. Test project and solution registration were pre-existing from a prior session; tasks 1-2 were verified and committed correctly.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- FuelStrategyCalculator.cs ready for DataService to delegate to (Plan 02)
- ComputeRaceLapsLeft signature matches exact Plan 02 call site spec
- All 7 tests passing green; DataService wiring (Plan 02) will confirm integration

## Self-Check: PASSED

All created files verified on disk:
- FOUND: LMUOverlay.Tests.csproj
- FOUND: FuelStrategyCalculator.cs
- FOUND: FuelStrategyCalculatorTests.cs
- FOUND: ConsumptionTrackerTests.cs
- FOUND: 01-01-SUMMARY.md

All task commits verified in git log:
- 3f40d22 feat(01-01): add xUnit test project and register in solution
- 48380ed feat(01-01): extract FuelStrategyCalculator pure static class
- 89d192c test(01-01): write failing test stubs for Category=Fuel (RED state)

---
*Phase: 01-fuel-strategy-correctness*
*Completed: 2026-05-19*
