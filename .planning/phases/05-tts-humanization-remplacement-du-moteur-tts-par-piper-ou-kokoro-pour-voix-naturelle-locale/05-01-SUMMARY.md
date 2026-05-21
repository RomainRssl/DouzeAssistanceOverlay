---
phase: 05-tts-humanization-remplacement-du-moteur-tts-par-piper-ou-kokoro-pour-voix-naturelle-locale
plan: 01
subsystem: testing
tags: [xunit, tdd, tts, piper, alert-texts, voice-service]

# Dependency graph
requires: []
provides:
  - TDD RED stubs for AlertTexts dictionary on GeneralSettings (6 test contracts)
  - Test category PiperTTS reachable via dotnet test --filter Category=PiperTTS
  - Defined contracts: JSON round-trip, legacy migration, GetAlertText (present/absent/empty), EnsureDefaultAlertTexts 23 keys
affects:
  - 05-02 (implements AlertTexts + GetAlertText to turn these RED stubs GREEN)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TDD RED stubs pattern: Assert.Fail stubs compile before implementation members exist"
    - "Trait Category filter: [Trait('Category', 'PiperTTS')] for targeted test runs"

key-files:
  created:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/PiperTTS/AlertTextsTests.cs
  modified: []

key-decisions:
  - "6 test classes in one file (mirrors TwitchVisualConfigTests pattern) — one Fact per class for isolation"
  - "using LMUOverlay.Services included for VoiceService reference in Class 6 stub even though EnsureDefaultAlertTexts does not exist yet — stub body contains only Assert.Fail so no compile error"
  - "All member references (AlertTexts, GetAlertText, EnsureDefaultAlertTexts) kept out of method signatures — only valid as runtime-never-reached comments, not as parameter/return types"

patterns-established:
  - "PiperTTS stub pattern: method bodies contain only Assert.Fail('RED — implement in Plan 05-02') — no actual property access"

requirements-completed: []

# Metrics
duration: 5min
completed: 2026-05-21
---

# Phase 05 Plan 01: PiperTTS AlertTexts TDD RED Stubs Summary

**6 xUnit RED stubs in LMUOverlay.Tests/PiperTTS/ establish the AlertTexts + GetAlertText test contract before Plan 05-02 adds the implementation**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-05-21
- **Completed:** 2026-05-21
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Created `LMUOverlay.Tests/PiperTTS/AlertTextsTests.cs` with 6 test classes all tagged `[Trait("Category", "PiperTTS")]`
- All 6 tests fail RED via `Assert.Fail("RED — implement in Plan 05-02")` as required
- File compiles cleanly against current codebase (0 build errors) — no references to unimplemented members in compilable positions
- 40 pre-existing tests continue to pass, zero regressions

## Task Commits

1. **Task 1: Create AlertTextsTests.cs with 6 RED stubs** - `ab8687d` (test)

**Plan metadata:** _(docs commit follows)_

## Files Created/Modified
- `LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests/PiperTTS/AlertTextsTests.cs` - 6 RED TDD stubs for AlertTexts dictionary and GetAlertText helper contracts

## Decisions Made
- All 6 test classes kept in one file to mirror the TwitchVisualConfigTests pattern from Phase 4
- `using LMUOverlay.Services;` included even though `EnsureDefaultAlertTexts` does not exist yet — the stub body only calls `Assert.Fail()` so the using is inert until Plan 05-02 adds the method
- No references to `AlertTexts`, `GetAlertText`, or `EnsureDefaultAlertTexts` appear outside of comments and stub descriptions — all references are conceptual, kept in XML doc summaries only

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 05-02 can now implement `AlertTexts Dictionary<string,string>` on `GeneralSettings`, `GetAlertText()` helper, and `VoiceService.EnsureDefaultAlertTexts()` to turn all 6 stubs GREEN
- Test filter `dotnet test --filter Category=PiperTTS` confirmed working

---
*Phase: 05-tts-humanization*
*Completed: 2026-05-21*
