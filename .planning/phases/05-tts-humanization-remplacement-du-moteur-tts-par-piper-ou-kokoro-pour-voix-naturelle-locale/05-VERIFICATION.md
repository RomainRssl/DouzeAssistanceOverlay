---
phase: 05-tts-humanization-remplacement-du-moteur-tts-par-piper-ou-kokoro-pour-voix-naturelle-locale
verified: 2026-05-21T00:00:00Z
status: human_needed
score: 7/8 must-haves verified
re_verification: false
human_verification:
  - test: "Launch the app and navigate to the Voice/Audio tab — verify the 'TEXTES DES ALERTES — PIPER TTS' section is visible with 23 labelled TextBox, pre-populated with French defaults, grouped by DRAPEAUX / CARBURANT / GAP & POSITION / SPOTTER / SECTEURS & TOURS / TEST categories."
    expected: "Section renders without crash; all 23 TextBox show French default texts."
    why_human: "WPF XAML rendering and pre-population from AlertTexts cannot be verified without a running desktop process."
  - test: "With piper.exe absent from the piper\\ folder, click APPLIQUER after editing any TextBox."
    expected: "TbPiperStatus shows 'Erreur : piper.exe introuvable dans piper\\' — no exception or crash."
    why_human: "File-system guard and UI error message can only be verified by running the app."
  - test: "With piper.exe present, edit 1–2 TextBox texts and click APPLIQUER."
    expected: "TbPiperStatus shows 'Generation en cours... (N/M)' during work, then 'N WAV generes avec succes.'; VoicePackName becomes 'piper' in the ComboBox; config.json is persisted."
    why_human: "Requires piper.exe binary and real WAV generation against the Piper process."
  - test: "Close and reopen the app after editing and saving texts."
    expected: "Edited texts are still shown in the TextBox (loaded from persisted config.json)."
    why_human: "Config round-trip persistence requires running the app across two launches."
---

# Phase 5: TTS Humanization — Verification Report

**Phase Goal:** Remplacement du moteur TTS par Piper ou Kokoro pour voix naturelle locale — les textes d'alertes sont configurables par l'utilisateur et Piper TTS peut générer des WAV via stdin.
**Verified:** 2026-05-21
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| 1 | AlertTextsTests.cs exists with 6 tests tagged Category=PiperTTS — 6 pass | VERIFIED | `dotnet test --filter Category=PiperTTS`: 6/6 Reussi |
| 2 | GeneralSettings has AlertTexts dictionary property and GetAlertText() method | VERIFIED | OverlayConfig.cs lines 452-467: property + TryGetValue helper present |
| 3 | VoiceService has EnsureDefaultAlertTexts() static method | VERIFIED | VoiceService.cs line 106: public static void EnsureDefaultAlertTexts; populates 23 keys via TryAdd |
| 4 | VoiceService uses GetAlertText() for all 23 alert strings — no raw French literals remain | VERIFIED | All 23 SpeechItem/switch Enqueue calls use _settings.GetAlertText(key, fallback); grep confirms 0 raw literals |
| 5 | VoicePanel.xaml has "TEXTES DES ALERTES" section with 23 TextBox named TbAlert_* | VERIFIED | Lines 370-707: section present, all 23 x:Name="TbAlert_*" confirmed; TbAlert_Test maps __test__ key |
| 6 | VoicePanel.xaml.cs has GenerateWav() using stdin invocation (not --text flag) | VERIFIED | RedirectStandardInput=true at line 341; --text absent from Arguments string; StandardInput.Close() before WaitForExit |
| 7 | EnsureDefaultAlertTexts called before VoicePanel.Initialize at startup | VERIFIED | MainWindow.xaml.cs line 87: Services.VoiceService.EnsureDefaultAlertTexts(_config.General) — placed before VoicePanel.Initialize at line 90 |
| 8 | VoicePanel "Textes des alertes" section renders, pre-populates TextBox, and APPLIQUER generates WAV or shows error | HUMAN NEEDED | Cannot verify WPF rendering, piper.exe invocation, and UI state machine without running app |

**Score:** 7/8 automated truths verified; 1 requires human testing

### Required Artifacts

| Artifact | Provides | Status | Details |
|----------|----------|--------|---------|
| `LMUOverlay.Tests/PiperTTS/AlertTextsTests.cs` | 6 real test methods (not stubs) | VERIFIED | 119 lines; all 6 tests have real assertions, none contain Assert.Fail |
| `LMUOverlay/Models/OverlayConfig.cs` | AlertTexts Dictionary + GetAlertText() in GeneralSettings | VERIFIED | Lines 452-467 contain full property + helper |
| `LMUOverlay/Services/VoiceService.cs` | EnsureDefaultAlertTexts static, 23 Enqueue calls updated | VERIFIED | 23 keys in _defaultAlertTexts; all Enqueue calls wrap GetAlertText |
| `LMUOverlay/Views/VoicePanel.xaml` | 23 TbAlert_* TextBox + BtnApplyPiper + TbPiperStatus | VERIFIED | Lines 388-707 contain all 23 named TextBox; BtnApplyPiper at 703; TbPiperStatus at 707 |
| `LMUOverlay/Views/VoicePanel.xaml.cs` | GenerateWav (stdin), OnApplyPiperTexts (async), PopulatePiperTexts, CollectAlertTexts | VERIFIED | All 4 methods present; GenerateWav uses RedirectStandardInput=true |
| `LMUOverlay/Views/MainWindow.xaml.cs` | EnsureDefaultAlertTexts call after config load | VERIFIED | Line 87 before VoicePanel.Initialize at line 90 |

Note: The plan listed App.xaml.cs as the target for EnsureDefaultAlertTexts — the implementation correctly placed it in MainWindow.xaml.cs instead (as the plan permitted via "If config initialization is in MainWindow.xaml.cs rather than App.xaml.cs, add the call there").

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| AlertTextsTests.cs | GeneralSettings | `new AppConfig(); config.General.AlertTexts` | VERIFIED | File compiles and all 6 tests pass |
| VoiceService.cs | GeneralSettings.AlertTexts | `_settings.GetAlertText(key, fallback)` | VERIFIED | GetAlertText appears 23 times across all alert paths |
| VoiceService.EnsureDefaultAlertTexts | GeneralSettings.AlertTexts | `TryAdd` on dictionary | VERIFIED | Pattern confirmed in VoiceService.cs line 109 |
| VoicePanel.xaml BtnApplyPiper | VoicePanel.xaml.cs OnApplyPiperTexts | `Click="OnApplyPiperTexts"` | VERIFIED | XAML line 705; handler at code-behind line 363 |
| VoicePanel.xaml.cs GenerateWav | piper\piper.exe | `RedirectStandardInput=true` stdin only | VERIFIED | Line 341: RedirectStandardInput=true; Arguments contains only --model and --output_file |
| MainWindow.xaml.cs | VoiceService.EnsureDefaultAlertTexts | Called after config load, before VoicePanel.Initialize | VERIFIED | Lines 87 then 90 |

### Requirements Coverage

No requirement IDs declared in any plan for this phase — coverage check not applicable.

### Anti-Patterns Found

None. Scanned VoiceService.cs, OverlayConfig.cs, AlertTextsTests.cs — no TODO/FIXME/placeholder/Assert.Fail patterns detected.

### Human Verification Required

#### 1. VoicePanel "Textes des alertes" section renders and pre-populates

**Test:** Launch the app; navigate to the Audio/Voice tab.
**Expected:** "TEXTES DES ALERTES — PIPER TTS" section visible; all 23 TextBox pre-populated with French default texts; grouped by category headers (DRAPEAUX, CARBURANT, GAP & POSITION, SPOTTER, SECTEURS & TOURS, TEST).
**Why human:** WPF XAML rendering and data binding cannot be verified without a running desktop process.

#### 2. piper.exe absent — error message, no crash

**Test:** Ensure piper.exe is NOT in the piper\ folder; click APPLIQUER after editing any TextBox.
**Expected:** TbPiperStatus shows "Erreur : piper.exe introuvable dans piper\" — no exception or crash dialog.
**Why human:** File-system guard and UI error path require a running app.

#### 3. WAV generation via piper.exe (if binary available)

**Test:** Place piper.exe + fr_FR-siwis-medium.onnx in the piper\ folder; edit 1–2 TextBox texts; click APPLIQUER.
**Expected:** Status shows "Generation en cours... (N/M)" during work; then "N WAV generes avec succes."; ComboBox "Pack vocal" switches to "piper"; WAV files created under voice\piper\.
**Why human:** Requires the Piper binary and actual process execution.

#### 4. Config persistence across restarts

**Test:** Edit texts, click APPLIQUER, close and reopen the app.
**Expected:** Edited texts still displayed in TextBox (loaded from persisted config.json).
**Why human:** Requires running the app across two launches.

### Gaps Summary

No automated gaps. All 7 automated must-haves are fully verified. One must-have (end-to-end UI + WAV generation flow) requires human confirmation because it depends on WPF rendering, a running process, and an optional Piper binary.

---

_Verified: 2026-05-21_
_Verifier: Claude (gsd-verifier)_
