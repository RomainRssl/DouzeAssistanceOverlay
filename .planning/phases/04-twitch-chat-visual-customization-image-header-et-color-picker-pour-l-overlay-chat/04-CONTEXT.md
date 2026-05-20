# Phase 4: Twitch Chat Visual Customization - Context

**Gathered:** 2026-05-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Modifier TwitchChatOverlay pour (1) remplacer le texte "TWITCH" en bandeau par une image configurable par l'utilisateur, et (2) permettre de choisir via une roue chromatique les deux couleurs de l'overlay (fond + accent). Les modifications de comportement du chat (messages, connexion, TTS) sont hors scope.

</domain>

<decisions>
## Implementation Decisions

### Image header — sélection
- Bouton "Parcourir" dans la section TwitchChat de MainWindow (même zone que le TextBox Channel)
- Formats acceptés : PNG, JPG, BMP (filtre sur l'OpenFileDialog)
- Chemin de l'image sauvegardé dans `TwitchSettings.HeaderImagePath` (config.json)

### Image header — états d'affichage
- **Image sélectionnée** : l'image remplace le TextBlock "TWITCH"
- **Aucune image** : affiche le texte "TCHAT" (fallback texte)
- **Option "Masquer le bandeau"** : checkbox/toggle dans les settings — cache entièrement la ligne header (image OU texte, plus le séparateur)
- Ce toggle est sauvegardé dans `TwitchSettings` (config.json)

### Couleurs — ce que l'utilisateur contrôle
- **Couleur de fond** : correspond au `Background` de l'`outer Border` (actuellement `PanelBackground` alpha 200)
- **Couleur accent** : correspond à la barre séparateur (actuellement `#9146FF` Twitch Purple alpha 80)
- Les deux couleurs sont indépendamment personnalisables
- Sauvegardées dans `TwitchSettings.BackgroundColor` et `TwitchSettings.AccentColor` (hex strings, comme `InputGraphConfig.ThrottleColor`)

### Color picker UI
- Roue chromatique (color wheel) dans les settings Twitch de MainWindow
- Le researcher doit évaluer les options WPF disponibles : Xceed.Wpf.Toolkit ColorPicker (MIT), ou implémentation custom
- Un bouton "Reset" par couleur pour revenir aux valeurs par défaut (fond = PanelBackground alpha 200, accent = #9146FF)

### Persistance
- `TwitchSettings` dans config.json reçoit les nouveaux champs : `HeaderImagePath`, `ShowHeader`, `BackgroundColor`, `AccentColor`
- Si les champs sont absents du JSON existant (upgrade), les valeurs par défaut s'appliquent automatiquement (Newtonsoft.Json null → valeur initiale C#)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `TwitchChatOverlay.cs:34` — `outer Border` dont le `Background` doit devenir la couleur choisie
- `TwitchChatOverlay.cs:52-60` — TextBlock "TWITCH" à remplacer par un `Image` ou un `TextBlock "TCHAT"` conditionnel
- `TwitchChatOverlay.cs:79` — séparateur avec `Color.FromArgb(80, 145, 70, 255)` → couleur accent configurable
- `OverlayConfig.cs:869-871` — pattern `InputGraphConfig.ThrottleColor` (string hex) pour stocker des couleurs personnalisées
- `OverlayConfig.cs:917` — `ParseColor(string hex)` helper déjà disponible pour convertir hex → WPF Color
- `MainWindow.xaml.cs:481-532` — section TwitchChat existante où ajouter bouton Parcourir + color pickers

### Established Patterns
- Couleurs custom stockées en `string hex` dans le modèle config, converties via `ParseColor()`
- `CustomOptions` dictionary disponible sur chaque `OverlaySettings` pour des flags supplémentaires (ex: ShowHeader)
- Sliders et TextBox dans MainWindow construits en code-behind pur (pas XAML/MVVM) — même pattern pour les nouveaux contrôles

### Integration Points
- `TwitchSettings` (OverlayConfig.cs:70-74) : ajouter `HeaderImagePath`, `ShowHeader`, `BackgroundColor`, `AccentColor`
- `TwitchChatOverlay` constructeur : logique conditionnelle header (image vs texte vs caché)
- `TwitchChatOverlay` : écouter les changements de config pour mettre à jour les couleurs sans redémarrage (ou reload à chaud)
- MainWindow section TwitchChat : ajouter row image picker + 2 color pickers + reset buttons

</code_context>

<specifics>
## Specific Ideas

- L'image remplace physiquement le TextBlock "TWITCH" dans le header Grid — même emplacement, même contraintes de taille
- L'option "Masquer le bandeau" cache header + séparateur → le chat occupe toute la hauteur de l'overlay
- Les couleurs par défaut à restaurer via Reset : fond = `PanelBackground` alpha 200 (hex calculé au runtime), accent = `#9146FF` alpha ~31% (80/255)

</specifics>

<deferred>
## Deferred Ideas

- Personnalisation de la police des messages Twitch — hors scope
- Couleurs par utilisateur/badge Twitch — hors scope
- Animation ou transition sur le header — hors scope

</deferred>

---

*Phase: 04-twitch-chat-visual-customization*
*Context gathered: 2026-05-20*
