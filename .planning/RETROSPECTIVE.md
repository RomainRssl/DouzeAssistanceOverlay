# Retrospective: LMUOverlay — Douze Assistance

---

## Milestone: v1.0 — MVP

**Shipped:** 2026-05-22
**Phases:** 6 | **Plans:** 19 | **Commits:** 209 | **Timeline:** 60 jours (2026-03-23 → 2026-05-22)

### What Was Built

1. Fuel strategy correcte : calcul multi-classe sur leader global + filtrage Safety Car + marge configurable
2. PoC render tech SkiaSharp 3.119.2 : mesures frame-time, recommandation WPF pour v1.0 / SkiaSharp différé VR-02
3. UI Customization complète : drag snap-to-grid, resize edges+corner, 3 thèmes, profils 2D/VR
4. WebBrowser Overlay WebView2 : navigation live, init async, gestion erreurs, barre de statut
5. TwitchChat visuel : bandeau image, toggle, color pickers fond+accent en temps réel
6. TTS Humanization : 23 textes éditables + Piper TTS stdin (fr_FR VITS), popup guide installation

### What Worked

- **TDD RED→GREEN sur chaque phase** — xUnit stubs avant implémentation a prévenu les régressions silencieuses (FuelStrategyCalculator, VrProfileHelper, AlertTexts)
- **Phases incrémentales courtes** — 3 plans max par phase a maintenu le contexte propre et les commits atomiques
- **Pure static helpers** (FuelStrategyCalculator, VrProfileHelper, SnapGridHelper) — pas de dépendances WPF → testables en isolation, pattern clair pour la suite
- **Constructor injection pour les configs** — FuelStrategyConfig injectée dans DataService, élimine les constantes hardcodées
- **Décision early de Phase 01.1** — évaluer le rendu avant UI Customization a évité une migration hasardeuse au milieu de la phase 2

### What Was Inefficient

- **Requirements des phases 3-5 jamais ajoutés à REQUIREMENTS.md** — WEB, TWITCH-V, TTS définis uniquement dans ROADMAP, gap de traçabilité découvert au complete-milestone
- **Bugs post-phase sur WebBrowserOverlay** — 4 commits de fix après la phase 3 (race condition URL, _initFailed, dossier WebView2, barre de statut) — la phase aurait dû inclure un test d'intégration plus complet
- **Drag & drop non fluide découvert après la livraison** — bug d'oscillation avec PointToScreen corrigé hors GSD ; aurait dû être couvert par les tests d'UI ou attrapé dans la phase 2
- **STATE.md non mis à jour en cours de route** — percent resté à 70% alors que 100% terminé

### Patterns Established

- `CaptureMouse() + PointToScreen()` pour le drag WPF — coordonnées screen-space, delta total depuis le début (pas incrémental relatif fenêtre)
- Piper TTS : `RedirectStandardInput=true + Close() avant WaitForExit(15000)` — jamais `--text` (freeze Windows issue #810)
- WebView2 : `AllowsTransparency=false` obligatoire avant `Show()`, dossier UserDataFolder explicite, `_pendingUrl` pour l'URL reçue avant init
- Pattern de test TDD pour les phases de config : définir les stubs au Plan 01 (RED), implémenter au Plan 02 (GREEN), UI au Plan 03

### Key Lessons

- Ajouter les requirements WEB/TWITCH/TTS à REQUIREMENTS.md DÈS la création de la phase dans ROADMAP, pas à la fin
- Les overlays HWND (WebView2) ont des contraintes spécifiques à documenter dès la conception : pas de WPF transparency, z-order HWND toujours au-dessus des visuels WPF
- Les corrections de bugs post-phase doivent être tracées comme Known Issues dans MILESTONES.md si elles ne méritent pas une phase dédiée

### Cost Observations

- Sessions : ~10 sessions sur 60 jours
- Notable : phases courtes (3 plans) = sessions efficaces, pas de dépassement de contexte

---

## Cross-Milestone Trends

| Métrique | v1.0 MVP |
|----------|----------|
| Phases | 6 |
| Plans | 19 |
| Commits | 209 |
| LOC net | +22 724 |
| Timeline | 60 jours |
| TDD coverage | Phases 1-5 (stubs RED→GREEN) |
