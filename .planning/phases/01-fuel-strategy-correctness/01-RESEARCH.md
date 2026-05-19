# Phase 1: Fuel Strategy Correctness — Research

**Researched:** 2026-05-19
**Domain:** C# / .NET 8 / rF2SharedMemory — fuel strategy calculation inside DataService
**Confidence:** HIGH — based on direct source analysis of all relevant production files

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| FUEL-01 | Race laps remaining uses the overall race leader's position (all classes), not player's own laps completed | Leader found via `mVehicleScoring[i].mPlace == 1` scan over all vehicles; `mTotalLaps` on that entry gives the ground truth |
| FUEL-02 | Laps driven under Safety Car / VSC are detected and excluded from the per-lap consumption average | `mYellowFlagState >= 0` on `rF2ScoringInfo` is the session-wide SC signal; `mUnderYellow` on `rF2VehicleScoring` is per-vehicle confirmation |
| FUEL-03 | User-configurable safety margin (default 1 lap) added to fuel-to-add calculation; margin is visible in the displayed figure | `SAFETY_MARGIN_LAPS = 1.0` is a hardcoded private const today; must be promoted to a config field in `AppConfig` + exposed in settings UI |
</phase_requirements>

---

## Summary

The fuel strategy logic lives entirely inside `DataService.cs` (~2000 lines). Three bugs need correcting, all confined to two private methods: `UpdateEnergyAndFuelTracking()` (called once per tick from `UpdateTelemetryTrace()`) and `GetFuelData()` (called on every overlay refresh). The overlay (`FuelStrategyOverlay.UpdateData()`) is a pure consumer of `FuelData` DTO and **does not need to change** for any of the three requirements.

The multi-class bug (FUEL-01) is a two-line fix in `GetFuelData()`: replace `info.mMaxLaps - currentLap` (player's own laps) with `info.mMaxLaps - leaderTotalLaps`, where `leaderTotalLaps` is obtained by scanning `_reader.Scoring.mVehicles` for the entry where `mPlace == 1`. The SC exclusion bug (FUEL-02) is a bool-tagging addition to `UpdateEnergyAndFuelTracking()` — the same pattern already used for `_wasInPitsThisLap`. The configurable margin (FUEL-03) requires adding one `double` field to `AppConfig` (or a dedicated `FuelStrategyConfig` sub-object), wiring it into `DataService`, and exposing a numeric input in the settings panel.

**Primary recommendation:** Fix DataService in three incremental, isolated steps following the existing code patterns. Add no new abstraction layers unless extracting `ConsumptionTracker` is done as a pure refactor (no behavior change). The priority is correctness over elegance.

---

## Standard Stack

### Core (all already in project — no new dependencies)

| Component | Version / Location | Purpose |
|-----------|-------------------|---------|
| `DataService.cs` | `Services/DataService.cs` | Stateful calculation coordinator — all fixes land here |
| `rF2ScoringInfo` struct | `rF2SharedMemory/rF2Data.cs` | `mMaxLaps`, `mYellowFlagState`, `mNumVehicles`, `mCurrentET`, `mEndET` |
| `rF2VehicleScoring` struct | `rF2SharedMemory/rF2Data.cs` | `mPlace`, `mTotalLaps`, `mFinishStatus`, `mIsPlayer`, `mUnderYellow`, `mInPits` |
| `FuelData` DTO | `Models/OverlayConfig.cs` | Output shape — minimal additions needed |
| `AppConfig` | `Models/OverlayConfig.cs` | Root config object — add `FuelSafetyMarginLaps` field here |
| `ConfigService` | `Services/ConfigService.cs` | JSON persistence at `%APPDATA%/DouzeAssistance/config.json` |

### No New Packages Required

All needed capabilities exist: rF2SharedMemory structs, Newtonsoft.Json for config, WPF for UI controls.

---

## Architecture Patterns

### Existing Pattern: Pit Lap Exclusion (replicate for SC)

The pit-lap exclusion is the direct model for SC exclusion. Copy this pattern exactly:

```csharp
// EXISTING in UpdateEnergyAndFuelTracking() — pit exclusion
if (scr.mInPits != 0) _wasInPitsThisLap = true;

// At lap boundary:
bool isValid = !_wasInPitsThisLap && !_wasLapInvalidThisLap;

// Reset at lap start:
_wasInPitsThisLap = scr.mInPits != 0;
_wasLapInvalidThisLap = false;
```

Add `_wasSlowPhaseThisLap` using exactly the same lifecycle:
- Declare: `private bool _wasSlowPhaseThisLap;`
- Set: `if (_reader.ScoringInfo.mYellowFlagState >= 0) _wasSlowPhaseThisLap = true;`
- Include in gate: `bool isValid = !_wasInPitsThisLap && !_wasLapInvalidThisLap && !_wasSlowPhaseThisLap;`
- Reset at lap boundary: `_wasSlowPhaseThisLap = _reader.ScoringInfo.mYellowFlagState >= 0;`

### Existing Pattern: Player Scanning (replicate for leader scan)

`GetFuelData()` already scans `scoringVehicles` to find the player by `mIsPlayer != 0`. The leader scan is identical in shape:

```csharp
// EXISTING — player scan in GetFuelData()
for (int i = 0; i < numVeh; i++)
{
    var veh = scoringVehicles![i];
    if (veh.mIsPlayer != 0) { playerClass = rF2Helper.Str(veh.mVehicleClass); break; }
}

// NEW — leader scan (same pattern, different predicate)
int leaderTotalLaps = 0;
for (int i = 0; i < numVeh; i++)
{
    var veh = scoringVehicles![i];
    // mFinishStatus: 0=none, 1=DNF, 2=DQ, 3=finished — exclude finished/DNF from P1 search
    if (veh.mPlace == 1 && veh.mFinishStatus <= 0)
    {
        leaderTotalLaps = veh.mTotalLaps;
        break;
    }
}
```

### Existing Pattern: Config Extension

`AppConfig` already has many sub-objects. The margin setting should either go as a top-level double on `AppConfig` or in a new `FuelStrategyConfig` class. Given the project uses flat sub-objects for domain grouping, a lightweight dedicated class is appropriate:

```csharp
// In OverlayConfig.cs — new class
public class FuelStrategyConfig
{
    public double SafetyMarginLaps { get; set; } = 1.0;
}

// In AppConfig — add property
public FuelStrategyConfig FuelStrategy { get; set; } = new();
```

Config null-guard in `ConfigService.Load()`:
```csharp
cfg.FuelStrategy ??= new FuelStrategyConfig();
```

### Existing Pattern: DataService Receives AppConfig

`DataService` currently does NOT receive `AppConfig` — it uses only `SharedMemoryReader`. The safety margin is currently a `private const`. To make it configurable, two options exist:

**Option A (simpler, preferred):** Pass `FuelStrategyConfig` at construction time and store it. `OverlayManager` passes it from `AppConfig`.

```csharp
// DataService constructor change
public DataService(SharedMemoryReader reader, FuelStrategyConfig fuelConfig)
{
    _reader = reader;
    _fuelConfig = fuelConfig;
}
private readonly FuelStrategyConfig _fuelConfig;
```

**Option B:** Expose a mutable property on DataService. Simpler for the overlay manager, but less clean.

Option A is preferred — it mirrors how `VoiceService` and `LeaderboardService` already receive config sub-objects from `OverlayManager`.

### Recommended Project Structure (files touched)

```
LMUOverlay/Services/
├── DataService.cs              MODIFIED — three targeted fixes + config wiring
Models/
├── OverlayConfig.cs            MODIFIED — add FuelStrategyConfig class + AppConfig property
Services/
├── OverlayManager.cs           MODIFIED — pass FuelStrategyConfig to DataService constructor
├── ConfigService.cs            MODIFIED — add null-guard for FuelStrategyConfig
Views/
├── MainWindow.xaml.cs          MODIFIED — add safety margin numeric input in settings UI
```

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Detecting SC / yellow flag | Parse game log or network events | `mYellowFlagState` on `rF2ScoringInfo` — already read by `GetYellowFlagState()` method in DataService |
| Finding overall race leader | Sort all vehicles by lap count + distance | Scan `mVehicles` for `mPlace == 1` — `mPlace` is the global race position set by the sim |
| Config persistence | Custom binary format or registry | `ConfigService` + Newtonsoft.Json already handles `%APPDATA%/DouzeAssistance/config.json` |
| Settings UI widget | Custom numeric spinner | WPF `Slider` + `TextBox` — already used in many settings panels (e.g., `UpdateRateHz`, `MaxMessages`) |

---

## Common Pitfalls

### Pitfall 1: mPlace Is type `byte`, Not `int`
**What goes wrong:** Comparing `veh.mPlace == 1` works, but mPlace is `byte` — confirmed in `rF2Data.cs` line 296. No cast needed for equality, but be aware if arithmetic is involved.
**How to avoid:** Use direct equality `veh.mPlace == 1`.

### Pitfall 2: mFinishStatus Values
**What goes wrong:** A finished car (mFinishStatus == 3) may still have mPlace == 1 after crossing the line. If you include it, the leader lap count stops updating after they finish — which is correct behavior (that's the lap count we want). However for DNF (mFinishStatus == 1) you don't want to use them as P1.
**How to avoid:** Exclude `mFinishStatus == 1` (DNF) from the P1 search. `mFinishStatus == 3` (finished) is fine — they crossed the line legitimately.

### Pitfall 3: mYellowFlagState Is `sbyte`, Not `int`
**What goes wrong:** `mYellowFlagState` is declared as `sbyte` in rF2Data.cs. The value `-1` means "no yellow". Comparing `>= 0` correctly excludes -1 (no flag).
**Source confirmed:** `rF2Data.cs` line 237: `public sbyte mYellowFlagState;`
**How to avoid:** Use `_reader.ScoringInfo.mYellowFlagState >= 0` — the same condition used in `GetYellowFlagState()` which already returns this field.

### Pitfall 4: ScoringInfo Access Pattern
**What goes wrong:** `UpdateEnergyAndFuelTracking()` accesses `_reader.ScoringInfo` (a property of SharedMemoryReader). For SC detection, you need to read this during the tick, not just in `GetFuelData()`. However `_reader.ScoringInfo` is already accessible inside `UpdateEnergyAndFuelTracking()` — it only currently reads player-scoped data via `_reader.GetPlayerScoring()`.
**How to avoid:** Add `var info = _reader.ScoringInfo;` at the top of `UpdateEnergyAndFuelTracking()` alongside the existing `var scr = ps.Value;` pattern.

### Pitfall 5: SAFETY_MARGIN_LAPS Appears in Two Places
**What goes wrong:** `SAFETY_MARGIN_LAPS` is referenced in both the `FuelToAdd` formula AND in `windowClose` calculation: `double windowClose = L_real > 0 ? L_real - SAFETY_MARGIN_LAPS : 0;`
**How to avoid:** Replace BOTH references with `_fuelConfig.SafetyMarginLaps`. Don't miss the `windowClose` line.

### Pitfall 6: Leader Scan Runs Each GetFuelData() Call
**What goes wrong:** `GetFuelData()` is called at every overlay refresh (30 Hz). The leader scan is O(n) over all vehicles (max 128 in rF2/LMU). At 30 Hz with 60 cars, this is 1,800 iterations/second — trivially fast (< 1 µs). No caching needed.
**How to avoid:** Do not over-engineer a caching layer. Place the scan inline in `GetFuelData()`, same as the existing player scan.

### Pitfall 7: Lap 1 Bootstrap — No Samples Yet
**What goes wrong:** On lap 1, `_fuelSamples.Count == 0` so `C_fuel = 0` and `fuelDataReady = false`. FuelToAdd displays "--". This is existing behavior and is correct — there is no per-lap consumption estimate until at least one lap completes.
**What to avoid:** Do NOT attempt to bootstrap with a "default" consumption value. Show "--" until 2 samples are collected (existing `_fuelSamples.Count >= 2` gate is correct). The success criterion "shows a correct value from lap 1" means from the first lap completion, not from the start line.

### Pitfall 8: mTotalLaps Is `short`, Not `int`
**What goes wrong:** `mTotalLaps` is `public short mTotalLaps` (rF2Data.cs line 277). Assigning to `int leaderTotalLaps` works via implicit widening — no cast needed, but be aware.
**How to avoid:** `int leaderTotalLaps = veh.mTotalLaps;` is fine — widening conversion is automatic.

---

## Code Examples

### Fix 1 — Leader Laps (FUEL-01): Replacement for `raceLapsLeft` computation

```csharp
// Source: DataService.cs GetFuelData() — current wrong code at line 814:
// raceLapsLeft = Math.Max(0, info.mMaxLaps - currentLap);  // WRONG: uses player's laps

// CORRECTED:
int leaderTotalLaps = currentLap; // fallback: player's own laps (single-class / no leader found)
var scoringVehicles = _reader.Scoring.mVehicles;
int numVeh = Math.Min(info.mNumVehicles, scoringVehicles?.Length ?? 0);
for (int i = 0; i < numVeh; i++)
{
    var veh = scoringVehicles![i];
    if (veh.mPlace == 1 && veh.mFinishStatus != 1) // exclude DNF
    {
        leaderTotalLaps = veh.mTotalLaps;
        break;
    }
}
int raceLapsLeft;
if (info.mMaxLaps > 0 && info.mMaxLaps < 10000)
    raceLapsLeft = Math.Max(0, info.mMaxLaps - leaderTotalLaps);
else if (T_tour > 10 && sessionLeft > 0)
    raceLapsLeft = (int)Math.Ceiling(sessionLeft / T_tour);
else
    raceLapsLeft = 0;
```

Note: The existing player class scan (lines 822-828) already contains a `scoringVehicles` / `numVeh` declaration. Merge the leader scan into that same loop to avoid iterating twice. Both `mIsPlayer != 0` and `mPlace == 1` can be captured in one pass.

### Fix 2 — SC Tagging (FUEL-02): Additions to UpdateEnergyAndFuelTracking()

```csharp
// DECLARE (with other bool tracking fields, line ~39):
private bool _wasSlowPhaseThisLap;

// READ ScoringInfo (add at top of UpdateEnergyAndFuelTracking(), after getting scr):
var info = _reader.ScoringInfo;

// SET during tick (after existing "if (scr.mInPits != 0)" check):
if (info.mYellowFlagState >= 0) _wasSlowPhaseThisLap = true;

// INCLUDE in validity gate (line ~674):
bool isValid = !_wasInPitsThisLap && !_wasLapInvalidThisLap && !_wasSlowPhaseThisLap;

// RESET at lap boundary (alongside existing resets at line ~725):
_wasSlowPhaseThisLap = info.mYellowFlagState >= 0;  // carry over if SC still active
```

### Fix 3 — Configurable Margin (FUEL-03): Config and DataService wiring

```csharp
// In OverlayConfig.cs — new class:
public class FuelStrategyConfig
{
    public double SafetyMarginLaps { get; set; } = 1.0;
}

// In AppConfig — new property:
public FuelStrategyConfig FuelStrategy { get; set; } = new();

// In DataService — constructor change:
private readonly FuelStrategyConfig _fuelConfig;
public DataService(SharedMemoryReader reader, FuelStrategyConfig fuelConfig)
{
    _reader = reader;
    _fuelConfig = fuelConfig;
}

// In DataService — replace SAFETY_MARGIN_LAPS constant references:
// Line ~834: double V_marge = _fuelConfig.SafetyMarginLaps * C_fuel;
// Line ~856: double windowClose = L_real > 0 ? L_real - _fuelConfig.SafetyMarginLaps : 0;
// Remove: private const double SAFETY_MARGIN_LAPS = 1.0;

// In OverlayManager.cs — constructor:
_dataService = new DataService(_reader, config.FuelStrategy);

// In ConfigService.Load():
cfg.FuelStrategy ??= new FuelStrategyConfig();
```

---

## Data Model

### Key rF2 Fields (confirmed in rF2Data.cs)

| Field | Type | Location | Use |
|-------|------|----------|-----|
| `mYellowFlagState` | `sbyte` | `rF2ScoringInfo` | -1=none, 0=pending, 1=pits closed, 2=pit open lap, 3=last lap |
| `mMaxLaps` | `int` | `rF2ScoringInfo` | Race lap limit; 10000 = time-based race |
| `mNumVehicles` | `int` | `rF2ScoringInfo` | Number of vehicles in session |
| `mCurrentET` | `double` | `rF2ScoringInfo` | Elapsed time |
| `mEndET` | `double` | `rF2ScoringInfo` | Session end time |
| `mPlace` | `byte` | `rF2VehicleScoring` | Global race position (1-based) |
| `mTotalLaps` | `short` | `rF2VehicleScoring` | Completed laps |
| `mFinishStatus` | `sbyte` | `rF2VehicleScoring` | 0=none, 1=DNF, 2=DQ, 3=finished |
| `mIsPlayer` | `byte` | `rF2VehicleScoring` | Non-zero for the local player |
| `mUnderYellow` | `byte` | `rF2VehicleScoring` | Per-vehicle yellow flag (secondary) |
| `mInPits` | `byte` | `rF2VehicleScoring` | Non-zero when in pit lane |

### FuelData DTO Additions (optional but useful for display)

The architecture research suggests adding `LeaderTotalLaps` and `IsUnderSlowPhase` to `FuelData`. These are optional for the core fixes — the overlay already shows "--" when no data is ready. Adding `IsUnderSlowPhase` enables showing a warning to the driver during SC periods. This is a nice-to-have, not a blocker.

---

## Validation Architecture

### Test Framework

No existing test project exists in the solution. The solution (`LMUOverlay.sln`) contains only two projects: `LMUOverlay` (WinExe, .NET 8) and `rF2SharedMemory` (library). There is no `*.Tests.csproj`.

| Property | Value |
|----------|-------|
| Framework | None present — needs Wave 0 creation |
| Config file | None — needs `LMUOverlay.Tests/LMUOverlay.Tests.csproj` |
| Quick run command | `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/ --filter Category=Fuel` |
| Full suite command | `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FUEL-01 | Multi-class: GT3 player + Hypercar leader → laps remaining uses leader | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.MultiClass"` | Wave 0 |
| FUEL-01 | Single-class: player IS leader → identical result to old code | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.SingleClass"` | Wave 0 |
| FUEL-01 | Time-based race (mMaxLaps==10000) → falls back to sessionLeft/lapTime | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.TimeBased"` | Wave 0 |
| FUEL-02 | SC lap fuel sample excluded from average | unit | `dotnet test --filter "FullyQualifiedName~ConsumptionTrackerTests.SCLapExcluded"` | Wave 0 |
| FUEL-02 | Normal lap after SC included | unit | `dotnet test --filter "FullyQualifiedName~ConsumptionTrackerTests.PostSCNormal"` | Wave 0 |
| FUEL-03 | Safety margin 0.5 → fuelToAdd uses 0.5 × C_fuel | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.SafetyMargin"` | Wave 0 |
| FUEL-03 | Safety margin visible in windowClose | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.WindowClose"` | Wave 0 |

**Important:** `GetFuelData()` and `UpdateEnergyAndFuelTracking()` both depend on `SharedMemoryReader` (a Windows shared memory handle). Unit tests cannot call these methods directly. The correct approach is to extract the pure calculation logic into a static helper class (`FuelStrategyCalculator`) that takes plain inputs and returns plain outputs — this is what tests target. DataService remains the integration coordinator.

### Sampling Rate

- Per task commit: `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/ --filter Category=Fuel`
- Per wave merge: `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/`
- Phase gate: Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj` — new xUnit test project targeting net8.0
- [ ] `LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/FuelStrategyCalculatorTests.cs` — covers FUEL-01 and FUEL-03
- [ ] `LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/ConsumptionTrackerTests.cs` — covers FUEL-02
- [ ] Extraction: `Services/FuelStrategyCalculator.cs` (static class) — prerequisite for testability
- [ ] Framework install: `dotnet add LMUOverlay/LMUOverlay/LMUOverlay.Tests/ package xunit && dotnet add LMUOverlay/LMUOverlay/LMUOverlay.Tests/ package xunit.runner.visualstudio && dotnet sln LMUOverlay/LMUOverlay/LMUOverlay.sln add LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj`

---

## State of the Art

| Old Approach | Current Approach | Impact for This Phase |
|--------------|-----------------|----------------------|
| `mMaxLaps - currentLap` (player laps) | `mMaxLaps - leaderTotalLaps` (global P1) | FUEL-01: 2-line fix in GetFuelData() |
| No SC detection | Tag `_wasSlowPhaseThisLap` when `mYellowFlagState >= 0` | FUEL-02: 4-line addition across the method |
| Hardcoded `SAFETY_MARGIN_LAPS = 1.0` const | `_fuelConfig.SafetyMarginLaps` from `AppConfig` | FUEL-03: config class + constructor change + 2 reference replacements |

---

## Open Questions

1. **Does LMU expose per-class P1 separately from global P1?**
   - What we know: `mPlace` is the global overall position; `mVehicleClass` identifies the class. Class-position is computed in DataService (`ClassPosition` field) but this is a derived value, not a raw rF2 field.
   - What's unclear: Does LMU/rF2 ever put a class leader at mPlace==1 within-class? (Almost certainly not — `mPlace` is always global race position.)
   - Recommendation: Use `mPlace == 1` for global P1, which is correct for fuel prediction. Per-class positions are irrelevant for "when does the race end?".

2. **VSC detection — is mYellowFlagState sufficient?**
   - What we know: The architecture research identifies `mYellowFlagState >= 0` as the signal. In LMU, VSC and full SC both activate the yellow flag state machine.
   - What's unclear: Whether LMU reports a specific value for VSC vs. full SC (the values 0-3 may vary by implementation).
   - Recommendation: `mYellowFlagState >= 0` excludes any non-normal flag condition. This is conservative (correct for fuel prediction — we exclude the sample in any doubt) and mirrors the existing approach in `GetYellowFlagState()`.

3. **Settings UI location for safety margin**
   - What we know: `MainWindow.xaml.cs` has a large settings panel with per-overlay options. The `GeneralSettings` properties like `UpdateRateHz` use `Slider` controls. `OverlaySettings.CustomOptions` is a `Dictionary<string, object>` for per-overlay custom options.
   - Recommendation: Add the margin control to the Fuel overlay's settings section in MainWindow, alongside any existing fuel-related controls. Use a `Slider` (range 0-3, step 0.5) with a `TextBlock` label showing the current value.

---

## Sources

### Primary (HIGH confidence)
- Direct analysis of `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/DataService.cs` — `UpdateEnergyAndFuelTracking()` (lines 636–738) and `GetFuelData()` (lines 740–888)
- Direct analysis of `LMUOverlay/LMUOverlay/LMUOverlay/rF2SharedMemory/rF2Data.cs` — `mYellowFlagState` (sbyte, line 237), `mPlace` (byte, line 296), `mTotalLaps` (short, line 277), `mFinishStatus` (sbyte, line 279), `mUnderYellow` (byte, line 321), `mMaxLaps` (int, line 231)
- Direct analysis of `Models/OverlayConfig.cs` — `AppConfig`, `FuelData`, `GeneralSettings` structure
- Direct analysis of `Services/OverlayManager.cs` — `DataService` construction, config flow
- Direct analysis of `Views/Overlays/FuelStrategyOverlay.cs` — overlay is pure renderer, no calculation
- `.planning/research/ARCHITECTURE.md` — detailed algorithm designs (HIGH — based on same code analysis)

### Secondary (MEDIUM confidence)
- rF2/LMU SharedMemory field semantics (mYellowFlagState values 0-3, mMaxLaps=10000 convention) — consistent with code usage patterns found in `GetYellowFlagState()` and `GetFuelData()`

---

## Metadata

**Confidence breakdown:**
- Bug identification (FUEL-01, 02, 03): HIGH — confirmed by reading exact production code
- Fix algorithms: HIGH — directly derived from existing patterns in the same file
- rF2 field semantics: MEDIUM — derived from code usage; no official rF2 SDK docs consulted
- Test approach: HIGH — standard xUnit pattern for .NET 8; extraction-first strategy is required given SharedMemoryReader dependency

**Research date:** 2026-05-19
**Valid until:** 60 days (stable domain — rF2 shared memory protocol and .NET 8 are not fast-moving)
