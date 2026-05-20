---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: executing
stopped_at: Completed 03-03-PLAN.md — Phase 03 complete
last_updated: "2026-05-20T12:57:47.330Z"
last_activity: 2026-05-19 — Phase 2 complete; all 4 plans done (edit mode, themes, VR profiles); ready for Phase 3
progress:
  total_phases: 6
  completed_phases: 4
  total_plans: 13
  completed_plans: 13
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-19)

**Core value:** Le pilote sait en un coup d'oeil combien d'essence/energie ajouter au pit stop pour finir la course — en tenant compte du leader global et du multi-classe.
**Current focus:** Phase 1 — Fuel Strategy Correctness

## Current Position

Phase: 2 of 3 (UI Customization)
Plan: 4 of 4 in current phase (02-04 complete — Phase 2 fully done)
Status: Executing
Last activity: 2026-05-19 — Phase 2 complete; all 4 plans done (edit mode, themes, VR profiles); ready for Phase 3

Progress: [██████████] 100% (Phase 2 complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 0
- Average duration: -
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1. Fuel | 0/3 | - | - |
| 2. UI | 0/4 | - | - |

**Recent Trend:**
- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
| Phase 01-fuel-strategy-correctness P01 | 3 | 3 tasks | 5 files |
| Phase 01.1-render-tech-evaluation P01 | 2 | 2 tasks | 2 files |
| Phase 01.1-render-tech-evaluation P02 | 3min | 2 tasks | 2 files |
| Phase 01.1-render-tech-evaluation P03 | 3min | 1 tasks | 1 files |
| Phase 01.1-render-tech-evaluation P03 | 5min | 2 tasks | 2 files |
| Phase 02-ui-customization P01 | 2min | 2 tasks | 4 files |
| Phase 02-ui-customization P03 | 15min | 2 tasks | 2 files |
| Phase 02-ui-customization P02 | 45min | 4 tasks | 5 files |
| Phase 03-web-browser-overlay P01 | 2min | 1 tasks | 1 files |
| Phase 03-web-browser-overlay P02 | 3min | 2 tasks | 5 files |
| Phase 03-web-browser-overlay P03 | 2min | 1 tasks | 1 files |
| Phase 03-web-browser-overlay P03 | 2min | 2 tasks | 1 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Roadmap init: Fuel phase before UI phase — fuel math has zero rendering risk; UI customization deferred until panel architecture is stable
- Existing: Rendu code-behind C# pur (pas MVVM/binding) — pattern valide en prod, conserver pour les overlays
- [Phase 01-fuel-strategy-correctness]: Extract FuelStrategyCalculator as pure static class — no SharedMemoryReader dependency enables unit testing
- [Phase 01-fuel-strategy-correctness]: Target net8.0-windows for test project to match main project TargetFramework and avoid WPF type conflicts
- [Plan 01-02]: Merged vehicle loop in GetFuelData() to find playerClass and P1 leader in one pass — DNF (mFinishStatus==1) excluded, finished (==3) included
- [Plan 01-02]: _wasSlowPhaseThisLap tracks SC/VSC via mYellowFlagState >= 0; resets carry-over at lap boundary if SC still active
- [Plan 01-03]: FuelStrategyConfig constructor injection into DataService — injected at construction time guarantees field always initialized; SAFETY_MARGIN_LAPS const removed
- [Plan 01-03]: Slider range 0-3 laps (F1 format) with immediate _configService.Save() on change — consistent with all other overlay settings sliders in MainWindow
- [Phase 01.1-render-tech-evaluation]: Match VROverlayService.CaptureAndSubmit() RTB caching pattern in OpenXRService (CachedRtb, CachedPixels, LastPixelW, LastPixelH fields)
- [Phase 01.1-render-tech-evaluation]: ProximityRadarOverlay instrumented with Stopwatch microsecond frame-time logging (500-sample rolling window, logs min/avg/p95/max to Debug)
- [Phase 01.1-render-tech-evaluation]: Use SKElement (not SKGLElement) — SKGLElement incompatible with AllowsTransparency=true due to WPF airspace conflict
- [Phase 01.1-render-tech-evaluation]: SKPaintSurfaceEventArgs in SkiaSharp.Views.Desktop namespace (Desktop.Common package) — separate using required alongside SkiaSharp.Views.WPF
- [Phase 01.1-render-tech-evaluation]: Phase 2 proceeds in WPF — BaseOverlayWindow already implements drag, resize, themes, 2D/VR profiles; no render change needed for UI-01 through UI-04
- [Phase 01.1-render-tech-evaluation]: SkiaSharp deferred to VR-02 — only adopt if live session shows >= 20% avg frame-time improvement over WPF baseline
- [Phase 01.1-render-tech-evaluation]: RECOMMENDATION.md approved by Romain — Phase 2 proceeds in WPF, SkiaSharp deferred to VR-02 pending live frame-time data
- [Phase 02-ui-customization]: VrProfileTests covers 6 tests (4 drag + 2 resize via SaveResizeResult) for full VR/2D separation
- [Phase 02-ui-customization]: ThemePresetTests uses IDisposable temp directory for hermetic test isolation (not %AppData%)
- [Phase 02-ui-customization]: ColorOverrideTests reads/writes via existing CustomOptions dictionary — no new OverlaySettings field needed for test scaffold
- [Phase 02-ui-customization]: Preset JSON uses nested colors/effects sections matching ApplyJson() parser — plan reference flat JSON was inaccurate; EnsurePresetThemesExistIn(dir) testable overload pattern enables hermetic test isolation without %AppData%
- [Plan 02-02]: Raw accumulation + snap display: _rawDragLeft/_rawDragTop track true float position; only Left/Top display are snapped — prevents drift from repeated rounding of small integer mouse deltas
- [Plan 02-02]: OverlayFocused as static event on BaseOverlayWindow — avoids circular reference between overlay windows and MainWindow; MainWindow subscribes, OverlayEditBar shown from there
- [Plan 02-02]: OverlayEditBar single shared instance with Closing+=Cancel+Collapse — eliminates Window creation overhead when clicking between overlays in edit mode
- [Plan 02-02]: SetAllLocked called only from OnToggleLock and startup init — no session or race event auto-locks overlays; edit mode persists until explicit user action
- [Phase 02-ui-customization]: VrProfileHelper pure static class — no WPF dependency enables unit testing all profile logic without a UI harness
- [Phase 02-ui-customization]: ApplyVrProfile writes to WPF window Left/Top directly — 2D Settings fields preserved intact so Apply2dProfile can restore without backup
- [Phase 02-ui-customization]: IsVRModeActive static property on BaseOverlayWindow — avoids circular reference with OverlayManager, visible to all overlay subclasses
- [Phase 02-ui-customization]: Nullable double? for VR fields — Newtonsoft.Json deserializes missing JSON keys to null, zero migration risk for old config.json
- [Phase 03-web-browser-overlay]: TDD RED stubs pattern for Plan 03-01: test contract before implementation (mirrors SnapGridTests/VrProfileTests approach from Phase 2)
- [Phase 03-web-browser-overlay]: AllowsTransparency=false must be set in WebBrowserOverlay constructor before Show() — BaseOverlayWindow sets true, override corrects it before window is shown
- [Phase 03-web-browser-overlay]: WebBrowserUrlValidator has zero WPF using statements — kept in LMUOverlay.Helpers so test project (UseWPF=false) can reference it transitively
- [Phase 03-web-browser-overlay]: WebBrowser added to _persistentOverlays — shows without LMU connection, mirrors TwitchChat behavior
- [Phase 03-web-browser-overlay]: WebBrowser added to _allOverlays sidebar list (NAVIGATEUR WEB) — required for toggle and settings panel routing, missing from plan spec
- [Phase 03-web-browser-overlay]: URL TextBox Text='' hardcoded, never bound to config — volatile by locked design decision
- [Phase 03-web-browser-overlay]: Human-verify checkpoint approved: full WebBrowserOverlay end-to-end flow confirmed — URL TextBox, CHARGER button, page loads, drag works via WEB bandeau, invalid URL disables overlay

### Roadmap Evolution

- Phase 01.1 inserted after Phase 1: Render tech evaluation (URGENT) — evaluate WPF vs SkiaSharp vs D3D11 before Phase 2 UI Customization
- Phase 3 added: Web Browser Overlay — WebView2 overlay affichant une page web depuis une URL configurable
- Phase 4 added: Twitch Chat Visual Customization — image header et color picker pour l'overlay chat
- Phase 5 added: TTS Humanization — moteur TTS local (Piper/Kokoro) pour voix naturelle

### Pending Todos

None yet.

### Blockers/Concerns

None yet.

## Session Continuity

Last session: 2026-05-20T12:57:47.328Z
Stopped at: Completed 03-03-PLAN.md — Phase 03 complete
Resume file: None
