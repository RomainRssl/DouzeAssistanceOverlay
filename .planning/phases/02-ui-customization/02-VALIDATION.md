---
phase: 02
slug: ui-customization
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-19
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.0 |
| **Config file** | LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj |
| **Quick run command** | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=UI" -x` |
| **Full suite command** | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ -x` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=UI" -x`
- **After every plan wave:** Run `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ -x`
- **Before `/gsd:verify-work`:** Full suite must be green (Fuel + UI categories)
- **Max feedback latency:** ~15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-W0-snap | 02-01 | 0 | UI-01 | unit | `dotnet test --filter "Category=UI&DisplayName~Snap"` | ❌ W0 gap | ⬜ pending |
| 02-W0-vrprofile | 02-04 | 0 | UI-04 | unit | `dotnet test --filter "Category=UI&DisplayName~VrProfile"` | ❌ W0 gap | ⬜ pending |
| 02-W0-themepreset | 02-03 | 0 | UI-03 | unit | `dotnet test --filter "Category=UI&DisplayName~Preset"` | ❌ W0 gap | ⬜ pending |
| 02-W0-coloroverride | 02-04 | 0 | UI-01/02 | unit | `dotnet test --filter "Category=UI&DisplayName~Color"` | ❌ W0 gap | ⬜ pending |
| 02-01-drag | 02-01 | 1 | UI-01 | unit | `dotnet test --filter "Category=UI&DisplayName~Drag"` | ❌ W0 | ⬜ pending |
| 02-02-resize | 02-02 | 1 | UI-02 | unit | `dotnet test --filter "Category=UI&DisplayName~Resize"` | ❌ W0 | ⬜ pending |
| 02-03-theme | 02-03 | 2 | UI-03 | unit | `dotnet test --filter "Category=UI&DisplayName~Theme"` | ❌ W0 | ⬜ pending |
| 02-04-vr | 02-04 | 2 | UI-04 | unit | `dotnet test --filter "Category=UI&DisplayName~VrProfile"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**Note:** WPF window instantiation tests (Left/Top/ActualWidth) require STA thread. Recommended: extract snap math, VR profile copy logic, color override lookup into pure static methods testable without WPF. Visual feedback (edit borders, edit bar position) is manual-only.

---

## Wave 0 Requirements

- [ ] `LMUOverlay.Tests/UI/SnapGridTests.cs` — snap-to-grid math (pure static method, no WPF) — UI-01
- [ ] `LMUOverlay.Tests/UI/VrProfileTests.cs` — VR profile copy logic (pure OverlaySettings) — UI-04
- [ ] `LMUOverlay.Tests/UI/ThemePresetTests.cs` — preset theme JSON writing (file I/O, no WPF) — UI-03
- [ ] `LMUOverlay.Tests/UI/ColorOverrideTests.cs` — color override lookup via CustomOptions dict — UI-01/02

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Edit mode border appears on unlock | UI-01/02 | WPF visual, no headless rendering | Launch app, click "Déverrouiller tout", confirm colored border on each overlay |
| Drag snaps to 10px grid visually | UI-01 | Pixel-level visual check | In edit mode, drag overlay slowly; position jumps should align to 10px increments |
| Resize handles visible in edit mode | UI-02 | WPF visual | Confirm edge/corner grip handles visible on each overlay when unlocked |
| Theme applies instantly without restart | UI-03 | Runtime behavior | Switch theme in settings; all panels should update within one render frame |
| Edit bar appears on overlay focus | UI-01/02 | WPF window positioning | Click overlay in edit mode; contextual bar with opacity/color controls should appear |
| VR layout independent of 2D layout | UI-04 | Requires VR toggle | Move overlay in 2D mode, switch VREnabled; VR position should be unchanged |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
