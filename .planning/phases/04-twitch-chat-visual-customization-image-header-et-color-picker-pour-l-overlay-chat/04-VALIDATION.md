---
phase: 04
slug: twitch-chat-visual-customization-image-header-et-color-picker-pour-l-overlay-chat
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-20
---

# Phase 04 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.0 |
| **Config file** | none (implicit discovery) |
| **Quick run command** | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=TwitchVisual" --no-build` |
| **Full suite command** | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --no-build` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=TwitchVisual" --no-build`
- **After every plan wave:** Run `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | TWITCH-V-01 | unit | `dotnet test --filter "FullyQualifiedName~TwitchVisualConfigTests"` | ❌ W0 | ⬜ pending |
| 04-01-02 | 01 | 1 | TWITCH-V-02 | unit | `dotnet test --filter "FullyQualifiedName~TwitchVisualConfigTests"` | ❌ W0 | ⬜ pending |
| 04-02-01 | 02 | 2 | TWITCH-V-01/02 | unit | `dotnet test --filter "Category=TwitchVisual"` | ✅ after W0 | ⬜ pending |
| 04-03-01 | 03 | 3 | TWITCH-V-03/04 | manual | n/a | n/a | ⬜ pending |
| 04-03-02 | 03 | 3 | TWITCH-V-05/06/07 | manual | n/a | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LMUOverlay.Tests\TwitchVisual\TwitchVisualConfigTests.cs` — RED stubs covering TWITCH-V-01 and TWITCH-V-02

*Wave 0 covers the only two automatable behaviors; WPF rendering tests are impossible (UseWPF=false in test project).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Image picker button appears, file filter correct | TWITCH-V-03 | WPF OpenFileDialog, no headless support | Click "Parcourir" in TwitchChat settings — verify PNG/JPG/BMP filter appears |
| Header hides when ShowHeader=false | TWITCH-V-04 | WPF rendering | Toggle "Masquer le bandeau" — header row should disappear completely |
| Background and accent colors apply live without restart | TWITCH-V-05 | WPF rendering | Change color via picker — overlay should update immediately |
| Reset restores default colors | TWITCH-V-06 | WPF rendering | Click Reset per color — verify reverts to defaults (#1a1a2e / #9146FF) |
| Missing image file at startup falls back to "TCHAT" text | TWITCH-V-07 | WPF rendering | Delete image file, restart — overlay should show "TCHAT" text, no crash |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
