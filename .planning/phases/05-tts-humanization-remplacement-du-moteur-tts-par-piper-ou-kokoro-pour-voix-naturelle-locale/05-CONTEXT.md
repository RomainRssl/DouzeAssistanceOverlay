# Phase 5: TTS Humanization - Context

**Gathered:** 2026-05-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Remplacer le moteur TTS SAPI robotique (System.Speech.Synthesis) par Piper (local, gratuit, voix VITS naturelles) pour les 23 alertes vocales de VoiceService. L'utilisateur peut éditer le texte de chaque alerte dans l'onglet Audio, puis cliquer "Appliquer" pour régénérer les WAV via Piper. Piper est livré avec l'application. La logique de déclenchement des alertes (CheckFlags, CheckFuel, etc.) est hors scope.

</domain>

<decisions>
## Implementation Decisions

### Moteur TTS
- **Piper** — moteur sélectionné (vs Kokoro)
- Modèle : `fr_FR-siwis-medium.onnx` (voix française naturelle)
- Livré avec l'app dans un sous-dossier `piper\` (piper.exe + modèle .onnx + config .json)
- Invocation CLI : `piper.exe --model piper\fr_FR-siwis-medium.onnx --text "Drapeau bleu" --output_file voice\piper\BlueFlagWarning.wav`
- Pas d'action requise de l'utilisateur pour installer Piper

### Intégration avec le système WAV pack existant
- Les WAV générés par Piper sont sauvegardés dans `voice\piper\{key}.wav`
- `VoicePackName` est automatiquement réglé sur "piper" à la première génération
- `VoiceService.SpeakSync()` utilise déjà le mécanisme `_wavPackDir` — aucun changement dans VoiceService pour la lecture
- Fallback : si un WAV Piper est absent, SAPI prend le relais (comportement actuel conservé)

### Textes éditables des alertes
- Les 23 textes d'alertes hardcodés dans VoiceService sont extraits vers `GeneralSettings.AlertTexts`
  - Type : `Dictionary<string, string>` — clé = alert key (ex: "BlueFlagWarning"), valeur = texte
  - Valeurs par défaut identiques aux textes actuels dans VoiceService
- VoicePanel (onglet Audio) affiche un champ TextBox par alerte, scrollable
- Bouton **"Appliquer"** en bas de la section :
  1. Sauvegarde tous les textes modifiés dans config.json via ConfigService
  2. Lance Piper en `Process.Start` pour chaque alerte modifiée (génération batch)
  3. Met à jour `VoicePackName = "piper"` dans GeneralSettings
  4. Affiche un indicateur de progression (ou simple message "Génération en cours…")

### Distribution et chemins
- `piper\piper.exe` — dans le répertoire de l'exécutable (`AppDomain.CurrentDomain.BaseDirectory`)
- `piper\fr_FR-siwis-medium.onnx` + `piper\fr_FR-siwis-medium.onnx.json` — même dossier
- WAV générés dans `voice\piper\` (sous VoiceRootDir existant)
- Taille indicative : piper.exe ~15 MB + modèle ~63 MB → ~80 MB à distribuer

### Claude's Discretion
- Gestion des erreurs si piper.exe absent ou échoue (message d'erreur dans VoicePanel)
- UI exacte de la liste des alertes dans VoicePanel (accordéon par catégorie vs liste plate)
- Groupement des alertes (drapeaux / carburant / position / spotter / tour)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `VoiceService.SpeakSync()` (ligne 484) : essaie WAV d'abord, fallback SAPI — aucune modification nécessaire pour la lecture Piper
- `VoiceService._wavPackDir` : chemin résolu par `settings.VoicePackName` + `VoiceRootDir` — Piper alimente ce mécanisme
- `VoiceService.VoiceRootDir` (ligne 77) : `voice\` à côté de l'exe ou `%APPDATA%\DouzeAssistance\voice\`
- `VoicePanel.PopulateVoicePacks()` (ligne 126) : scanne VoiceRootDir et peuple le ComboBox — "piper" apparaîtra automatiquement après génération
- `GeneralSettings.VoicePackName` (ligne 421) : string dans config.json — recevoir "piper"
- `ConfigService` : pattern `_config.Save()` déjà utilisé dans VoicePanel pour sauvegarder les settings

### Les 23 alert keys et textes par défaut (à extraire vers AlertTexts)
| Key | Texte par défaut |
|-----|-----------------|
| BlueFlagWarning | Drapeau bleu, laisse passer |
| RedFlag | Drapeau rouge, arrêt immédiat |
| GreenFlag | Drapeau vert, go |
| CheckeredFlag | Drapeau à damiers, belle course |
| YellowFlag | Drapeau jaune, prudence |
| YellowFlagPitClosed | Jaune, pit fermé |
| YellowFlagResume | Reprise, drapeau vert |
| FuelWindowOpen | Fenêtre de pit ouverte |
| FuelLowLaps | Moins de trois tours de carburant |
| FuelCritical | Carburant critique |
| GapCloseBehind | Attention derrière |
| GapLostAhead | Tu décroches de la voiture devant |
| PositionGained | Position gagnée |
| PositionLost | Position perdue |
| SpotterClear | Dégagé |
| Spotter_1 | Voiture à gauche |
| Spotter_2 | Voiture à droite |
| Spotter_3 | Voitures des deux côtés |
| SectorBeat_S1 | Secteur un, meilleur temps |
| SectorBeat_S2 | Secteur deux, meilleur temps |
| SectorBeat_S3 | Secteur trois, meilleur temps |
| NewPersonalBest | Nouveau meilleur tour, bravo |
| __test__ | Douze Assistance, système vocal actif |

### Integration Points
- `GeneralSettings` (OverlayConfig.cs:318) : ajouter `Dictionary<string, string> AlertTexts`
- `VoiceService` (VoiceService.cs) : remplacer les 23 strings hardcodées par `_settings.AlertTexts[key]`
- `VoicePanel.xaml` + `.xaml.cs` : nouvelle section "Textes des alertes" avec 23 TextBox + bouton Appliquer
- `VoicePanel` → appel `Process.Start("piper\piper.exe", args)` pour la génération batch

</code_context>

<specifics>
## Specific Ideas

- Le bouton "Appliquer" ne régénère que les WAV dont le texte a changé depuis la dernière génération (comparaison avec les textes actuellement dans AlertTexts)
- La section textes peut être organisée en groupes pliables : Drapeaux / Carburant / Positions / Spotter / Secteurs

</specifics>

<deferred>
## Deferred Ideas

- Sélection de la voix Piper (dropdown pour choisir entre plusieurs modèles .onnx) — hors scope Phase 5
- Ajout d'alertes personnalisées (clés custom) — hors scope
- Support de Kokoro en alternative — hors scope (Piper choisi)
- Preview audio de chaque alerte depuis VoicePanel — hors scope Phase 5

</deferred>

---

*Phase: 05-tts-humanization*
*Context gathered: 2026-05-20*
