# Requirements: LMUOverlay — Douze Assistance

**Defined:** 2026-05-19
**Core Value:** Le pilote sait en un coup d'œil combien d'essence/énergie ajouter au pit stop pour finir la course — en tenant compte du leader global et du multi-classe.

## v1 Requirements

### Fuel Strategy

- [ ] **FUEL-01**: Le calcul des tours restants est basé sur la position du leader global (tous classes confondus), pas sur les tours parcourus par le joueur
- [ ] **FUEL-02**: Les tours effectués sous Safety Car / VSC sont détectés et exclus de la moyenne de consommation par tour
- [ ] **FUEL-03**: L'utilisateur peut configurer une marge de sécurité (en tours, défaut = 1) ajoutée au calcul de carburant à ajouter au prochain pit stop

### UI Customization

- [ ] **UI-01**: L'utilisateur peut repositionner chaque panneau overlay par drag & drop pendant une session de configuration
- [ ] **UI-02**: L'utilisateur peut redimensionner librement chaque panneau overlay (largeur et hauteur)
- [ ] **UI-03**: L'utilisateur peut choisir parmi plusieurs thèmes visuels (au minimum : dark actuel + 2 nouveaux thèmes)
- [ ] **UI-04**: Les positions et tailles des panneaux sont sauvegardées dans des profils séparés pour l'affichage 2D (écran) et VR

## v2 Requirements

### VR Rendering (déféré — priorité après stabilisation v1)

- **VR-01**: Proof-of-concept pipeline SkiaSharp 3.119.2 + D3D11 backend sur un overlay unique avant migration complète
- **VR-02**: Migration du rendu VR de `RenderTargetBitmap` vers rendu direct dans textures swapchain OpenXR (tous overlays)

### Fuel — VE Hypercar (déféré)

- **FUEL-04**: Chemin de calcul séparé pour les voitures hybrides/électriques : prédiction énergie virtuelle (%) à ajouter cohérente avec les tours restants multi-classe

### Performance (déféré — à intégrer dans phases ultérieures)

- **PERF-01**: VR frame submission déplacé sur thread dédié hors UI thread
- **PERF-02**: Profiling et budget du DispatcherTimer 60Hz — mesure du budget réel par overlay

## Out of Scope

| Feature | Reason |
|---------|--------|
| Application mobile ou web | Overlay desktop/VR uniquement |
| Connexion cloud / multi-driver | Données locales uniquement (shared memory) |
| Support simulateurs autres que LMU/rF2 | Hors scope v2.x |
| AI engineer / stratégiste IA | Couvert par des outils dédiés (Crew Chief, Smart Race Engineer) |
| Télémétrie recording / replay | Couvert par TinyPedal — duplication inutile |
| Multi-driver stint scheduling | Outil dédié hors scope overlay |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FUEL-01 | Phase 1 | Pending |
| FUEL-02 | Phase 1 | Pending |
| FUEL-03 | Phase 1 | Pending |
| UI-01 | Phase 2 | Pending |
| UI-02 | Phase 2 | Pending |
| UI-03 | Phase 2 | Pending |
| UI-04 | Phase 2 | Pending |

**Coverage:**
- v1 requirements: 7 total
- Mapped to phases: 7
- Unmapped: 0 ✓

---
*Requirements defined: 2026-05-19*
*Last updated: 2026-05-19 après définition initiale*
