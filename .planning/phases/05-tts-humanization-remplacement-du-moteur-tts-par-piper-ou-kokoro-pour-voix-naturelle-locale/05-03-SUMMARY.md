---
phase: 05-tts-humanization-remplacement-du-moteur-tts-par-piper-ou-kokoro-pour-voix-naturelle-locale
plan: 03
subsystem: ui
tags: [wpf, xaml, piper-tts, tts, voice, alert-texts, stdin]

# Dependency graph
requires:
  - phase: 05-02
    provides: AlertTexts dictionary, GetAlertText(), EnsureDefaultAlertTexts() on GeneralSettings/VoiceService

provides:
  - VoicePanel Textes des alertes section with 23 TextBox grouped by category
  - Piper WAV generation via stdin (not --text argument) in GenerateWav()
  - OnApplyPiperTexts async handler with selective regen and progress status
  - EnsureDefaultAlertTexts wired at app startup in MainWindow ctor

affects: [voice-alerts, piper-tts, wav-generation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Piper TTS stdin invocation: RedirectStandardInput=true + Close() before WaitForExit(15000) — NOT --text arg"
    - "Static dictionary _piperTextBoxMapping shared between PopulatePiperTexts/CollectAlertTexts — single source of truth"
    - "EnsureDefaultAlertTexts called before VoicePanel.Initialize() in MainWindow ctor"

key-files:
  created: []
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/VoicePanel.xaml
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/VoicePanel.xaml.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/MainWindow.xaml.cs

key-decisions:
  - "EnsureDefaultAlertTexts placed in MainWindow ctor (not App.xaml.cs) — config load is in MainWindow, App.xaml.cs has no AppConfig reference"
  - "_piperTextBoxMapping as static readonly field — avoids duplicating 23-entry dict in both PopulatePiperTexts and CollectAlertTexts"
  - "GenerateWav returns false on timeout/non-zero exit — errors logged to Debug, user sees status TextBlock message not an exception dialog"

patterns-established:
  - "Piper stdin pattern: ProcessStartInfo with RedirectStandardInput=true, WriteLine(text), Close() BEFORE WaitForExit — prevents freeze on Windows (issue #810)"
  - "Selective WAV regen: compare GetAlertText(key) vs new value to skip unchanged texts"

requirements-completed: []

# Metrics
duration: 8min
completed: 2026-05-21
---

# Phase 05 Plan 03: VoicePanel Textes des alertes + Piper stdin generation Summary

**WPF VoicePanel extended with 23 customisable alert TextBox (6 categories) and async Piper WAV generation via stdin, wired to startup default-text initialisation**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-21T14:27:00Z
- **Completed:** 2026-05-21T14:35:00Z
- **Tasks:** 2 (+ checkpoint)
- **Files modified:** 3

## Accomplishments

- VoicePanel.xaml gains a "TEXTES DES ALERTES — PIPER TTS" section with 23 named TextBox grouped under 6 category headers (DRAPEAUX, CARBURANT, GAP & POSITION, SPOTTER, SECTEURS & TOURS, TEST)
- VoicePanel.xaml.cs gains PopulatePiperTexts, CollectAlertTexts, GenerateWav (stdin-only, 15s timeout), and OnApplyPiperTexts (selective regen, progress counter, VoicePackName update, config save)
- MainWindow.xaml.cs wires VoiceService.EnsureDefaultAlertTexts before VoicePanel.Initialize — 23 defaults populated on first launch without overwriting custom user texts

## Task Commits

1. **Task 1: Wire EnsureDefaultAlertTexts at startup** - `56b708c` (feat)
2. **Task 2: Add Textes des alertes section + Piper generation code** - `1e75b89` (feat)

## Files Created/Modified

- `LMUOverlay/.../Views/MainWindow.xaml.cs` - EnsureDefaultAlertTexts call before VoicePanel.Initialize
- `LMUOverlay/.../Views/VoicePanel.xaml` - 23 TbAlert_* TextBox, BtnApplyPiper, TbPiperStatus
- `LMUOverlay/.../Views/VoicePanel.xaml.cs` - PopulatePiperTexts, CollectAlertTexts, GenerateWav (stdin), OnApplyPiperTexts async handler

## Decisions Made

- EnsureDefaultAlertTexts placed in MainWindow.xaml.cs constructor (not App.xaml.cs) — AppConfig is loaded and available there, App.xaml.cs only handles crash logging and theme init
- `_piperTextBoxMapping` as a static readonly Dictionary field shared between PopulatePiperTexts and CollectAlertTexts — avoids duplicating the 23-entry mapping
- GenerateWav returns bool (no throw) — errors logged to Debug output, user sees TbPiperStatus message rather than exception dialog

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None

## User Setup Required

None - no external service configuration required. (piper.exe must be placed in `piper\` subfolder next to the executable for WAV generation to work; the app shows an error in TbPiperStatus if absent, no crash.)

## Next Phase Readiness

Phase 5 task code is complete. Human-verify checkpoint (Task 3) awaits visual and functional verification:
- Launch app, navigate to Audio tab, confirm 23 TextBox pre-populated
- Edit texts, click APPLIQUER, verify WAV generation (or graceful error if piper.exe absent)
- Confirm config.json saved and voice pack ComboBox refreshed to "piper"

---
*Phase: 05-tts-humanization*
*Completed: 2026-05-21*
