---
phase: 3
slug: web-browser-overlay
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-20
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing — LMUOverlay.Tests project) |
| **Config file** | LMUOverlay/LMUOverlay.Tests/LMUOverlay.Tests.csproj |
| **Quick run command** | `dotnet test --filter "Category=Phase3" --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Phase3" --no-build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-01-01 | 01 | 0 | WEB-01 | unit stub | `dotnet test --filter "WebBrowserOverlayTests"` | ❌ W0 | ⬜ pending |
| 3-02-01 | 02 | 1 | WEB-01 | integration | Manual — launch overlay, load URL | N/A | ⬜ pending |
| 3-02-02 | 02 | 1 | WEB-02 | manual | Verify AllowsTransparency=false, window opaque | N/A | ⬜ pending |
| 3-02-03 | 02 | 1 | WEB-03 | manual | Load invalid URL → overlay disables | N/A | ⬜ pending |
| 3-03-01 | 03 | 2 | WEB-01 | manual | Enter URL in MainWindow → overlay loads page | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LMUOverlay.Tests/Phase3/WebBrowserOverlayTests.cs` — stubs for WebBrowserOverlay OverlaySettings registration

*WebView2 integration is inherently manual — no unit tests can simulate HWND-based browser rendering.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Page web s'affiche dans l'overlay | WEB-01 | WebView2 HWND, pas testable en headless | Lancer app, entrer URL valide dans MainWindow, cliquer Charger |
| AllowsTransparency=false sans casser l'UI | WEB-02 | Rendu visuel WPF | Vérifier fond sombre opaque, overlay draggable via bandeau |
| Overlay se désactive sur URL invalide | WEB-03 | NavigationCompleted event, runtime requis | Entrer "not-a-url" ou URL down → overlay doit disparaître |
| Drag via bandeau uniquement | WEB-04 | Interaction souris + HWND | Cliquer dans WebView2 → pas de drag ; cliquer bandeau → drag OK |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
