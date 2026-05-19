# Phase 2: UI Customization - Context

**Gathered:** 2026-05-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Le pilote peut arranger, dimensionner et thématiser chaque panneau overlay librement, avec des profils de layout séparés pour l'affichage 2D (écran) et VR. Cela inclut : un mode édition global pour drag/resize, des thèmes visuels présets additionnels, des surcharges de couleurs par overlay, des contrôles d'opacité par overlay, le snap-to-grid pendant le drag, et des profils 2D/VR indépendants.

**Note critique :** L'utilisateur souhaite une Phase 1.5 (évaluation technologie de rendu : WPF vs SkiaSharp vs D3D11) AVANT d'exécuter la Phase 2. Voir section Deferred.

</domain>

<decisions>
## Implementation Decisions

### Mode édition global (Config session)
- Un bouton unique "Déverrouiller tout" dans MainWindow déverrouille TOUS les overlays simultanément
- En mode édition : une bordure colorée (accent primary) apparaît sur chaque overlay + les poignées de resize sont visibles
- Le snap-to-grid est actif pendant le drag : grille de 10px par défaut
- Le mode édition ne se désactive QUE sur clic explicite "Verrouiller tout" — pas de verrouillage automatique, même au démarrage de session de jeu

### Thèmes (3 nouveaux présets à livrer)
- **Racing Clair** : fond clair/blanc, texte sombre, lisibilité maximale (conditions lumineuses, écrans lumineux)
- **Bleu Nuit LMP2** : fond très sombre bleu-marine, accents bleus (#0090FF, couleur LMP2), atmosphère Le Mans nuit
- **Minimaliste Mono** : fond noir pur, texte blanc, zéro couleur d'accent — overlay discret, style e-sport
- L'éditeur de thèmes existant (ThemesTab avec color pickers et live preview) est conservé tel quel — aucune modification
- Total final : 4 présets (Endurance Noir existant + 3 nouveaux)

### Couleurs par overlay (scope étendu — essentiel pour l'utilisateur)
- Chaque overlay peut surcharger des couleurs spécifiques (fond, texte, accent) indépendamment du thème global
- Les surcharges sont stockées dans les settings de l'overlay (OverlaySettings) — probablement via CustomOptions ou un nouveau champ ColorOverrides
- Accessible depuis l'UI d'édition au niveau de chaque overlay (quand sélectionné/focalisé en mode édition)

### Opacité et fond par overlay
- Opacity et BackgroundOpacity sont déjà dans OverlaySettings — les exposer dans l'UI d'édition par-overlay
- Contrôles accessibles pendant le mode édition (sliders ou inputs dans une petite barre contextuelle)

### Profils 2D/VR (UI-04)
- Basculement automatique : quand VREnabled toggle dans AppConfig, l'app charge le profil de layout correspondant
- Première initialisation du profil VR : copie du layout 2D actuel comme point de départ
- Positions 2D restent dans les champs actuels (PosX, PosY, OverlayWidth, OverlayHeight dans OverlaySettings)
- Positions VR nécessitent de nouveaux champs dédiés dans OverlaySettings (ex. VrPosX, VrPosY, VrWidth, VrHeight)
- Modifier le profil 2D ne touche pas le profil VR, et vice versa

### Claude's Discretion
- Design exact de la barre contextuelle per-overlay en mode édition (position, taille, contenu)
- Animation/transition visuelle au basculement de profil 2D/VR
- Comportement du snap quand deux overlays s'alignent (snap inter-overlays ou grille globale seulement)
- Rendu exact des bordures colorées en mode édition (épaisseur, couleur exacte, animation)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `BaseOverlayWindow` : drag-to-reposition (OnMouseDown/OnMouseMove/OnMouseUp) et resize (edgeRight, edgeBottom, corner grip) déjà implémentés — contrôlés par `IsLocked`. Mode édition global = toggle IsLocked sur tous les overlays à la fois.
- `OverlaySettings` : PosX, PosY, OverlayWidth, OverlayHeight, Opacity, BackgroundOpacity, IsLocked, Scale, CustomOptions déjà présents. Ajouter VrPosX/VrPosY/VrWidth/VrHeight.
- `ThemeManager` : système de thèmes JSON avec hot-reload via événement `ThemeChanged`. Tous les overlays y souscrivent via `OnThemeChanged()`. Ajouter les 3 fichiers JSON de thèmes dans `%AppData%/DouzeAssistance/themes/`.
- `ThemesTab` : éditeur complet (color pickers, effets, new/duplicate/delete/import/export, live preview) — aucune modification nécessaire pour les thèmes présets.
- `AppConfig.VREnabled` : flag VR existant — surveiller ce changement pour déclencher le switch de profil layout.
- `ConfigService` : sauvegarde vers `%AppData%/DouzeAssistance/config.json` via Newtonsoft.Json — backward compat garantie si on ajoute des champs nullables.

### Established Patterns
- Code-behind C# pur (pas de MVVM/binding) pour les overlays — continuer ce pattern pour le mode édition
- `INotifyPropertyChanged` sur OverlaySettings — les changements de profil VR/2D doivent passer par PropertyChanged pour que BaseOverlayWindow réagisse automatiquement
- Tous les overlays héritent de `BaseOverlayWindow` — un changement global IsLocked peut être fait via `OverlayManager` qui tient la liste de tous les overlays ouverts

### Integration Points
- `OverlayManager` : gère la liste de tous les overlays actifs — point d'entrée pour le bouton "Déverrouiller tout" (itérer sur tous les overlays et setter `Settings.IsLocked = false`)
- `MainWindow` : ajouter le bouton global unlock/lock
- `AppConfig.VREnabled` : observer ce champ pour déclencher le basculement de profil layout (dans OverlayManager ou App.xaml.cs)

</code_context>

<specifics>
## Specific Ideas

- Le bouton "Déverrouiller tout / Verrouiller tout" devrait être très visible dans MainWindow (pas caché dans un sous-menu) — le pilote l'utilise avant chaque session de configuration
- Les surcharges couleur par overlay permettent par exemple de mettre le FuelStrategy en rouge vif et le Standings en bleu, même avec le thème Endurance Noir actif
- Le snap 10px est le comportement par défaut — pas besoin de le rendre configurable pour l'instant

</specifics>

<deferred>
## Deferred Ideas

- **Phase 1.5 — Évaluation technologie de rendu** : L'utilisateur souhaite une évaluation WPF vs SkiaSharp vs D3D11 AVANT d'exécuter la Phase 2. Cette phase doit être insérée en Phase 1.5 via `/gsd:insert-phase`. Utiliser cette commande pour ajouter la phase au roadmap avant de lancer `/gsd:plan-phase 2`.
- **Z-order (ordre d'affichage entre overlays)** : Contrôle quel overlay s'affiche devant un autre en cas de chevauchement — à ajouter au backlog pour une phase ultérieure.

</deferred>

---

*Phase: 02-ui-customization*
*Context gathered: 2026-05-19*
