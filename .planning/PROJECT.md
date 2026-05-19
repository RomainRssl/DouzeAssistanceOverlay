# LMUOverlay — Douze Assistance

## What This Is

Overlay temps-réel pour Le Mans Ultimate (simulateur rFactor 2), affiché par-dessus le jeu en 2D et en VR (OpenXR). L'application "Douze Assistance" lit la shared memory rF2 pour afficher panneaux de stratégie carburant, classements, chronos, télémétrie et assistance vocale. Actuellement en production à la version 2.2.9.

## Core Value

Le pilote doit savoir en un coup d'œil combien d'essence/énergie ajouter à son prochain pit stop pour finir la course — en tenant compte du leader global et du multi-classe.

## Requirements

### Validated

- ✓ Overlay WPF multi-panneaux (Chrono, Classement, Telemetry, Voice, FuelStrategy) — existing
- ✓ Lecture shared memory rF2 via rF2SharedMemory — existing
- ✓ Support VR via OpenXR (Silk.NET.OpenXR) + affichage 2D simultané — existing
- ✓ Système de thèmes dark avec ThemeManager + BrushCache — existing
- ✓ Stratégie carburant basique : fuel/tour, tours restants, carburant à ajouter — existing
- ✓ Barre énergie virtuelle (VE) pour voitures hybrides/électriques — existing
- ✓ Données pneus : usure, compound, tours restants — existing
- ✓ Countdown distance entrée pit — existing
- ✓ Classement multi-classe avec positions — existing
- ✓ Assistance vocale (System.Speech) — existing
- ✓ Export données CSV + Excel (ClosedXML) — existing
- ✓ Auto-updater (AutoUpdater.NET) — existing

### Active

- [ ] Évaluation technologie de rendu : comparer WPF actuel vs alternatives (SkiaSharp, D3D11 pur, WriteableBitmap) sur critères perf, qualité visuelle, VR
- [ ] Personnalisation panneaux : drag & drop pour repositionner, redimensionnement libre de chaque panneau
- [ ] Système de thèmes étendu : plus de thèmes visuels, couleurs, styles configurables
- [ ] Refonte stratégie carburant : calcul multi-classe basé sur leader global (hypercar/LMP2/GT3), avec marge safety car
- [ ] Gestion énergie VE améliorée : prédiction énergie à ajouter cohérente avec le nombre de tours réels à effectuer
- [ ] Optimisation ressources : réduire empreinte CPU/GPU pendant la course

### Out of Scope

- Application mobile ou web — overlay desktop/VR uniquement
- Connexion en ligne / cloud — données locales uniquement (shared memory)
- Support d'autres simulations que LMU/rF2 — hors scope v2.x

## Context

L'app utilise une architecture overlay WPF avec fenêtres transparentes `AllowsTransparency=true`. Chaque panneau est un `BaseOverlayWindow` indépendant qui appelle `UpdateData()` sur timer. Le rendu VR passe par `OpenXRService` qui capture le rendu WPF via `RenderTargetBitmap` et le pousse en OpenXR — ce pipeline a des limitations de performance et de synchronisation.

La `DataService` centralise la lecture shared memory et expose des DTOs typés (`GetFuelData()`, `GetAllVehicles()`, etc.). Le calcul de `RaceLapsRemaining` actuel est simple et ne tient pas compte du leader global en multi-classe.

Technologie actuelle : WPF .NET 8 + SharpDX (DirectInput) + Silk.NET.OpenXR. Le rendu code-behind C# pur (pas de XAML data-binding pour les overlays) est déjà en place pour les performances.

## Constraints

- **Tech stack**: .NET 8 Windows — toute nouvelle technologie UI doit rester dans cet écosystème
- **VR**: Doit continuer à fonctionner en OpenXR sans casser le pipeline existant
- **Compatibilité**: L'app doit tourner sur des PC de course (pas forcément haut de gamme) — overhead minimal
- **Backwards compat**: La configuration existante (JSON) doit rester compatible après refonte UI

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Rendu code-behind C# pur (pas MVVM/binding pour overlays) | Perf : évite les allocations de binding WPF sur timer 60Hz | ✓ Good — pattern validé en prod |
| Architecture shared memory directe (pas d'API REST principale) | Latence minimale, pas de dépendance réseau | ✓ Good |
| Technologie de rendu alternative (évaluation à faire) | WPF a des limites perf/VR, besoin d'évaluer avant de choisir | — Pending |
| Calcul fuel multi-classe (à concevoir) | Le calcul actuel ignore le leader global | — Pending |

---
*Last updated: 2026-05-19 après initialisation*
