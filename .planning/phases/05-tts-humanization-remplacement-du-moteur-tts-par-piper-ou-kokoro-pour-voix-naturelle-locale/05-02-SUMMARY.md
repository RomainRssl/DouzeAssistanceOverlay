---
phase: 05-tts-humanization-remplacement-du-moteur-tts-par-piper-ou-kokoro-pour-voix-naturelle-locale
plan: 02
subsystem: voice
tags: [tts, piper, csharp, dictionary, overlayconfig, voiceservice]

# Dependency graph
requires:
  - phase: 05-01
    provides: "RED TDD stubs for AlertTexts/GetAlertText/EnsureDefaultAlertTexts"
provides:
  - "GeneralSettings.AlertTexts Dictionary<string,string> with null-safe setter"
  - "GeneralSettings.GetAlertText(key, defaultText) helper — fallback on missing/whitespace"
  - "VoiceService.EnsureDefaultAlertTexts(settings) static — TryAdd pattern, 23 keys"
  - "All 23 VoiceService Enqueue calls use GetAlertText(key, fallback)"
  - "6 PiperTTS tests GREEN"
affects: [05-03, voice, tts]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AlertTexts dictionary on GeneralSettings — user-editable alert texts, JSON round-trip safe"
    - "GetAlertText(key, default) — lookup with whitespace guard, returns hardcoded fallback if dict empty"
    - "EnsureDefaultAlertTexts static helper — TryAdd never overwrites custom user text"
    - "All SpeechItem constructors use GetAlertText(key, fallback) — no raw French string literals"

key-files:
  created: []
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/VoiceService.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/PiperTTS/AlertTextsTests.cs

key-decisions:
  - "AlertTexts setter converts null to new() — Newtonsoft.Json assigns null on missing key, setter ensures non-null post-deserialize"
  - "GetAlertText guards against whitespace-only strings — IsNullOrWhiteSpace ensures accidental blank overrides fall through to hardcoded default"
  - "EnsureDefaultAlertTexts uses TryAdd — custom user texts are never overwritten on startup"
  - "23 keys matches _defaultAlertTexts count exactly — test PIPER-06 enforces this invariant"

patterns-established:
  - "Pattern: GetAlertText(key, fallback) — all alert text retrieval goes through this helper"
  - "Pattern: EnsureDefaultAlertTexts called at startup to populate missing entries"

requirements-completed: []

# Metrics
duration: 4min
completed: 2026-05-21
---

# Phase 05 Plan 02: AlertTexts Dictionary + 23 Enqueue GetAlertText Replacements Summary

**AlertTexts Dictionary<string,string> added to GeneralSettings with GetAlertText() helper; EnsureDefaultAlertTexts() static method populates 23 defaults; all 23 SpeechItem Enqueue calls replaced with _settings.GetAlertText(key, fallback) — turns 6 RED PiperTTS stubs GREEN**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-21T14:18:56Z
- **Completed:** 2026-05-21T14:22:56Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- `GeneralSettings.AlertTexts` Dictionary<string,string> added with null-safe setter ensuring legacy JSON (no AlertTexts key) deserializes to empty dict, not null
- `GeneralSettings.GetAlertText(key, defaultText)` helper — returns stored value only if non-whitespace, otherwise returns hardcoded fallback
- `VoiceService.EnsureDefaultAlertTexts(settings)` static method — TryAdd pattern over 23 default French alert texts; never overwrites custom user overrides
- All 23 `SpeechItem` Enqueue calls updated to `_settings.GetAlertText(key, fallback)` — no raw French string literals remain in VoiceService constructors
- 6 PiperTTS tests flipped from RED to GREEN; 46/46 full suite passes with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add AlertTexts + GetAlertText to GeneralSettings** - `ff87f44` (feat)
2. **Task 2: Add EnsureDefaultAlertTexts + replace 23 hardcoded strings** - `1279eec` (feat)

_Note: TDD tasks combined test implementation and production code in single commits per task._

## Files Created/Modified
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs` - Added AlertTexts property + GetAlertText() to GeneralSettings (Voice section)
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/VoiceService.cs` - Added _defaultAlertTexts dict, EnsureDefaultAlertTexts static, replaced all 23 hardcoded strings
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/PiperTTS/AlertTextsTests.cs` - Replaced 6 Assert.Fail stubs with real test bodies

## Decisions Made
- AlertTexts setter converts null to new() — Newtonsoft.Json assigns null on missing JSON key; setter ensures post-deserialize the dict is always non-null without migration code
- GetAlertText guards whitespace — IsNullOrWhiteSpace ensures accidentally blank overrides don't produce silent empty TTS
- EnsureDefaultAlertTexts uses TryAdd — users who manually edit config.json won't lose their custom texts on next launch
- Fixed xUnit2013 warnings (Assert.Equal(0, count) → Assert.Empty) as inline Rule 1 auto-fixes during Task 1

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed xUnit2013 analyzer warnings in test file**
- **Found during:** Task 1 (test body implementation)
- **Issue:** `Assert.Equal(0, collection.Count)` triggers xUnit2013 warning; should be `Assert.Empty(collection)`
- **Fix:** Replaced two `Assert.Equal(0, ...)` calls with `Assert.Empty(...)` in AlertTextsTests.cs
- **Files modified:** LMUOverlay.Tests/PiperTTS/AlertTextsTests.cs
- **Verification:** Build warnings cleared; tests still pass
- **Committed in:** ff87f44 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - xUnit analyzer compliance)
**Impact on plan:** Minimal; analyzer warnings fixed inline, no scope change.

## Issues Encountered
None — implementation followed plan spec exactly.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- GeneralSettings.AlertTexts is ready for UI exposure (plan 05-03 settings panel)
- EnsureDefaultAlertTexts should be called in App.xaml.cs startup after config load
- VoiceService._defaultAlertTexts can be exposed via VoiceRootDir pattern for WAV pack generation pipeline

---
*Phase: 05-tts-humanization*
*Completed: 2026-05-21*
