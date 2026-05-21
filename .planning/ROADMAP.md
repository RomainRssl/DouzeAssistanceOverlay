# Roadmap: LMUOverlay — Douze Assistance

## Overview

Two focused delivery phases. Phase 1 corrects the fuel strategy math — the core value of the application — by fixing multi-class race-end prediction and filtering Safety Car laps out of consumption averages. Phase 2 gives the driver control over the overlay itself: panel positioning, resizing, visual themes, and separate layout profiles for 2D and VR. Both phases are independent; fuel math carries zero rendering risk and ships first to stabilize the prediction engine before UI architecture changes land.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Fuel Strategy Correctness** - Fix race-end prediction for multi-class and SC lap filtering
- [x] **Phase 01.1: Render tech evaluation (INSERTED)** - WPF vs SkiaSharp PoC on ProximityRadarOverlay — unblocks Phase 2 (completed 2026-05-19)
- [x] **Phase 2: UI Customization** - Drag, resize, themes, and 2D/VR layout profiles (completed 2026-05-19)

## Phase Details

### Phase 1: Fuel Strategy Correctness
**Goal**: The driver gets an accurate fuel-to-add figure that accounts for the global race leader and excludes Safety Car laps from the consumption average
**Depends on**: Nothing (first phase)
**Requirements**: FUEL-01, FUEL-02, FUEL-03
**Success Criteria** (what must be TRUE):
  1. In a multi-class session, the fuel-to-add displayed is calculated from the global leader's remaining laps, not the player's own laps completed
  2. Laps driven behind Safety Car or VSC are excluded from the per-lap consumption average shown in the panel
  3. The driver can set a configurable safety margin (default 1 lap) in settings, and that margin is visibly reflected in the fuel-to-add figure
  4. The fuel panel shows a correct value from lap 1 of a multi-class race (no need to wait for a pit stop cycle to get a valid reading)
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md — xUnit test infrastructure + FuelStrategyCalculator extraction (Wave 0)
- [x] 01-02-PLAN.md — Fix FUEL-01 (leader laps) + FUEL-02 (SC exclusion) in DataService (Wave 1)
- [x] 01-03-PLAN.md — Fix FUEL-03 (configurable safety margin): config class, DataService wiring, settings slider (Wave 2)

### Phase 01.1: Render tech evaluation (INSERTED)

**Goal:** Evaluate WPF vs SkiaSharp 3.119.2 + D3D11 backend for overlay rendering — produce a PoC on ProximityRadarOverlay with frame-time measurements and a written recommendation that unblocks Phase 2
**Requirements**: VR-01
**Depends on:** Phase 1
**Plans:** 3/3 plans complete

Plans:
- [ ] 01.1-01-PLAN.md — Fix OpenXRService allocation bug + WPF baseline frame-time instrumentation on ProximityRadarOverlay (Wave 1)
- [ ] 01.1-02-PLAN.md — Implement ProximityRadarSkiaOverlay (SKElement PoC) + frame-time measurement (Wave 2)
- [ ] 01.1-03-PLAN.md — Write RECOMMENDATION.md with scored options; human approval checkpoint (Wave 3)

### Phase 2: UI Customization
**Goal**: The driver can arrange, size, and theme every overlay panel freely, with separate layout profiles saved for 2D screen and VR
**Depends on**: Phase 1, Phase 01.1
**Requirements**: UI-01, UI-02, UI-03, UI-04
**Success Criteria** (what must be TRUE):
  1. The driver can drag any overlay panel to a new screen position during a configuration session and the panel stays there on next launch
  2. The driver can resize any panel by dragging its edges or corners, and the resized dimensions persist across sessions
  3. The driver can select from at least three visual themes (dark current + 2 new) and the selected theme applies instantly to all panels without restart
  4. Repositioning or resizing panels in 2D mode does not alter the saved VR layout, and vice versa — the two profiles are independent
**Plans**: 4 plans

Plans:
- [ ] 02-01-PLAN.md — Wave 0: 4 xUnit test stubs (SnapGridTests, VrProfileTests, ThemePresetTests, ColorOverrideTests)
- [ ] 02-02-PLAN.md — Wave 1: Edit mode toggle, snap-to-grid drag, colored border, OverlayEditBar (UI-01, UI-02)
- [ ] 02-03-PLAN.md — Wave 1: 3 new theme presets + EnsurePresetThemesExist startup deploy (UI-03)
- [ ] 02-04-PLAN.md — Wave 2: VR profile fields, IsVRModeActive routing, ApplyVrProfile/Apply2dProfile (UI-04)

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 01.1 → 2

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Fuel Strategy Correctness | 3/3 | Complete | 2026-05-19 |
| 01.1. Render Tech Evaluation | 3/3 | Complete    | 2026-05-19 |
| 2. UI Customization | 4/4 | Complete   | 2026-05-19 |

### Phase 3: Web Browser Overlay — Overlay WebView2 affichant une page web depuis une URL configurable par l'utilisateur

**Goal:** L'utilisateur peut saisir une URL dans MainWindow et la charger dans un overlay flottant WebView2 ; l'overlay se désactive silencieusement si l'URL est invalide ou si la page échoue à charger
**Requirements**: WEB-01, WEB-02, WEB-03, WEB-04
**Depends on:** Phase 2
**Plans:** 3/3 plans complete

Plans:
- [ ] 03-01-PLAN.md — Wave 1: xUnit test stubs (WebBrowserUrlValidation, NavigationFailure, AppConfig round-trip)
- [ ] 03-02-PLAN.md — Wave 2: WebView2 NuGet, WebBrowserUrlValidator, WebBrowserOverlay class, AppConfig + OverlayManager wiring
- [ ] 03-03-PLAN.md — Wave 3: MainWindow URL TextBox + CHARGER button + human-verify checkpoint

### Phase 4: Twitch Chat Visual Customization — image header et color picker pour l'overlay chat

**Goal:** L'utilisateur peut personnaliser visuellement l'overlay TwitchChat : choisir une image de bandeau (PNG/JPG/BMP), masquer le bandeau entièrement, et ajuster la couleur de fond et la couleur accent via des swatches — tous les changements s'appliquent en temps réel sans redémarrage
**Requirements**: TWITCH-V-01, TWITCH-V-02, TWITCH-V-03, TWITCH-V-04, TWITCH-V-05, TWITCH-V-06, TWITCH-V-07
**Depends on:** Phase 3
**Plans:** 3/3 plans complete

Plans:
- [ ] 04-01-PLAN.md — Wave 0: RED stubs TwitchVisualConfigTests (JSON round-trip contrat TDD)
- [ ] 04-02-PLAN.md — Wave 1: TwitchSettings 4 nouveaux champs + TwitchChatOverlay ApplyVisualSettings()
- [ ] 04-03-PLAN.md — Wave 2: MainWindow TwitchChat — image picker + toggle bandeau + color pickers + human-verify

### Phase 5: TTS Humanization — remplacement du moteur TTS par Piper ou Kokoro pour voix naturelle locale

**Goal:** Le pilote peut editer les 23 textes d'alertes vocales depuis l'onglet Audio et generer les WAV correspondants via Piper TTS local (voix VITS fr_FR naturelle) en cliquant "Appliquer" — sans installation manuelle de Piper
**Requirements**: TBD
**Depends on:** Phase 4
**Plans:** 2/3 plans executed

Plans:
- [ ] 05-01-PLAN.md — Wave 0: RED TDD stubs AlertTextsTests (JSON round-trip, GetAlertText, 23 defaults)
- [ ] 05-02-PLAN.md — Wave 1: GeneralSettings.AlertTexts + GetAlertText + VoiceService.EnsureDefaultAlertTexts + replace 23 hardcoded strings
- [ ] 05-03-PLAN.md — Wave 2: VoicePanel Textes des alertes section (23 TextBox) + Piper Process.Start stdin handler + human-verify
