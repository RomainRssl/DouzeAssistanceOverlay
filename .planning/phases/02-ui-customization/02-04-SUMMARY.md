---
phase: 02-ui-customization
plan: "04"
subsystem: ui
tags: [wpf, vr, layout-profiles, overlay-positioning, csharp]

# Dependency graph
requires:
  - phase: 02-ui-customization/02-02
    provides: BaseOverlayWindow drag/resize infrastructure and OverlayManager
  - phase: 02-ui-customization/02-03
    provides: ThemeManager and preset theme infrastructure
provides:
  - Independent 2D and VR layout profiles via VrPosX/VrPosY/VrWidth/VrHeight nullable fields
  - VrProfileHelper static class (InitVrFromTwoD, SaveDragResult, SaveResizeResult)
  - BaseOverlayWindow.IsVRModeActive static flag routing saves to correct profile
  - OverlayManager.ApplyVrProfile / Apply2dProfile for profile switching on VR toggle
affects:
  - 03-vr (VR overlay positioning will read VrPosX/VrPosY from OverlaySettings)
  - any future overlay that extends BaseOverlayWindow

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Static IsVRModeActive flag on BaseOverlayWindow avoids circular reference with OverlayManager while routing saves to correct profile"
    - "VrProfileHelper pure static class: no WPF dependency, fully unit-testable with 6 tests"
    - "ApplyVrProfile sets WPF Left/Top/Width/Height directly — never writes to Settings 2D fields, preserving profile isolation invariant"
    - "Nullable double? fields: backward-compatible with old config.json (null on missing JSON fields), no ConfigService migration needed"
    - "InitVrFromTwoD guard (VrPosX == null): safe to call on every VR activation, idempotent after first init"

key-files:
  created:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Helpers/VrProfileHelper.cs
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/Overlays/BaseOverlayWindow.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/OverlayManager.cs

key-decisions:
  - "VrProfileHelper pure static class: no WPF dependency enables unit testing without UI harness"
  - "ApplyVrProfile writes directly to WPF window Left/Top — 2D Settings fields preserved intact so Apply2dProfile can restore without backup"
  - "IsVRModeActive as static property on BaseOverlayWindow — avoids circular reference, visible to all overlay subclasses"
  - "Nullable double? for VR fields — Newtonsoft.Json deserializes missing JSON keys to null automatically, zero migration risk"

patterns-established:
  - "Profile isolation invariant: VR profile apply MUST NOT write to Settings.PosX/PosY/OverlayWidth/OverlayHeight"
  - "Static routing flag pattern: BaseOverlayWindow.IsVRModeActive set by OverlayManager before applying profile"

requirements-completed:
  - UI-04

# Metrics
duration: 30min
completed: 2026-05-19
---

# Phase 2 Plan 04: VR Layout Profile Isolation Summary

**Independent 2D/VR layout profiles via nullable VR fields on OverlaySettings and a static IsVRModeActive flag routing drag/resize saves to the correct profile**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-05-19
- **Completed:** 2026-05-19
- **Tasks:** 3 (including human verification checkpoint)
- **Files modified:** 4

## Accomplishments

- Added VrPosX, VrPosY, VrWidth, VrHeight nullable double? fields to OverlaySettings with full INotifyPropertyChanged support and backward-compatible JSON deserialization
- Created VrProfileHelper pure static class (InitVrFromTwoD, SaveDragResult, SaveResizeResult) with zero WPF dependency, making all 6 VrProfileTests GREEN
- Routed drag/resize saves in BaseOverlayWindow via VrProfileHelper using IsVRModeActive static flag; OverlayManager.StartVR/StopVR set the flag and call ApplyVrProfile/Apply2dProfile
- Human verification approved: 2D positions preserved after VR toggle, old config.json loads without crash with missing VR fields

## Task Commits

Each task was committed atomically:

1. **Task 1: Add VR fields to OverlaySettings + create VrProfileHelper** - `48627ad` (feat)
2. **Task 2: Route drag/resize saves via VrProfileHelper + profile switch in OverlayManager** - `38b07a9` (feat)
3. **Task 3: Human Verification** - checkpoint approved by user

## Files Created/Modified

- `LMUOverlay/.../Models/OverlayConfig.cs` - Added VrPosX, VrPosY, VrWidth, VrHeight nullable double? fields with INotifyPropertyChanged pattern
- `LMUOverlay/.../Helpers/VrProfileHelper.cs` - New pure static class: InitVrFromTwoD, SaveDragResult, SaveResizeResult
- `LMUOverlay/.../Views/Overlays/BaseOverlayWindow.cs` - Added IsVRModeActive static flag; OnMouseUp and resize save routed via VrProfileHelper
- `LMUOverlay/.../Services/OverlayManager.cs` - Added ApplyVrProfile, Apply2dProfile; wired into StartVR/StopVR

## Decisions Made

- VrProfileHelper as a pure static class with no WPF dependency allows unit testing all profile logic without a UI harness — 6 tests cover all drag/resize combinations in both modes
- ApplyVrProfile sets WPF window Left/Top/Width/Height directly (not Settings 2D fields) — this is the profile isolation invariant that enables Apply2dProfile to restore 2D layout without a separate backup copy
- IsVRModeActive as a static property on BaseOverlayWindow avoids introducing a circular reference between BaseOverlayWindow and OverlayManager while remaining visible to all overlay subclasses
- Nullable double? for VR fields: Newtonsoft.Json deserializes missing JSON keys as null automatically, so old config.json files (without VR fields) load without migration or crash

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- UI-04 requirement delivered: 2D repositioning no longer affects VR layout and vice versa
- VR profile fields (VrPosX/VrPosY/VrWidth/VrHeight) ready for Phase 3 VR service integration to read initial overlay positions
- All 21 UI tests remain GREEN (VrProfile: 6, SnapGrid: 7, ColorOverride: 4, ThemePreset: 4)
- Phase 2 UI Customization fully complete — all 4 plans delivered and human-verified

---
*Phase: 02-ui-customization*
*Completed: 2026-05-19*
