---
phase: 04-twitch-chat-visual-customization-image-header-et-color-picker-pour-l-overlay-chat
plan: "03"
subsystem: ui
tags: [wpf, twitch, color-picker, image-picker, overlay-customization]

requires:
  - phase: 04-02
    provides: TwitchChatOverlay.ApplyVisualSettings() + TwitchSettings visual fields in OverlayConfig

provides:
  - "MainWindow TwitchChat section: IMAGE BANDEAU (PARCOURIR + Effacer), Masquer le bandeau toggle, COULEURS (Fond + Accent color pickers with Reset)"
  - "Live visual updates via ApplyVisualSettings() on every user interaction"
  - "Persistence to config.json for HeaderImagePath, ShowHeader, BackgroundColor, AccentColor"

affects: [phase-05-tts, checkpoint-human-verify]

tech-stack:
  added: []
  patterns:
    - "Visual control code-behind pur pattern: update _config.X → _configService.Save → ApplyVisualSettings()"
    - "OpenFileDialog image picker with PNG/JPG/BMP filter in WPF code-behind"
    - "Inverted toggle pattern: checked = hidden (ShowHeader = !v)"

key-files:
  created: []
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/MainWindow.xaml.cs

key-decisions:
  - "AddColorPicker reused with existing internal presets — plan's custom bgPresets/accPresets dropped in favor of existing 8-swatch palette (no API change needed)"
  - "Inverted toggle: AddToggle receives !ShowHeader so checked state means 'masqué'"

patterns-established:
  - "Three-step live update: _config.X = value → _configService.Save(_config) → GetOverlay<T>(key)?.ApplyVisualSettings()"

requirements-completed:
  - TWITCH-V-03
  - TWITCH-V-04
  - TWITCH-V-05
  - TWITCH-V-06
  - TWITCH-V-07

duration: ~25min
completed: 2026-05-20
---

# Phase 4 Plan 03: TwitchChat Visual Controls in MainWindow Summary

**PARCOURIR image picker + Masquer le bandeau toggle + Fond/Accent color pickers with Reset buttons added to MainWindow TwitchChat section, all wired to live ApplyVisualSettings() — checkpoint humain approuvé**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-20
- **Completed:** 2026-05-20
- **Tasks:** 2 of 2 (Task 1 auto + Task 2 checkpoint:human-verify approuvé)
- **Files modified:** 1

## Accomplishments
- Added IMAGE BANDEAU section with PARCOURIR button (OpenFileDialog PNG/JPG/BMP), filename display label, and Effacer l'image button
- Added Masquer le bandeau toggle wired inversely (checked = header hidden, ShowHeader = !v)
- Added COULEURS section with AddColorPicker Fond + Reset fond + AddColorPicker Accent + Reset accent
- All three sections fully wired: config update → save → ApplyVisualSettings() live refresh
- Build: 0 errors (22 pre-existing warnings, all unrelated to this plan)

## Task Commits

1. **Task 1: Ajouter les controles visuels dans la section TwitchChat de MainWindow** - `24eff42` (feat)
2. **Task 2: checkpoint:human-verify — validation visuelle complète** - approuvé (aucun commit code — vérification humaine)

## Files Created/Modified
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/MainWindow.xaml.cs` - Added 165 lines: IMAGE BANDEAU + Masquer le bandeau + COULEURS sections inside if (key == "TwitchChat") block

## Decisions Made
- AddColorPicker existing method uses its own hardcoded presets (line 685) — plan mentioned custom bgPresets/accPresets arrays that don't apply to the existing API signature. Passed currentHex as specified, presets are the existing 8-color palette. No new overload needed.
- Inverted toggle pattern chosen as specified: `AddToggle("Masquer le bandeau", !_config.Twitch.ShowHeader, v => { _config.Twitch.ShowHeader = !v; ... })`

## Deviations from Plan

None - plan executed exactly as written (custom preset arrays in plan were documentation-only and not applicable to AddColorPicker signature, no code change needed).

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
Plan 04-03 complete. Phase 4 (Twitch Chat Visual Customization) entièrement terminée — TwitchSettings 4 champs + ApplyVisualSettings() + controles MainWindow tous validés.
Phase 5 (TTS Humanization — moteur TTS local Piper/Kokoro) peut démarrer sans dépendances bloquantes.

---
*Phase: 04-twitch-chat-visual-customization-image-header-et-color-picker-pour-l-overlay-chat*
*Completed: 2026-05-20*
