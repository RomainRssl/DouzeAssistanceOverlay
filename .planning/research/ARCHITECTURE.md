# Architecture Patterns: FuelStrategy Refactoring

**Domain:** Real-time racing overlay — endurance multi-class fuel strategy
**Researched:** 2026-05-19
**Confidence:** HIGH — based on direct codebase analysis of existing DataService + rF2Data structs

---

## Current State (What Exists)

The app already has a working architecture with clear separation:

```
SharedMemoryReader (rF2SharedMemory library)
    └── DataService  (calculation + state, ~2000 lines)
         ├── GetFuelData() → FuelData DTO
         ├── GetAllVehicles() → List<VehicleData>
         └── GetPitStrategyData() → PitStrategyData DTO

FuelStrategyOverlay : BaseOverlayWindow
    └── UpdateData() → calls DataService, renders to WPF controls
```

The overlay is already correctly separated from calculation — `FuelStrategyOverlay.UpdateData()` is pure display logic, taking DTOs from `DataService`. The problem is **inside DataService**, specifically in `GetFuelData()`:

1. `raceLapsLeft` uses `info.mMaxLaps - currentLap` (player's own lap count) — wrong for multi-class
2. `_fuelSamples` has no safety car outlier filtering — it only excludes pit laps and invalid laps
3. `UpdateEnergyAndFuelTracking()` is tightly coupled to `GetFuelData()` — state mutation and calculation live in the same 200-line method

---

## Recommended Architecture

### Component Boundaries (Refined)

```
┌─────────────────────────────────────────────────────────┐
│  rF2SharedMemory (unchanged)                            │
│  SharedMemoryReader → raw structs (mScoringInfo, etc.)  │
└──────────────────┬──────────────────────────────────────┘
                   │ rF2ScoringInfo, rF2VehicleScoring[]
                   │ rF2VehicleTelemetry
                   ▼
┌─────────────────────────────────────────────────────────┐
│  DataService (existing)                                 │
│                                                         │
│  NEW: RaceContextResolver  (pure static/injectable)     │
│   ├── GetOverallLeaderLaps(vehicles[]) → int            │
│   └── IsUnderSlowPhase(mYellowFlagState) → bool         │
│                                                         │
│  NEW: ConsumptionTracker  (extracted from DataService)  │
│   ├── OnLapComplete(fuelUsed, energyNet, isValid, isSC) │
│   ├── GetFuelPerLap() → double  (filtered mean)         │
│   └── GetEnergyPerLap() → double (filtered mean)        │
│                                                         │
│  MODIFIED: GetFuelData()                                │
│   ├── calls RaceContextResolver.GetOverallLeaderLaps()  │
│   ├── calls ConsumptionTracker.GetFuelPerLap()          │
│   └── returns FuelData (unchanged DTO shape)            │
└──────────────────┬──────────────────────────────────────┘
                   │ FuelData DTO (unchanged)
                   ▼
┌─────────────────────────────────────────────────────────┐
│  FuelStrategyOverlay (unchanged display logic)          │
│  UpdateData() → reads FuelData, renders WPF controls    │
└─────────────────────────────────────────────────────────┘
```

The display layer **does not change** — all fixes are inside DataService and its extracted helpers.

---

## Problem 1: Multi-Class Race End Prediction

### Why the Current Code Is Wrong

```csharp
// CURRENT (wrong for multi-class):
if (info.mMaxLaps > 0 && info.mMaxLaps < 10000)
    raceLapsLeft = Math.Max(0, info.mMaxLaps - currentLap);
```

`currentLap` is `scr.mTotalLaps` — the player's own lap counter. In a multi-class race, a GT3 car may be on lap 35 while the HYPERCAR leader is on lap 42. `mMaxLaps` is the total event lap count the leader must complete. The GT3 must continue until the leader finishes, even if the GT3's own lap delta to `mMaxLaps` is already negative.

### Correct Algorithm

The race ends when the **overall leader** (position 1 in `mPlace`, across all classes) completes `mMaxLaps` laps. Steps:

```csharp
// In RaceContextResolver:
public static int GetRaceLapsRemaining(
    rF2VehicleScoring[] vehicles,
    int numVehicles,
    int mMaxLaps)
{
    // 1. Find the overall P1 vehicle (mPlace == 1, lowest value)
    //    mPlace is 1-based; among all vehicles regardless of class
    int leaderLaps = 0;
    for (int i = 0; i < numVehicles; i++)
    {
        if (vehicles[i].mPlace == 1)
        {
            leaderLaps = vehicles[i].mTotalLaps;
            break;
        }
    }

    // 2. Laps the leader still needs to complete
    if (mMaxLaps > 0 && mMaxLaps < 10000)
        return Math.Max(0, mMaxLaps - leaderLaps);

    // 3. Time-based fallback (unchanged from current code)
    return 0; // caller handles with sessionLeft / T_tour
}
```

Then for `FuelToAdd`, the player needs enough fuel for the laps **they** will still complete, which equals the leader laps remaining (the player may actually complete fewer if lapped, but adding excess fuel is always safe — add a ceil not a floor).

**Edge cases:**
- Leader in pits: `mTotalLaps` still reflects completed laps, not current lap — safe to use
- Leader DNF (`mFinishStatus == 3`): exclude from P1 search, use P2, etc.
- No mMaxLaps (time-based race): fall back to `Math.Ceiling(sessionLeft / playerLapTime)`

```csharp
// Time-based fallback (player's last lap time against session remaining):
else if (T_tour > 10 && sessionLeft > 0)
    raceLapsLeft = (int)Math.Ceiling(sessionLeft / T_tour);
```

This is correct for time-based races — the player races until time expires.

---

## Problem 2: Outlier Filtering for Safety Car Laps

### Why the Current Filtering Is Insufficient

Current exclusion rules:
- `_wasInPitsThisLap == true` → skip sample
- `_wasLapInvalidThisLap == true` → skip sample

Safety car laps pass both tests. A SC lap at Le Mans Hypercar might consume 1.8 L/lap instead of 4.2 L/lap — including this in a rolling 5-sample average would underestimate consumption and potentially leave the player short on fuel.

### Detection Signal: `mYellowFlagState`

rF2SharedMemory exposes `mYellowFlagState` on `rF2ScoringInfo`:
- `-1` = none
- `0` = pending
- `1` = pits closed (full course yellow / safety car deployed)
- `2` = pit open lap
- `3` = last lap of yellow

A safety car period spans values `0..2`. The lap should be tagged as SC if `mYellowFlagState >= 0` at **any point** during the lap.

Additionally `mUnderYellow` on `rF2VehicleScoring` is per-vehicle — use this as a secondary confirmation.

### Recommended Filtering Approach: Tag + IQR

Two-stage filter:

**Stage 1 — Hard exclusion (current approach, extend it):**

```csharp
// In UpdateEnergyAndFuelTracking(), add:
if (scoringInfo.mYellowFlagState >= 0) _wasSlowPhaseThisLap = true;
// Reset at lap boundary (same as _wasInPitsThisLap)

// At lap boundary:
bool isValid = !_wasInPitsThisLap
            && !_wasLapInvalidThisLap
            && !_wasSlowPhaseThisLap;   // NEW
```

This cleanly excludes SC laps from samples, same pattern as pit lap exclusion already present.

**Stage 2 — Statistical guard (soft, for edge cases):**

After collecting samples, apply Interquartile Range (IQR) filtering before computing the mean. With MAX_SAMPLES = 5, use a simpler approach: exclude samples deviating more than 30% from the median.

```csharp
// ConsumptionTracker.GetFuelPerLap():
public double GetFuelPerLap()
{
    if (_fuelSamples.Count == 0) return 0;
    if (_fuelSamples.Count < 3) return _fuelSamples.Average();

    var sorted = _fuelSamples.OrderBy(x => x).ToList();
    double median = sorted[sorted.Count / 2];
    double threshold = median * 0.30;   // 30% deviation band

    var filtered = sorted.Where(x => Math.Abs(x - median) <= threshold).ToList();
    return filtered.Count > 0 ? filtered.Average() : sorted.Average();
}
```

**Why 30%?** At race pace, lap-to-lap consumption varies ±5-10%. A SC lap is typically 50-60% below normal consumption. 30% is a conservative threshold that catches SC outliers while tolerating legitimate variation (mixed conditions, different throttle usage).

**Important:** Do NOT use IQR filtering in isolation — Stage 1 (SC tagging) is the primary mechanism. Stage 2 is a safety net for partial SC laps (FCY deployed mid-lap) and VSC periods.

---

## Problem 3: FuelStrategyService — Testable Separation

### Recommended Extraction

Extract the pure calculation logic from DataService into a static class `FuelStrategyCalculator`. This class has **no state** and **no dependencies** on SharedMemory — it takes plain doubles and returns results. This makes it trivially unit-testable.

```csharp
// NEW: Services/FuelStrategyCalculator.cs
public static class FuelStrategyCalculator
{
    public static int ComputeRaceLapsRemaining(
        int mMaxLaps,
        int leaderTotalLaps,
        double sessionLeft,
        double playerLapTime)
    {
        if (mMaxLaps > 0 && mMaxLaps < 10000)
            return Math.Max(0, mMaxLaps - leaderTotalLaps);
        if (playerLapTime > 10 && sessionLeft > 0)
            return (int)Math.Ceiling(sessionLeft / playerLapTime);
        return 0;
    }

    public static double ComputeFuelToAdd(
        int raceLapsRemaining,
        double fuelPerLap,
        double currentFuel,
        double fuelCapacity,
        double safetyMarginLaps = 1.0)
    {
        if (raceLapsRemaining <= 0 || fuelPerLap <= 0) return 0;
        double needed = (raceLapsRemaining + safetyMarginLaps) * fuelPerLap;
        return Math.Max(0, Math.Min(needed - currentFuel, fuelCapacity));
    }

    public static double ComputeEnergyToAdd(
        int raceLapsRemaining,
        double energyPerLap,
        double currentEnergyPct)
    {
        if (raceLapsRemaining <= 0 || energyPerLap <= 0) return 0;
        double needed = raceLapsRemaining * energyPerLap;
        return Math.Max(0, needed - currentEnergyPct);
    }

    public static int ComputeStopsRequired(
        double fuelDeficit,
        double fuelCapacity)
    {
        if (fuelCapacity <= 0 || fuelDeficit <= 0) return 0;
        return Math.Min(99, (int)Math.Ceiling(fuelDeficit / fuelCapacity));
    }
}
```

DataService calls `FuelStrategyCalculator.*` methods — it remains the stateful coordinator. The overlay remains a pure consumer of `FuelData`.

### ConsumptionTracker — Extracted State

```csharp
// NEW: Services/ConsumptionTracker.cs
public class ConsumptionTracker
{
    private readonly List<double> _fuelSamples    = new();
    private readonly List<double> _energySamples  = new();
    private readonly int          _maxSamples;

    public ConsumptionTracker(int maxSamples = 5)
        => _maxSamples = maxSamples;

    public void AddFuelSample(double litersUsed, bool isValid)
    {
        if (!isValid) return;
        if (litersUsed < 0.5 || litersUsed > 50.0) return;
        _fuelSamples.Add(litersUsed);
        if (_fuelSamples.Count > _maxSamples) _fuelSamples.RemoveAt(0);
    }

    public void AddEnergySample(double pctUsed, bool isValid)
    {
        if (!isValid) return;
        if (pctUsed < 0.1 || pctUsed > 100.0) return;
        _energySamples.Add(pctUsed);
        if (_energySamples.Count > _maxSamples) _energySamples.RemoveAt(0);
    }

    public double GetFuelPerLap()  => FilteredMean(_fuelSamples);
    public double GetEnergyPerLap() => FilteredMean(_energySamples);
    public int FuelSampleCount    => _fuelSamples.Count;
    public int EnergySampleCount  => _energySamples.Count;

    private static double FilteredMean(List<double> samples)
    {
        if (samples.Count == 0) return 0;
        if (samples.Count < 3) return samples.Average();
        var sorted = samples.OrderBy(x => x).ToList();
        double median = sorted[sorted.Count / 2];
        double band   = median * 0.30;
        var filtered  = sorted.Where(x => Math.Abs(x - median) <= band).ToList();
        return filtered.Count > 0 ? filtered.Average() : sorted.Average();
    }
}
```

DataService holds one `ConsumptionTracker` instance, replacing the raw `_fuelSamples` / `_energySamples` lists.

---

## Problem 4: Data Model — Inputs and Outputs

### Inputs → What DataService needs from SharedMemory

| Source | Field | Used For |
|--------|-------|---------|
| `rF2ScoringInfo` | `mMaxLaps` | Race lap limit (denominator) |
| `rF2ScoringInfo` | `mCurrentET`, `mEndET` | Time remaining (fallback) |
| `rF2ScoringInfo` | `mNumVehicles` | Loop bound for leader scan |
| `rF2ScoringInfo` | `mYellowFlagState` | SC/FCY detection |
| `rF2VehicleScoring[i]` | `mPlace` | Find overall P1 (leader) |
| `rF2VehicleScoring[i]` | `mTotalLaps` | Leader's completed laps |
| `rF2VehicleScoring[i]` | `mFinishStatus` | Exclude DNF from P1 |
| `rF2VehicleScoring[player]` | `mTotalLaps` | Player's lap count (for display) |
| `rF2VehicleScoring[player]` | `mLastLapTime` | Fallback lap time estimate |
| `rF2VehicleScoring[player]` | `mVehicleClass` | Car category → VE eligibility |
| `rF2VehicleScoring[player]` | `mUnderYellow` | Per-vehicle SC confirmation |
| `rF2VehicleTelemetry[player]` | `mFuel`, `mFuelCapacity` | Fuel quantity |
| `rF2VehicleTelemetry[player]` | `mBatteryChargeFraction` | Battery energy level |
| `rF2VehicleTelemetry[player]` | `mElectricBoostMotorState` | VE motor active |

### Outputs → FuelData DTO (existing, minimal additions)

The existing `FuelData` model is already well-designed. Two additions needed:

```csharp
public class FuelData
{
    // ... existing fields unchanged ...

    // NEW: expose leader context for display
    public int  LeaderTotalLaps    { get; set; }   // how many laps leader has done
    public bool IsUnderSlowPhase   { get; set; }   // SC/FCY active — warn pilot
}
```

The overlay can then show a warning when `IsUnderSlowPhase == true` that current consumption samples are being excluded (avoids confusion when "FUEL/TOUR" shows "--" during SC).

---

## Data Flow Diagram

```
SharedMemoryReader.Scoring.mVehicles[]
    │
    ├─[every tick]──► UpdateEnergyAndFuelTracking()
    │                  ├── detect lap boundary (mTotalLaps changed)
    │                  ├── if mYellowFlagState >= 0 → set _wasSlowPhaseThisLap
    │                  ├── on lap complete: ConsumptionTracker.AddFuelSample(used, isValid)
    │                  └── ConsumptionTracker.AddEnergySample(used, isValid)
    │
    └─[on demand]───► GetFuelData()
                       ├── RaceContextResolver.GetRaceLapsRemaining(vehicles, mMaxLaps)
                       │      └── scans mVehicles for mPlace==1, reads mTotalLaps
                       ├── ConsumptionTracker.GetFuelPerLap()   (filtered)
                       ├── ConsumptionTracker.GetEnergyPerLap() (filtered)
                       ├── FuelStrategyCalculator.ComputeFuelToAdd(...)
                       ├── FuelStrategyCalculator.ComputeEnergyToAdd(...)
                       ├── FuelStrategyCalculator.ComputeStopsRequired(...)
                       └── returns FuelData DTO
                            │
                            ▼
                   FuelStrategyOverlay.UpdateData()
                   (unchanged — pure rendering)
```

---

## Refactoring Approach

### Strategy: Extract, Don't Rewrite

The existing code works correctly for single-class and time-based races. The refactoring is surgical:

**Step 1 — Extract ConsumptionTracker** (pure refactor, no behavior change yet)
- Move `_fuelSamples`, `_energySamples`, `_energyDeployedSamples`, `_veSamples` lists into `ConsumptionTracker`
- Move `AddFuelSample()` / `AddEnergySample()` logic into the class
- `DataService` holds `private readonly ConsumptionTracker _consumption = new()`
- All existing tests should still pass (behavior identical)

**Step 2 — Add SC tagging** (small addition to tracking loop)
- Add `_wasSlowPhaseThisLap` bool, reset at lap boundary
- Set when `mYellowFlagState >= 0` during tick
- Pass as `isValid && !_wasSlowPhaseThisLap` to `AddFuelSample()`

**Step 3 — Add IQR filter** (inside ConsumptionTracker only)
- Replace `_fuelSamples.Average()` calls with `FilteredMean()` method
- This is isolated to ConsumptionTracker, zero impact on callsites

**Step 4 — Extract RaceContextResolver** (targeted fix for multi-class)
- Write static method `GetRaceLapsRemaining(vehicles, numVehicles, mMaxLaps)`
- Replace the two lines in `GetFuelData()` that compute `raceLapsLeft`
- Add same logic for energy (uses same `raceLapsLeft`)

**Step 5 — Extract FuelStrategyCalculator** (optional, enables unit tests)
- Move the `FuelToAdd`, `StopsRequired`, `EnergyToAdd` formulas into static methods
- DataService calls these instead of inline math
- Write unit tests against `FuelStrategyCalculator` without any SharedMemory dependency

### Risk Assessment

| Step | Risk | Reason |
|------|------|--------|
| 1 Extract ConsumptionTracker | Low | Pure refactor, same logic |
| 2 SC tagging | Low | Additive: new bool, same exclusion pattern as pits |
| 3 IQR filter | Low-Medium | Changes `FuelPerLap` value — test on SC lap data |
| 4 Multi-class laps | Medium | Core calculation change — verify on LMU multi-class replay |
| 5 FuelStrategyCalculator | Low | Pure extraction, no logic change |

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Using Player Laps as Race Remaining
**What goes wrong:** GT3 thinks race is over (or nearly over) 7+ laps before it actually ends, prompting an early pit that wastes time.
**Instead:** Always find P1 overall by scanning `mVehicles` for `mPlace == 1`.

### Anti-Pattern 2: Global mMaxLaps Misread
**What goes wrong:** `mMaxLaps` is 10000 in time-based races (rF2 convention). If not guarded, `raceLapsLeft = 10000 - 35 = 9965`.
**Instead:** Guard `mMaxLaps > 0 && mMaxLaps < 10000` (already done — keep this).

### Anti-Pattern 3: Including SC Laps in Rolling Average
**What goes wrong:** One SC lap at 1.8 L/tour in a 5-sample buffer shifts the average from 4.2 to 3.7 L/tour — the overlay recommends adding 15% less fuel.
**Instead:** Tag SC laps at tick level, exclude from sample buffer entirely.

### Anti-Pattern 4: Calculating EnergyToAdd Based on Player Laps
**What goes wrong:** Hybrid car (Peugeot 9X8) shows VE deficit as if race ends when player does — 7 laps early in Hypercar class. Actually no issue here since player IS Hypercar and the race ends for the leader first.
**Note:** VE calculation uses the same `raceLapsLeft` — fixing the laps remaining automatically fixes VE prediction.

### Anti-Pattern 5: Moving Calculation Into the Overlay
**What goes wrong:** Breaks testability, duplicates logic if other overlays (DashboardOverlay) need the same data.
**Instead:** All fuel calculations stay in DataService/ConsumptionTracker. Overlay stays a pure renderer.

---

## Scalability Considerations

| Concern | Current | After Refactor |
|---------|---------|----------------|
| 60+ car grids | O(n) scan for P1 in GetFuelData() — fine | Identical, O(n) scan |
| Tick rate 30-60Hz | UpdateEnergyAndFuelTracking() is O(1) | Identical |
| Memory | 5 samples × 4 lists = ~160 bytes | Identical |
| SC detection latency | N/A (not implemented) | 1 tick (~33ms) — negligible |

No performance concerns. The scan for overall P1 is O(n) over `mVehicles` array (max 128 vehicles in rF2) — at 30Hz this is negligible. The existing pattern of doing this scan inside `GetAllVehicles()` confirms this is accepted practice.

---

## Sources

- Direct analysis of `DataService.cs` (worktree `unruffled-mayer-93ceae`) — HIGH confidence
- Direct analysis of `rF2Data.cs` struct definitions — HIGH confidence
- Direct analysis of `FuelStrategyOverlay.cs` and `OverlayConfig.cs` — HIGH confidence
- rF2 SharedMemory protocol (mYellowFlagState values 0-3, mMaxLaps=10000 convention) — MEDIUM confidence (consistent with observed code patterns and rF2 community documentation)
- IQR filtering approach — MEDIUM confidence (standard statistical practice; threshold of 30% is a recommendation, should be validated against real SC lap data)
