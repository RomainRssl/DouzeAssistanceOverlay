---
phase: 01-fuel-strategy-correctness
verified: 2026-05-19T14:00:00Z
status: human_needed
score: 4/5 must-haves verified (1 requires human)
human_verification:
  - test: "Open the application, navigate to the FuelStrategy (STRATEGIE) overlay settings panel, confirm the 'Marge de securite (tours)' slider is visible with default value 1.0, move it to 0.5, close and reopen settings, confirm the value persists at 0.5, then check %APPDATA%\\DouzeAssistance\\config.json for 'SafetyMarginLaps': 0.5"
    expected: "Slider visible at default 1.0, persists after close/reopen, config.json updated with the chosen value"
    why_human: "WPF UI runtime behavior cannot be verified programmatically — slider visibility and persistence require launching the application"
---

# Phase 01: Fuel Strategy Correctness Verification Report

**Phase Goal:** The driver gets an accurate fuel-to-add figure that accounts for the global race leader and excludes Safety Car laps from the consumption average
**Verified:** 2026-05-19T14:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (from ROADMAP.md Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | In a multi-class session, the fuel-to-add uses the global leader's remaining laps, not the player's own laps completed | VERIFIED | `DataService.GetFuelData()` scans all vehicles for `mPlace == 1 && mFinishStatus != 1`, stores `veh.mTotalLaps` as `leaderTotalLaps`, then calls `FuelStrategyCalculator.ComputeRaceLapsLeft(info.mMaxLaps, leaderTotalLaps, currentLap, ...)`. MultiClass test passes: leaderLap=5 → lapsLeft=5, not playerLap=3 → 7. |
| 2 | Laps driven behind Safety Car or VSC are excluded from the per-lap consumption average | VERIFIED | `DataService` field `_wasSlowPhaseThisLap` set when `info.mYellowFlagState >= 0`, included in `isValid` guard (`!_wasSlowPhaseThisLap`), reset at lap boundary with carry-over. SCLapExcluded and PostSCNormal tests pass. |
| 3 | The driver can set a configurable safety margin (default 1 lap) in settings, and that margin is visibly reflected in the fuel-to-add figure | PARTIAL (automated) / HUMAN NEEDED (UI) | `FuelStrategyConfig.SafetyMarginLaps` default=1.0 exists; `_fuelConfig.SafetyMarginLaps` used in both formula references; `if (key == "FuelStrategy")` block with `AddSlider` present in MainWindow. UI visibility requires human. |
| 4 | The fuel panel shows a correct value from lap 1 of a multi-class race (no need to wait for a pit stop cycle) | VERIFIED (with note) | `raceLapsLeft` is computed from the global leader's laps from the first tick, not gated by pit stops. `fuelDataReady` requires `_fuelSamples.Count >= 2` (2 completed valid laps, not a pit stop cycle). Leader-based lap count is correct from lap 1. |

**Score:** 4/5 automated truths verified. Truth 3 requires human for UI confirmation.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/FuelStrategyCalculator.cs` | Pure static class with `ComputeRaceLapsLeft()` and `ComputeFuelToAdd()` — no SharedMemoryReader | VERIFIED | File exists, 71 lines, pure static class, no SharedMemoryReader import, correct method signatures |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj` | xUnit test project targeting net8.0-windows | VERIFIED | File exists, correct TargetFramework, xUnit 2.9.0 + runner + SDK, ProjectReference to main project |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/FuelStrategyCalculatorTests.cs` | 5 tests covering FUEL-01 and FUEL-03 | VERIFIED | File exists, 5 `[Fact]` tests, all pass green |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/ConsumptionTrackerTests.cs` | 2 tests covering FUEL-02 SC exclusion | VERIFIED | File exists, 2 `[Fact]` tests, all pass green |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/DataService.cs` | Fixed `GetFuelData()` (FUEL-01) and `UpdateEnergyAndFuelTracking()` (FUEL-02); `_wasSlowPhaseThisLap` field present | VERIFIED | `_wasSlowPhaseThisLap` at line 41, SC guard at line 675, validity gate at line 681, reset at line 734 |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs` | `FuelStrategyConfig` class + `AppConfig.FuelStrategy` property | VERIFIED | `class FuelStrategyConfig` at line 952 with `SafetyMarginLaps = 1.0`; `AppConfig.FuelStrategy` at line 50 |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/ConfigService.cs` | `cfg.FuelStrategy ??= new FuelStrategyConfig()` null-guard | VERIFIED | Line 34: `cfg.FuelStrategy ??= new FuelStrategyConfig();` in Load() |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/OverlayManager.cs` | Passes `config.FuelStrategy` to `DataService` constructor | VERIFIED | Line 83: `new DataService(_reader, config.FuelStrategy)` |
| `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/MainWindow.xaml.cs` | `if (key == "FuelStrategy")` block with `AddSlider` for SafetyMarginLaps | VERIFIED (code) / HUMAN (runtime) | Lines 532-546: block exists, reads/writes `_config.FuelStrategy.SafetyMarginLaps`, calls `_configService.Save(_config)` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `LMUOverlay.Tests.csproj` | `LMUOverlay.csproj` | `ProjectReference Include` | WIRED | `<ProjectReference Include="..\LMUOverlay\LMUOverlay.csproj" />` confirmed |
| `FuelStrategyCalculatorTests.cs` | `FuelStrategyCalculator.cs` | static method call | WIRED | `FuelStrategyCalculator.ComputeRaceLapsLeft(...)` and `ComputeFuelToAdd(...)` called in all 5 tests |
| `DataService.GetFuelData()` | `FuelStrategyCalculator.ComputeRaceLapsLeft()` | direct static call with leader scan | WIRED | Line 840-841: `FuelStrategyCalculator.ComputeRaceLapsLeft(info.mMaxLaps, leaderTotalLaps, currentLap, sessionLeft, T_tour)` |
| `DataService.UpdateEnergyAndFuelTracking()` | `_wasSlowPhaseThisLap` bool field | `mYellowFlagState >= 0` guard | WIRED | Field declared line 41; set line 675; used in gate line 681; reset line 734 |
| `OverlayManager constructor` | `DataService constructor` | `new DataService(_reader, config.FuelStrategy)` | WIRED | Exact pattern confirmed at line 83 |
| `ConfigService.Load()` | `FuelStrategyConfig` | `??= new FuelStrategyConfig()` | WIRED | Line 34 confirmed |
| `MainWindow FuelStrategy settings block` | `config.FuelStrategy.SafetyMarginLaps` | `AddSlider` lambda | WIRED (code) | Lines 537, 542: reads and writes `SafetyMarginLaps`; `_configService.Save(_config)` called |

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| FUEL-01 | 01-01, 01-02 | Fuel calculation uses global race leader's laps (not player's) | SATISFIED | Leader scan in `GetFuelData()` + `ComputeRaceLapsLeft()` + MultiClass test passing |
| FUEL-02 | 01-01, 01-02 | Safety Car / VSC laps excluded from consumption average | SATISFIED | `_wasSlowPhaseThisLap` field + `mYellowFlagState` gate + `!_wasSlowPhaseThisLap` in isValid + SCLapExcluded test passing |
| FUEL-03 | 01-01, 01-03 | User can configure safety margin (default 1 lap) in settings | SATISFIED (code) / HUMAN (UI) | `FuelStrategyConfig`, constructor injection, `_fuelConfig.SafetyMarginLaps` in both formula references, slider block in MainWindow |

All 3 required requirements are claimed by plans and have corresponding implementation evidence. No orphaned requirements.

### Test Results

```
dotnet test "LMUOverlay.Tests/" --filter "Category=Fuel"

Total: 7 tests
Passed: 7
Failed: 0

- WindowClose_MarginReflectedInWindow           PASSED
- SafetyMargin_HalfMargin_LowerFuelToAdd        PASSED
- SingleClass_PlayerIsLeader_SameResult         PASSED
- SCLap_IsExcludedFromConsumptionAverage        PASSED
- MultiClass_LeaderAheadOfPlayer_UsesLeaderLaps PASSED
- PostSCLap_IsIncludedWhenSCInactive            PASSED
- TimeBased_FallsBackToSessionTime              PASSED
```

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| `DataService.cs` | `fuelDataReady` requires `_fuelSamples.Count >= 2` — fuel-to-add is 0 until 2 valid laps complete | Info | Expected behavior: driver needs at least 2 clean laps before getting a figure. Not a bug; "from lap 1" in SC-4 means no pit-stop-cycle wait, which is met. |

No TODO/FIXME/placeholder comments found in the modified files. No stub return patterns. No empty handlers.

### Human Verification Required

#### 1. Safety margin slider visible and persistent in UI

**Test:** Launch the application and open the FuelStrategy overlay settings section (labeled "STRATEGIE" in the panel list). Confirm the "Marge de securite (tours)" slider appears with range 0-3 and default value 1.0. Move the slider to 0.5, close the settings panel, reopen it, and confirm the value still reads 0.5.

**Expected:** Slider is visible at default 1.0, value 0.5 persists after close/reopen, and `%APPDATA%\DouzeAssistance\config.json` contains `"FuelStrategy": { "SafetyMarginLaps": 0.5 }`.

**Why human:** WPF UI slider visibility and config persistence require actually running the application. The code path from `AddSlider` to `_configService.Save()` is wired correctly, but runtime rendering of the settings panel cannot be verified statically.

### Gaps Summary

No blocking gaps found. All 7 automated tests pass. All three requirements (FUEL-01, FUEL-02, FUEL-03) have verified code implementations. The single outstanding item is the runtime UI check for the FUEL-03 settings slider — this is a human verification gate, not a code gap.

The `fuelDataReady` requiring 2 samples is worth noting for success criterion 4 context: the figure becomes available after 2 valid racing laps (not 2 pit stops), which satisfies the intent of "no need to wait for a pit stop cycle."

---

_Verified: 2026-05-19T14:00:00Z_
_Verifier: Claude (gsd-verifier)_
