# Phase 3: Web Browser Overlay - Context

**Gathered:** 2026-05-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Nouvel overlay WPF qui embarque WebView2 (Chromium) pour afficher une page web depuis une URL saisie par l'utilisateur. L'overlay suit le pattern BaseOverlayWindow existant (drag via bandeau, resize libre, VR/2D profiles). Gestion de session TTS, audio, et intégrations Twitch sont hors scope.

</domain>

<decisions>
## Implementation Decisions

### Configuration de l'URL
- L'URL est saisie dans MainWindow (nouveau champ TextBox dans l'onglet HUD ou un onglet dédié)
- L'URL N'EST PAS persistée dans config.json — elle est volatile, remise à zéro à chaque lancement
- Pattern de référence : le TextBox du channel Twitch (MainWindow.xaml.cs:481) pour le style et le placement

### Comportement en cas d'échec
- Si l'URL est invalide (format incorrect) ou si la page échoue à charger → l'overlay se désactive (`IsEnabled = false`)
- Pas de message d'erreur affiché dans l'overlay — la désactivation silencieuse est suffisante

### Interactions avec le contenu
- L'utilisateur PEUT cliquer à l'intérieur du WebView2 (interactions web actives)
- Le drag-and-drop de l'overlay se fait exclusivement via le bandeau titre (comme tous les autres overlays)
- Pas de conflit attendu : le bandeau intercepte MouseDown en premier, WebView2 reçoit les clics dans sa zone

### Taille et refresh
- Claude's Discretion : taille par défaut, fréquence de refresh automatique (si pertinent), comportement au resize

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `BaseOverlayWindow` avec `UseRawResize = true` : mode resize libre sans Viewbox scaling — exact pattern pour un overlay WebView2 (même mode que TwitchChatOverlay)
- `OverlayHelper.MakeTitle("WEB")` : bandeau titre standard qui sert de drag handle
- `NoteOverlay` : référence pour overlay simple avec contenu interactif (pattern `UseRawResize`)
- `OverlayManager.Reg("WebBrowser", ...)` : enregistrement standard en une ligne

### Established Patterns
- Chaque overlay a une `OverlaySettings` dans `AppConfig` (position, taille, IsEnabled)
- `CustomOptions` dictionary pour stocker des paramètres overlay-spécifiques si besoin
- Overlay enregistré dans `OverlayManager` → automatiquement géré (show/hide, VR toggle, edit mode)

### Integration Points
- `AppConfig` : ajouter une propriété `public OverlaySettings WebBrowser { get; set; }`
- `OverlayManager` : ajouter `Reg("WebBrowser", () => new WebBrowserOverlay(...))`
- `MainWindow` : nouveau TextBox URL (pattern channel Twitch, ligne 481) + bouton "Charger"
- **Contrainte WebView2 + AllowsTransparency** : WebView2 est un contrôle HWND natif, incompatible avec `AllowsTransparency = true` (airspace WPF). Le researcher doit évaluer la solution (fenêtre opaque avec background personnalisé, ou autre workaround).

### NuGet à ajouter
- `Microsoft.Web.WebView2` (runtime WebView2 requis sur la machine — préinstallé sur Windows 11)

</code_context>

<specifics>
## Specific Ideas

- L'URL volatile (non persistée) correspond à un usage "je veux voir une page ponctuelle pendant une session" — ex. météo piste, live timing web, carte circuit
- Le bandeau titre sert de seul point de drag, comme pour TwitchChatOverlay qui a aussi du contenu interactif

</specifics>

<deferred>
## Deferred Ideas

- Persistance de l'URL entre sessions — décision explicite : hors scope Phase 3
- Historique d'URLs / favoris — future phase si demandé
- Plusieurs onglets / plusieurs WebBrowserOverlay — hors scope

</deferred>

---

*Phase: 03-web-browser-overlay*
*Context gathered: 2026-05-20*
