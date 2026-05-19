---
phase: 1
slug: fuel-strategy-correctness
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-19
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET 8) — needs Wave 0 creation |
| **Config file** | `LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj` — Wave 0 installs |
| **Quick run command** | `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/ --filter Category=Fuel` |
| **Full suite command** | `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/ --filter Category=Fuel`
- **After every plan wave:** Run `dotnet test LMUOverlay/LMUOverlay/LMUOverlay.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-W0-setup | 01 | 0 | FUEL-01/02/03 | infra | `dotnet build LMUOverlay/LMUOverlay/LMUOverlay.Tests/` | ❌ W0 | ⬜ pending |
| 1-W0-extract | 01 | 0 | FUEL-01/02/03 | unit stub | `dotnet test --filter Category=Fuel` | ❌ W0 | ⬜ pending |
| 1-01-multiclass | 01 | 1 | FUEL-01 | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.MultiClass"` | ❌ W0 | ⬜ pending |
| 1-01-singleclass | 01 | 1 | FUEL-01 | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.SingleClass"` | ❌ W0 | ⬜ pending |
| 1-01-timebased | 01 | 1 | FUEL-01 | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.TimeBased"` | ❌ W0 | ⬜ pending |
| 1-02-sc-excluded | 02 | 1 | FUEL-02 | unit | `dotnet test --filter "FullyQualifiedName~ConsumptionTrackerTests.SCLapExcluded"` | ❌ W0 | ⬜ pending |
| 1-02-post-sc | 02 | 1 | FUEL-02 | unit | `dotnet test --filter "FullyQualifiedName~ConsumptionTrackerTests.PostSCNormal"` | ❌ W0 | ⬜ pending |
| 1-03-margin | 03 | 2 | FUEL-03 | unit | `dotnet test --filter "FullyQualifiedName~FuelStrategyTests.SafetyMargin"` | ❌ W0 | ⬜ pending |
| 1-03-ui | 03 | 2 | FUEL-03 | manual | see Manual-Only below | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj` — new xUnit project targeting net8.0
- [ ] `LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/FuelStrategyCalculatorTests.cs` — test stubs covering FUEL-01 and FUEL-03
- [ ] `LMUOverlay/LMUOverlay/LMUOverlay.Tests/FuelStrategy/ConsumptionTrackerTests.cs` — test stubs covering FUEL-02
- [ ] `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/FuelStrategyCalculator.cs` — static class extraction (prerequisite for testability — no SharedMemoryReader dependency)
- [ ] Add xUnit + runner packages and add project to solution

Install commands:
```bash
dotnet add LMUOverlay/LMUOverlay/LMUOverlay.Tests/ package xunit
dotnet add LMUOverlay/LMUOverlay/LMUOverlay.Tests/ package xunit.runner.visualstudio
dotnet add LMUOverlay/LMUOverlay/LMUOverlay.Tests/ package Microsoft.NET.Test.Sdk
dotnet sln LMUOverlay/LMUOverlay/LMUOverlay.sln add LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj
```

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Safety margin slider appears in settings UI | FUEL-03 | WPF UI — no automated UI test framework | Launch app → open settings → verify "Fuel Safety Margin" slider appears with range 0-3, default 1.0 |
| Fuel-to-add reflects margin change in real-time | FUEL-03 | Requires running LMU session | Join practice session → change margin → verify fuel-to-add value changes by (margin × fuel_per_lap) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
