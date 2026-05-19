---
phase: 02
slug: ui-customization
status: draft
nyquist_compliant: true
wave_0_complete: true
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
| 02-W0-snap | 02-01 | 0 | UI-01 | unit | `dotnet test --filter "Category=UI&DisplayName~Snap"` | ✅ W0 | ⬜ pending |
| 02-W0-vrprofile | 02-01 | 0 | UI-04 | unit | `dotnet test --filter "Category=UI&DisplayName~VrProfile"` | ✅ W0 | ⬜ pending |
| 02-W0-themepreset | 02-01 | 0 | UI-03 | unit | `dotnet test --filter "Category=UI&DisplayName~Preset"` | ✅ W0 | ⬜ pending |
| 02-W0-coloroverride | 02-01 | 0 | UI-01/02 | unit | `dotnet test --filter "Category=UI&DisplayName~Color"` | ✅ W0 | ⬜ pending |
| 02-02-snap-impl | 02-02 | 1 | UI-01 | unit | `dotnet test --filter "Category=UI&DisplayName~Snap"` | ✅ W0 | ⬜ pending |
| 02-02-color-impl | 02-02 | 1 | UI-01/02 | unit | `dotnet test --filter "Category=UI&DisplayName~Color"` | ✅ W0 | ⬜ pending |
| 02-03-theme-impl | 02-03 | 1 | UI-03 | unit | `dotnet test --filter "Category=UI&DisplayName~Preset"` | ✅ W0 | ⬜ pending |
| 02-04-vr-impl | 02-04 | 2 | UI-04 | unit | `dotnet test --filter "Category=UI&DisplayName~VrProfile"` | ✅ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**Note:** WPF window instantiation tests (Left/Top/ActualWidth) require STA thread. Snap math, VR profile copy logic, color override lookup, and theme preset writing are all pure static methods testable without WPF. Visual feedback (edit borders, edit bar position, snap behavior, OverlayFocused event, session-start lock state) is manual-only — see Manual-Only Verifications below.

---

## Wave 0 Requirements

- [x] `LMUOverlay.Tests/UI/SnapGridTests.cs` — snap-to-grid math (pure static method, no WPF) — UI-01
- [x] `LMUOverlay.Tests/UI/VrProfileTests.cs` — VR profile copy logic (pure OverlaySettings) — UI-04
- [x] `LMUOverlay.Tests/UI/ThemePresetTests.cs` — preset theme JSON writing (file I/O, no WPF) — UI-03
- [x] `LMUOverlay.Tests/UI/ColorOverrideTests.cs` — color override lookup via CustomOptions dict — UI-01/02

All 4 Wave 0 files are defined in 02-01-PLAN.md. 02-01 resolves all Wave 0 gaps.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Covered By | Test Instructions |
|----------|-------------|------------|------------|-------------------|
| Edit mode border appears on unlock | UI-01/02 | WPF visual, no headless rendering | 02-02 Task 4 checkpoint | Launch app, click "Déverrouiller tout", confirm colored border on each overlay |
| Drag snaps to 10px grid visually | UI-01 | Pixel-level visual check | 02-02 Task 4 checkpoint | In edit mode, drag overlay slowly; position jumps should align to 10px increments |
| Snap behavior: overlay jumps 10px at a time | UI-01 | WPF drag simulation not unit-testable | 02-02 Task 4 checkpoint | Drag any overlay — movement must snap, not slide smoothly |
| Resize handles visible in edit mode | UI-02 | WPF visual | 02-02 Task 4 checkpoint | Confirm edge/corner grip handles visible on each overlay when unlocked |
| OverlayFocused event fires and shows edit bar | UI-01/02 | WPF event/window interaction | 02-02 Task 4 checkpoint | Click overlay in edit mode; contextual bar with opacity/color controls should appear |
| BaseOverlayWindow edit border changes color on theme switch | UI-01/02 | WPF visual | 02-02 Task 4 checkpoint | Switch theme, confirm edit border color updates to new AccentPrimary |
| No session-start auto-lock: edit mode state persists unless explicitly locked | UI-01 | Session lifecycle behavior | 02-02 Task 4 checkpoint | Start app in edit mode (unlocked), do not click lock; starting a race session should NOT auto-lock overlays |
| Theme applies instantly without restart | UI-03 | Runtime behavior | 02-03 no dedicated checkpoint | Switch theme in settings; all panels should update within one render frame |
| Edit bar appears on overlay focus | UI-01/02 | WPF window positioning | 02-02 Task 4 checkpoint | Click overlay in edit mode; contextual bar with opacity/color controls should appear |
| VR layout independent of 2D layout | UI-04 | Requires VR toggle | 02-04 Task 3 checkpoint | Move overlay in 2D mode, switch VREnabled; VR position should be unchanged |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (all 4 files defined in 02-01)
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
