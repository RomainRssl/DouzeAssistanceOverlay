# LMUOverlay — Douze Assistance

## What This Is

Overlay temps-réel pour Le Mans Ultimate (simulateur rFactor 2), affiché par-dessus le jeu en 2D et en VR (OpenXR). L'application "Douze Assistance" lit la shared memory rF2 pour afficher panneaux de stratégie carburant, classements, chronos, télémétrie, assistance vocale, navigateur web et chat Twitch. Développée en WPF .NET 8, version actuelle v1.0 MVP.

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
- ✓ **FUEL-01** : calcul tours restants basé sur le leader global multi-classe — v1.0
- ✓ **FUEL-02** : tours Safety Car / VSC exclus de la moyenne de consommation — v1.0
- ✓ **FUEL-03** : marge de sécurité configurable (défaut 1 tour) — v1.0
- ✓ **VR-01** : PoC SkiaSharp 3.119.2 + recommandation render tech validée — v1.0
- ✓ **UI-01** : drag & drop avec snap-to-grid pour repositionner chaque overlay — v1.0
- ✓ **UI-02** : redimensionnement libre (edges + corner grip) pour chaque overlay — v1.0
- ✓ **UI-03** : 3 thèmes visuels (dark existant + 2 nouveaux presets JSON) — v1.0
- ✓ **UI-04** : profils layout 2D et VR indépendants, persistés séparément — v1.0
- ✓ **WEB-01/04** : overlay WebView2 flottant avec URL configurable, navigation live — v1.0
- ✓ **TWITCH-V-01/07** : personnalisation visuelle TwitchChat (bandeau image, couleurs) — v1.0
- ✓ **TTS** : 23 textes d'alertes vocales configurables + Piper TTS stdin (fr_FR) — v1.0

### Active

- [ ] **VR-02** : migration rendu VR de RenderTargetBitmap vers textures swapchain OpenXR via SkiaSharp/D3D11
- [ ] **FUEL-04** : prédiction énergie VE (%) cohérente avec les tours multi-classe réels
- [ ] **PERF-01** : VR frame submission sur thread dédié hors UI thread
- [ ] **PERF-02** : profiling et budget DispatcherTimer 60Hz par overlay

### Out of Scope

- Application mobile ou web — overlay desktop/VR uniquement
- Connexion en ligne / cloud — données locales uniquement (shared memory)
- Support d'autres simulations que LMU/rF2 — hors scope v2.x
- AI strategist / engineer — couvert par Crew Chief, Smart Race Engineer
- Télémétrie recording / replay — couvert par TinyPedal
- Multi-driver stint scheduling — outil dédié hors scope overlay

## Context

v1.0 MVP livré le 2026-05-22. 6 phases, 19 plans, 209 commits, ~22 700 lignes nettes.

**Stack actuelle :** WPF .NET 8 + SharpDX (DirectInput) + Silk.NET.OpenXR + SkiaSharp 3.119.2 (PoC) + WebView2 (1.0.2849.39) + Piper TTS (processus externe stdin).

**Architecture :** `BaseOverlayWindow` gère drag/resize/snap/thèmes/profils pour tous les overlays. `DataService` centralise la lecture shared memory. `OverlayManager` construit les overlays et gère le timer 60Hz. `ConfigService` persist la config JSON. `VrProfileHelper` gère les profils layout 2D/VR.

**Pipeline VR :** `OpenXRService` capture via `RenderTargetBitmap` (CPU-only) → bottleneck identifié. Fix recommandé en VR-02 : SkiaSharp + D3D11 si live frame-time confirme ≥ 20% d'amélioration.

**Contraintes :** .NET 8 Windows uniquement. Backwards compat config JSON. PC de course (overhead minimal). VR OpenXR non cassé.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Rendu code-behind C# pur (pas MVVM/binding pour overlays) | Perf : évite les allocations de binding WPF sur timer 60Hz | ✓ Good — validé en prod |
| Architecture shared memory directe | Latence minimale, pas de dépendance réseau | ✓ Good |
| WPF conservé pour v1.0 (SkiaSharp différé à VR-02) | Phase 2 ne bénéficiait pas du changement de rendu ; données live manquaient | ✓ Good — Phase 2 livrée proprement en WPF |
| FuelStrategyCalculator pure static class | No WPF dependency → unit-testable | ✓ Good |
| VrProfileHelper pure static class | No WPF dependency → 6 tests unitaires | ✓ Good |
| AllowsTransparency=false pour WebBrowserOverlay | WebView2 HWND incompatible avec WPF transparency | ✓ Good — nécessaire, pas de workaround |
| URL TextBox volatile (non persistée) | Décision délibérée : l'URL est saisie à la session, pas sauvegardée | ✓ Good |
| Piper TTS via stdin (pas --text) | --text freeze sur Windows (issue #810) | ✓ Good — stdin stable |
| CaptureMouse + screen-space drag coords | Coordonnées relatives à la fenêtre causaient oscillation avec snap | ✓ Good — fix appliqué post-v1.0 |

---
*Last updated: 2026-05-22 après milestone v1.0 MVP*
