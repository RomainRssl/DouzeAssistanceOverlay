---
phase: 05
slug: tts-humanization-remplacement-du-moteur-tts-par-piper-ou-kokoro-pour-voix-naturelle-locale
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-20
---

# Phase 05 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.0 |
| **Config file** | LMUOverlay.Tests.csproj (`net8.0-windows`, `UseWPF=false`) |
| **Quick run command** | `dotnet test --filter "Category=PiperTTS" -v minimal` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=PiperTTS" -v minimal`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 0 | AlertTexts model | unit | `dotnet test --filter "Category=PiperTTS"` | ❌ W0 | ⬜ pending |
| 05-01-02 | 01 | 1 | AlertTexts JSON round-trip | unit | `dotnet test --filter "Category=PiperTTS"` | ❌ W0 | ⬜ pending |
| 05-01-03 | 01 | 1 | AlertTexts migration legacy config | unit | `dotnet test --filter "Category=PiperTTS"` | ❌ W0 | ⬜ pending |
| 05-01-04 | 01 | 1 | GetAlertText value from dict | unit | `dotnet test --filter "Category=PiperTTS"` | ❌ W0 | ⬜ pending |
| 05-01-05 | 01 | 1 | GetAlertText fallback on missing key | unit | `dotnet test --filter "Category=PiperTTS"` | ❌ W0 | ⬜ pending |
| 05-01-06 | 01 | 1 | 23 default keys present | unit | `dotnet test --filter "Category=PiperTTS"` | ❌ W0 | ⬜ pending |
| 05-02-01 | 02 | 1 | Piper WAV generation (Apply button) | manual | - | n/a | ⬜ pending |
| 05-02-02 | 02 | 1 | VoicePanel UI — 23 TextBox + Apply | manual | - | n/a | ⬜ pending |
| 05-02-03 | 02 | 1 | VoiceService reads from AlertTexts | manual | - | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LMUOverlay.Tests\PiperTTS\AlertTextsTests.cs` — RED stubs covering JSON round-trip, GetAlertText, defaults (23 keys)
- [ ] No new xUnit infrastructure needed — framework already in place

*Existing infrastructure covers all automated requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Bouton Appliquer régénère uniquement les WAV modifiés | CONTEXT decision | Requires piper.exe binary + actual WAV generation | 1. Edit 2-3 alert texts. 2. Click Appliquer. 3. Check only those WAV files updated (timestamp). |
| WAV piper joués par VoiceService.SpeakSync() | CONTEXT decision | Requires running sim session | 1. Generate piper WAVs. 2. Start sim. 3. Trigger alerts. 4. Verify natural voice plays. |
| Fallback SAPI si WAV piper absent | CONTEXT decision | Requires runtime testing | 1. Remove a piper WAV. 2. Trigger that alert. 3. Verify SAPI fallback fires. |
| piper.exe absent → message d'erreur dans VoicePanel | Claude's Discretion | UI feedback | 1. Rename piper.exe temporarily. 2. Click Appliquer. 3. Verify error message shown. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
