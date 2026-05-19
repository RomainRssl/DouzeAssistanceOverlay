# Requirements: LMUOverlay — Douze Assistance

**Defined:** 2026-05-19
**Core Value:** Le pilote sait en un coup d'oeil combien d'essence/energie ajouter au pit stop pour finir la course — en tenant compte du leader global et du multi-classe.

## v1 Requirements

### Fuel Strategy

- [x] **FUEL-01**: Le calcul des tours restants est base sur la position du leader global (tous classes confondus), pas sur les tours parcourus par le joueur
- [x] **FUEL-02**: Les tours effectues sous Safety Car / VSC sont detectes et exclus de la moyenne de consommation par tour
- [x] **FUEL-03**: L'utilisateur peut configurer une marge de securite (en tours, defaut = 1) ajoutee au calcul de carburant a ajouter au prochain pit stop

### UI Customization

- [x] **UI-01**: L'utilisateur peut repositionner chaque panneau overlay par drag & drop pendant une session de configuration
- [x] **UI-02**: L'utilisateur peut redimensionner librement chaque panneau overlay (largeur et hauteur)
- [x] **UI-03**: L'utilisateur peut choisir parmi plusieurs themes visuels (au minimum : dark actuel + 2 nouveaux themes)
- [x] **UI-04**: Les positions et tailles des panneaux sont sauvegardees dans des profils separes pour l'affichage 2D (ecran) et VR

## v2 Requirements

### VR Rendering (defere — priorite apres stabilisation v1)

- [x] **VR-01**: Proof-of-concept pipeline SkiaSharp 3.119.2 + D3D11 backend sur un overlay unique avant migration complete
- **VR-02**: Migration du rendu VR de `RenderTargetBitmap` vers rendu direct dans textures swapchain OpenXR (tous overlays)

### Fuel — VE Hypercar (defere)

- **FUEL-04**: Chemin de calcul separe pour les voitures hybrides/electriques : prediction energie virtuelle (%) a ajouter coherente avec les tours restants multi-classe

### Performance (defere — a integrer dans phases ulterieures)

- **PERF-01**: VR frame submission deplace sur thread dedie hors UI thread
- **PERF-02**: Profiling et budget du DispatcherTimer 60Hz — mesure du budget reel par overlay

## Out of Scope

| Feature | Reason |
|---------|--------|
| Application mobile ou web | Overlay desktop/VR uniquement |
| Connexion cloud / multi-driver | Donnees locales uniquement (shared memory) |
| Support simulateurs autres que LMU/rF2 | Hors scope v2.x |
| AI engineer / strategiste IA | Couvert par des outils dedies (Crew Chief, Smart Race Engineer) |
| Telemetrie recording / replay | Couvert par TinyPedal — duplication inutile |
| Multi-driver stint scheduling | Outil dedie hors scope overlay |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FUEL-01 | Phase 1 — Fuel Strategy Correctness | Complete |
| FUEL-02 | Phase 1 — Fuel Strategy Correctness | Complete |
| FUEL-03 | Phase 1 — Fuel Strategy Correctness | Complete |
| VR-01 | Phase 01.1 — Render Tech Evaluation | Complete |
| UI-01 | Phase 2 — UI Customization | Complete |
| UI-02 | Phase 2 — UI Customization | Complete |
| UI-03 | Phase 2 — UI Customization | Complete |
| UI-04 | Phase 2 — UI Customization | Complete |

**Coverage:**
- v1 requirements: 7 total
- Mapped to phases: 7
- Unmapped: 0

---
*Requirements defined: 2026-05-19*
*Last updated: 2026-05-19 — Traceability confirmed against ROADMAP.md*
