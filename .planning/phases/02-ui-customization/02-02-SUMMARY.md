---
phase: 02-ui-customization
plan: "02"
subsystem: ui
tags: [edit-mode, snap-grid, color-override, overlay-bar, wpf, drag, lock-toggle]

# Dependency graph
requires:
  - phase: 02-ui-customization
    plan: "01"
    provides: "SnapGridTests (7 RED) + ColorOverrideTests (4 RED) TDD scaffold"
provides:
  - SnapGridHelper.cs — pure static snap math (Math.Round(raw/grid)*grid), DefaultGrid=10px
  - ColorOverrideHelper.cs — Get/Set color overrides via CustomOptions dict
  - BaseOverlayWindow edit mode — colored border (AccentPrimary 2px), snap drag, OverlayFocused static event
  - OverlayEditBar.cs — floating per-overlay contextual window with opacity sliders + 3 hex color inputs
  - MainWindow fix — _isLocked=true at startup, correct DÉVERROUILLER/VERROUILLER TOUT label, OverlayEditBar wired
affects:
  - 02-03 (ThemePresets — theme hot-reload calls RefreshColorOverrides)
  - 02-04 (VrProfile — BaseOverlayWindow extended further for VR drag/resize)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Static event pattern (OverlayFocused) for cross-window communication without direct MainWindow reference
    - Raw accumulation + snap display: accumulate raw delta, snap only for display/save — avoids position drift
    - Code-behind only WPF Window (no XAML) for OverlayEditBar — reduces file count, self-contained
    - Closing += Cancel + Collapse pattern for reusable Window instance (avoid destroy/recreate cost)

key-files:
  created:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Helpers/SnapGridHelper.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Helpers/ColorOverrideHelper.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/Overlays/OverlayEditBar.cs
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/Overlays/BaseOverlayWindow.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/MainWindow.xaml.cs

key-decisions:
  - "Raw accumulation + snap display: _rawDragLeft/_rawDragTop track unrounded position, Left/Top display snapped — avoids drift from repeated rounding of integer deltas"
  - "OverlayFocused as static event on BaseOverlayWindow — avoids circular reference between overlay and MainWindow; MainWindow subscribes, OverlayEditBar shown from there"
  - "OverlayEditBar as Closing-cancel + Visibility.Collapsed — single shared instance, no repeated Window creation overhead in edit mode"
  - "SetAllLocked called only from OnToggleLock and startup init — no session-start or race-start hooks auto-lock overlays; edit mode persists until explicit user action"

patterns-established:
  - "Static event on base class for one-to-many notification without direct references"
  - "SnapGridHelper.DefaultGrid=10.0 constant — all future drag snapping uses this shared constant"
  - "ColorOverrideHelper.Get/Set as single access point for CustomOptions color keys — consistent key naming (ColorBg, ColorText, ColorAccent)"

requirements-completed:
  - UI-01
  - UI-02

# Metrics
duration: "~45min (across session with checkpoint)"
completed: 2026-05-19
---

# Phase 02 Plan 02: Edit Mode + Snap Grid + OverlayEditBar Summary

**Global edit mode toggle with 10px snap-to-grid drag, AccentPrimary colored borders, and floating per-overlay OverlayEditBar with opacity/color controls — human-verified and approved**

## Performance

- **Duration:** ~45 min (tasks + human verification)
- **Started:** 2026-05-19
- **Completed:** 2026-05-19
- **Tasks:** 4 (3 auto + 1 checkpoint — human approved)
- **Files modified:** 5 (3 created, 2 modified)

## Accomplishments

- Implemented SnapGridHelper and ColorOverrideHelper static helpers — 7 SnapGridTests + 4 ColorOverrideTests GREEN (TDD RED → GREEN)
- Extended BaseOverlayWindow with edit border (AccentPrimary, 2px, IsHitTestVisible=false), snap drag via raw accumulation pattern, OverlayFocused static event
- Created OverlayEditBar — code-behind-only WPF Window with opacity sliders and 3 per-overlay color override hex inputs, reusable via Closing-cancel + Collapse pattern
- Fixed MainWindow: _isLocked=true startup, correct lock/unlock button labels, OverlayEditBar wired to OverlayFocused event
- Human verification passed: edit border visible, snap drag confirmed at 10px boundaries, OverlayEditBar repositions per overlay, lock/unlock cycle hides/shows bar

## Task Commits

Each task was committed atomically:

1. **Task 1: Create SnapGridHelper + ColorOverrideHelper static helpers** - `84bd758` (feat)
2. **Task 2a: Edit mode in BaseOverlayWindow** - `41c2b7f` (feat)
3. **Task 2b: Create OverlayEditBar.cs** - `43a2e06` (feat)
4. **Task 3: Fix MainWindow edit mode toggle + wire OverlayEditBar** - `f01b1ea` (fix)
5. **Task 4: Human verification checkpoint** - APPROVED (no code commit)

## Files Created/Modified

- `LMUOverlay/.../Helpers/SnapGridHelper.cs` — Pure static Snap(rawValue, grid=10) method + DefaultGrid const
- `LMUOverlay/.../Helpers/ColorOverrideHelper.cs` — Get/Set color overrides from OverlaySettings.CustomOptions
- `LMUOverlay/.../Views/Overlays/OverlayEditBar.cs` — Floating WPF Window: opacity sliders, 3 hex color inputs, AttachTo/Detach API
- `LMUOverlay/.../Views/Overlays/BaseOverlayWindow.cs` — Added _editBorder, _rawDragLeft/_rawDragTop, OverlayFocused static event, snap drag, RefreshColorOverrides()
- `LMUOverlay/.../Views/MainWindow.xaml.cs` — Fixed _isLocked=true, corrected toggle labels, wired _editBar to OverlayFocused

## Decisions Made

- Raw accumulation + snap display pattern: `_rawDragLeft/_rawDragTop` track true float position; only `Left`/`Top` (display) are snapped. This prevents position drift from repeated rounding of small integer mouse deltas.
- OverlayFocused as a static event on BaseOverlayWindow rather than an interface or delegate injection — avoids a circular reference between overlay windows and MainWindow, simplest wiring.
- OverlayEditBar: single shared instance with `Closing += Cancel + Collapse` — eliminates Window creation overhead when clicking between overlays in edit mode.
- SetAllLocked is called from exactly two places: startup init (guarantees locked state on first run) and OnToggleLock (user-initiated). No session or race event auto-locks.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

VrProfileTests.cs compile errors (from plan 02-01 TDD scaffold) blocked running `dotnet test --filter Category=UI` for the full suite. These are intentional RED tests (VrProfileHelper and VrPosX/VrPosY fields not yet implemented — that's plan 02-04). SnapGridTests and ColorOverrideTests were confirmed GREEN in the Task 1 commit when the test project still compiled. The VrProfileTests blocking issue is pre-existing, out of scope, and will be resolved in plan 02-04.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- SnapGridHelper and ColorOverrideHelper available for any future overlay that needs snap or color override logic
- BaseOverlayWindow.RefreshColorOverrides() hookable by subclasses for theme-aware color reapplication
- ThemeManager.EnsurePresetThemesExist (plan 02-03) deployed — theme hot-reload will call RefreshColorOverrides correctly
- Plan 02-04 (VR Profile separation) is next: adds VrPosX/VrPosY/VrWidth/VrHeight to OverlaySettings and creates VrProfileHelper, unblocking VrProfileTests

---
*Phase: 02-ui-customization*
*Completed: 2026-05-19*
