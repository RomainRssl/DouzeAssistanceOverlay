---
phase: 04-twitch-chat-visual-customization-image-header-et-color-picker-pour-l-overlay-chat
plan: "02"
subsystem: TwitchChatOverlay
tags: [twitch, visual-customization, model, tdd]
dependency_graph:
  requires: ["04-01"]
  provides: ["TwitchSettings visual fields", "TwitchChatOverlay.ApplyVisualSettings()"]
  affects: ["04-03"]
tech_stack:
  added: []
  patterns: ["sentinel string fields for optional colors", "BrushCache.Get pattern for frozen brushes", "BitmapCacheOption.OnLoad for file handle closure"]
key_files:
  created:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/Overlays/TwitchChatOverlay.cs
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/TwitchVisual/TwitchVisualConfigTests.cs
decisions:
  - "Sentinel '' (empty string) for color fields — overlay interprets empty as use theme default"
  - "ShowHeader defaults to true — preserves behavior of existing configs without this field"
  - "ApplyVisualSettings() called at end of constructor before event subscriptions — ensures visual state matches config on first show"
  - "BitmapCacheOption.OnLoad for header image — closes file handle immediately after load"
  - "Alpha 200 hardcoded for background, alpha 80 hardcoded for accent — matches original overlay constants"
metrics:
  duration: "3min"
  completed_date: "2026-05-20"
  tasks_completed: 2
  files_changed: 3
---

# Phase 04 Plan 02: TwitchSettings Model Extension + ApplyVisualSettings() Summary

TDD implementation of 4 visual customization fields in TwitchSettings and public `ApplyVisualSettings()` method in TwitchChatOverlay for hot-reload visual updates.

## What Was Built

### TwitchSettings — 4 new properties (OverlayConfig.cs)

Added to the `TwitchSettings` class after `MaxMessages`:

- `HeaderImagePath` (string, default `""`) — absolute path to an image displayed in the header; empty string falls back to "TCHAT" text
- `ShowHeader` (bool, default `true`) — when false, header Grid and separator Border are collapsed; preserves pre-Phase-4 config behavior
- `BackgroundColor` (string, default `""`) — hex color for overlay background; empty string uses `ThemeManager.Current.PanelBackground` at alpha 200
- `AccentColor` (string, default `""`) — hex color for separator; empty string uses Twitch purple (#9146FF) at alpha 80

Newtonsoft.Json deserializes absent keys to C# default values — no migration logic needed for existing `config.json` files.

### TwitchChatOverlay — Refactored with reference fields + ApplyVisualSettings()

**New instance fields:**
- `_outerBorder` — reference to the outer `Border` (background color target)
- `_sepBorder` — reference to the separator `Border` (accent color + visibility target)
- `_headerGrid` — reference to the header `Grid` (visibility target)
- `_headerText` — nullable `TextBlock` showing "TCHAT" text (fallback)
- `_headerImage` — nullable `Image` showing the custom header image (optional)

**Constructor changes:**
- Replaced static `TextBlock { Text="TWITCH" }` with `_headerText { Text="TCHAT" }` + `_headerImage { Visibility=Collapsed }`
- All 5 reference fields assigned during construction
- `ApplyVisualSettings()` called at end of constructor (before event subscriptions)
- Added `using System.IO` and `using System.Windows.Media.Imaging`

**`public void ApplyVisualSettings()`:**
1. Background color: resolves from `BackgroundColor` or theme fallback, applies via `BrushCache.Get()`
2. Accent color: resolves from `AccentColor` or Twitch purple fallback, applies via `BrushCache.Get()`
3. Header/separator visibility: toggled from `ShowHeader`
4. Header content: if `HeaderImagePath` is non-empty and file exists, loads `BitmapImage` with `OnLoad` cache option (closes file handle), shows image, hides text; otherwise shows text

## Tests

| Test | ID | Result |
|------|----|--------|
| TwitchSettings: 4 visual fields survive JSON round-trip | TWITCH-V-01 | GREEN |
| TwitchSettings: old JSON without visual fields deserializes to defaults | TWITCH-V-02 | GREEN |
| Full suite | All 40 | GREEN (0 regressions) |

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| Task 1 (GREEN) | b97a68e | feat(04-02): extend TwitchSettings with 4 visual customization fields |
| Task 2 | 689faf4 | feat(04-02): refactor TwitchChatOverlay — reference fields + ApplyVisualSettings() |

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check

- [x] `OverlayConfig.cs` modified — `TwitchSettings` has 4 new properties
- [x] `TwitchChatOverlay.cs` created/updated — `ApplyVisualSettings()` public, all 5 reference fields exist
- [x] `TwitchVisualConfigTests.cs` updated — RED stubs replaced with GREEN implementation
- [x] Build: 0 errors
- [x] Test suite: 40/40 passed
- [x] Commits b97a68e and 689faf4 exist

## Self-Check: PASSED
