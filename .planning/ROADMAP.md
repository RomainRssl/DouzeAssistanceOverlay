# Roadmap: LMUOverlay — Douze Assistance

## Overview

Two focused delivery phases. Phase 1 corrects the fuel strategy math — the core value of the application — by fixing multi-class race-end prediction and filtering Safety Car laps out of consumption averages. Phase 2 gives the driver control over the overlay itself: panel positioning, resizing, visual themes, and separate layout profiles for 2D and VR. Both phases are independent; fuel math carries zero rendering risk and ships first to stabilize the prediction engine before UI architecture changes land.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Fuel Strategy Correctness** - Fix race-end prediction for multi-class and SC lap filtering
- [ ] **Phase 2: UI Customization** - Drag, resize, themes, and 2D/VR layout profiles

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

**Goal:** [Urgent work - to be planned]
**Requirements**: TBD
**Depends on:** Phase 1
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 01.1 to break down)

### Phase 2: UI Customization
**Goal**: The driver can arrange, size, and theme every overlay panel freely, with separate layout profiles saved for 2D screen and VR
**Depends on**: Phase 1
**Requirements**: UI-01, UI-02, UI-03, UI-04
**Success Criteria** (what must be TRUE):
  1. The driver can drag any overlay panel to a new screen position during a configuration session and the panel stays there on next launch
  2. The driver can resize any panel by dragging its edges or corners, and the resized dimensions persist across sessions
  3. The driver can select from at least three visual themes (dark current + 2 new) and the selected theme applies instantly to all panels without restart
  4. Repositioning or resizing panels in 2D mode does not alter the saved VR layout, and vice versa — the two profiles are independent
**Plans**: TBD

Plans:
- [ ] 02-01: Implement drag-to-reposition on BaseOverlayWindow with position persistence
- [ ] 02-02: Implement free resize on BaseOverlayWindow with dimension persistence
- [ ] 02-03: Add two new visual themes to ThemeManager; expose theme selector in settings UI
- [ ] 02-04: Split overlay config JSON into 2D and VR profile sections; wire profile switching on mode change

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Fuel Strategy Correctness | 3/3 | Complete | 2026-05-19 |
| 2. UI Customization | 0/4 | Not started | - |
