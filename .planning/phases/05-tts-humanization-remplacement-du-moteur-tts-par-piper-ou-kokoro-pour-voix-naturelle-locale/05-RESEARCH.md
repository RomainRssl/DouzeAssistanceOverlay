# Phase 05: TTS Humanization - Research

**Researched:** 2026-05-20
**Domain:** Piper TTS CLI integration, C# Process.Start stdin, WPF VoicePanel, GeneralSettings model extension
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **Moteur TTS** : Piper (vs Kokoro) — modèle `fr_FR-siwis-medium.onnx`
- **Distribution** : Piper livré dans `piper\` à côté de l'exe (piper.exe + .onnx + .json)
- **Invocation CLI** : `piper.exe --model piper\fr_FR-siwis-medium.onnx --output_file voice\piper\{key}.wav` (texte via stdin)
- **Chemins WAV** : `voice\piper\{key}.wav` (sous VoiceRootDir existant)
- **VoicePackName** auto-réglé sur "piper" après la première génération
- **SpeakSync()** inchangé — le mécanisme `_wavPackDir` gère déjà la lecture des WAV Piper
- **Fallback** : si WAV Piper absent, SAPI prend le relais (comportement actuel conservé)
- **AlertTexts** : `Dictionary<string, string>` dans `GeneralSettings` avec 23 clés/valeurs
- **VoicePanel** : 23 TextBox + bouton "Appliquer", scrollable
- **Régénération sélective** : seuls les WAV dont le texte a changé sont régénérés

### Claude's Discretion
- Gestion des erreurs si piper.exe absent ou échoue (message d'erreur dans VoicePanel)
- UI exacte de la liste des alertes dans VoicePanel (accordéon par catégorie vs liste plate)
- Groupement des alertes (drapeaux / carburant / position / spotter / tour)

### Deferred Ideas (OUT OF SCOPE)
- Sélection de la voix Piper (dropdown pour plusieurs modèles .onnx)
- Ajout d'alertes personnalisées (clés custom)
- Support Kokoro en alternative
- Preview audio de chaque alerte depuis VoicePanel
</user_constraints>

---

## Summary

Phase 5 remplace le moteur SAPI robotique par Piper TTS (voix VITS neurales) pour les 23 alertes vocales de VoiceService. L'architecture est un ajout pur : le mécanisme WAV pack existant (`_wavPackDir` / `SpeakSync()`) est réutilisé sans modification. Le travail consiste à (1) étendre le modèle `GeneralSettings` avec `AlertTexts`, (2) relier les textes hardcodés de VoiceService à ce dictionnaire, et (3) ajouter une section "Textes des alertes" dans VoicePanel avec un bouton Appliquer qui génère les WAV via `Process.Start`.

Le point critique de recherche : Piper sur Windows **ne supporte pas** `--text "string"` comme argument (le processus se fige en attente de stdin). La seule méthode fiable est de piper le texte via **stdin** (`RedirectStandardInput = true`, écrire le texte, fermer le stream). La clé est de fermer `StandardInput` avant `WaitForExit()` pour éviter un deadlock.

**Primary recommendation:** Invoquer piper.exe via `Process.Start` avec `RedirectStandardInput=true`, écrire le texte sur stdin, fermer le stream, puis `WaitForExit(timeout)` avec un timeout de 15 secondes par WAV.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Diagnostics.Process | .NET 8 BCL | Spawn piper.exe, stdin/stdout redirection | Déjà utilisé dans VoicePanel.cs (Process.Start explorer) |
| System.IO (File, Directory, Path) | .NET 8 BCL | Écriture/lecture WAV, détection piper.exe | Déjà utilisé dans VoiceService et ConfigService |
| Newtonsoft.Json | Déjà référencé | Sérialisation Dictionary<string,string> AlertTexts | Déjà le sérialiseur de l'app |

### Piper Binary (distribué avec l'app)
| Fichier | Taille approx. | Rôle |
|---------|---------------|------|
| `piper\piper.exe` | ~15 MB | Exécutable Windows amd64 — release 2023.11.14-2 |
| `piper\fr_FR-siwis-medium.onnx` | ~63 MB | Modèle VITS voix française naturelle |
| `piper\fr_FR-siwis-medium.onnx.json` | ~quelques KB | Config du modèle (requis par piper.exe) |

**Note sur le nommage :** Le modèle publié sur Hugging Face utilise des noms génériques `model.onnx` / `model.onnx.json`. Il faudra renommer lors de la distribution ou adapter le chemin `--model` en conséquence. La convention dans CONTEXT.md (`fr_FR-siwis-medium.onnx`) est la convention Rhasspy officielle pour les releases GitHub — c'est le nom standard dans les releases `piper_windows_amd64.zip`.

### Source du modèle
- Releases GitHub rhasspy/piper : https://github.com/rhasspy/piper/releases/tag/2023.11.14-2
- Modèle fr_FR-siwis-medium sur Hugging Face : https://huggingface.co/Trelis/piper-fr-fr-siwis-medium

---

## Architecture Patterns

### Recommended Project Structure
```
piper\                              # dossier distribué avec l'app
├── piper.exe                       # binaire Windows amd64
├── fr_FR-siwis-medium.onnx         # modèle (~63 MB)
└── fr_FR-siwis-medium.onnx.json    # config du modèle

voice\
└── piper\                          # pack vocal auto-créé par VoicePanel
    ├── BlueFlagWarning.wav
    ├── RedFlag.wav
    └── ... (23 fichiers .wav)
```

### Pattern 1 : Invocation Piper via stdin (CRITIQUE)

**Ce qu'il faut savoir :** Piper Windows ne supporte PAS `--text "string"` comme argument direct — le processus se fige en attente de stdin. Il faut obligatoirement utiliser `RedirectStandardInput`.

**Pattern C# validé :**
```csharp
// Source: investigation GitHub issues rhasspy/piper #810 + .NET docs
private static bool GenerateWav(string piperExe, string modelPath, string outputWav, string text)
{
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputWav)!);

        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName               = piperExe,
            Arguments              = $"--model \"{modelPath}\" --output_file \"{outputWav}\"",
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardError  = true,   // éviter buffer overflow sur erreurs
            CreateNoWindow         = true,
        };

        proc.Start();

        // Écrire le texte sur stdin, puis fermer pour signaler EOF
        proc.StandardInput.WriteLine(text);
        proc.StandardInput.Close();  // CRITIQUE : fermer avant WaitForExit

        // Timeout de sécurité (génération d'un WAV court <3s normalement)
        bool exited = proc.WaitForExit(15_000);
        if (!exited) proc.Kill();

        return exited && proc.ExitCode == 0 && File.Exists(outputWav);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Piper] Erreur génération '{outputWav}': {ex.Message}");
        return false;
    }
}
```

**Anti-pattern à éviter :**
```csharp
// NE PAS FAIRE — se fige sur Windows (issue #810)
Arguments = $"--model \"{modelPath}\" --output_file \"{outputWav}\" --text \"{text}\""
```

### Pattern 2 : Génération batch depuis VoicePanel

Le bouton "Appliquer" compare les textes actuels avec `_settings.AlertTexts`, identifie les changements, puis génère uniquement les WAV modifiés séquentiellement (pas en parallèle — piper.exe est CPU-intensif).

```csharp
private async void OnApplyPiperTexts(object s, RoutedEventArgs e)
{
    if (_settings == null || _config == null || _appConfig == null) return;

    var piperExe   = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper", "piper.exe");
    var modelPath  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper", "fr_FR-siwis-medium.onnx");
    var outputDir  = Path.Combine(VoiceService.VoiceRootDir, "piper");

    if (!File.Exists(piperExe))
    {
        TbPiperStatus.Text = "Erreur : piper.exe introuvable dans piper\\";
        return;
    }

    // Collecter les textes depuis les TextBox
    var newTexts = CollectAlertTexts();

    // Identifier les WAV à régénérer
    var toGenerate = newTexts
        .Where(kv => !_settings.AlertTexts.TryGetValue(kv.Key, out var old) || old != kv.Value)
        .ToList();

    if (toGenerate.Count == 0)
    {
        TbPiperStatus.Text = "Aucun changement détecté.";
        return;
    }

    TbPiperStatus.Text = $"Génération en cours… (0/{toGenerate.Count})";
    BtnApplyPiper.IsEnabled = false;

    int done = 0;
    bool allOk = true;

    await Task.Run(() =>
    {
        foreach (var (key, text) in toGenerate)
        {
            var outputWav = Path.Combine(outputDir, $"{key}.wav");
            bool ok = GenerateWav(piperExe, modelPath, outputWav, text);
            if (!ok) allOk = false;

            done++;
            Dispatcher.Invoke(() =>
                TbPiperStatus.Text = $"Génération en cours… ({done}/{toGenerate.Count})");
        }
    });

    // Mettre à jour le modèle et sauvegarder
    foreach (var (key, text) in newTexts)
        _settings.AlertTexts[key] = text;

    _settings.VoicePackName = "piper";
    _config.Save(_appConfig);
    _voice?.SetWavPack("piper");

    // Rafraîchir le ComboBox pack vocal
    _loading = true;
    PopulateVoicePacks("piper");
    _loading = false;

    TbPiperStatus.Text = allOk
        ? $"{toGenerate.Count} WAV générés avec succès."
        : "Génération terminée avec des erreurs. Voir les logs.";
    BtnApplyPiper.IsEnabled = true;
}
```

### Pattern 3 : Ajout de `AlertTexts` dans `GeneralSettings`

```csharp
// Dans OverlayConfig.cs — classe GeneralSettings
private Dictionary<string, string> _alertTexts = new();

public Dictionary<string, string> AlertTexts
{
    get => _alertTexts;
    set { _alertTexts = value ?? new Dictionary<string, string>(); OnPropertyChanged(); }
}

// Méthode helper pour obtenir le texte avec fallback sur les defaults
public string GetAlertText(string key, string defaultText)
    => _alertTexts.TryGetValue(key, out var txt) && !string.IsNullOrWhiteSpace(txt)
        ? txt : defaultText;
```

**Migration JSON :** `Newtonsoft.Json` désérialise un `Dictionary<string,string>` manquant en `null`. L'initialiseur `= new()` garantit que le dict est non-null pour une config.json ancienne. Les 23 textes par défaut sont injectés au premier lancement si `AlertTexts` est vide.

### Pattern 4 : VoiceService — liaison avec AlertTexts

Remplacer les strings hardcodées par `_settings.GetAlertText(key, defaultText)` :

```csharp
// Avant (hardcodé) :
Enqueue(_urgentQueue, new SpeechItem("BlueFlagWarning", "Drapeau bleu, laisse passer", ...));

// Après (depuis AlertTexts) :
Enqueue(_urgentQueue, new SpeechItem("BlueFlagWarning",
    _settings.GetAlertText("BlueFlagWarning", "Drapeau bleu, laisse passer"), ...));
```

Les 23 occurrences hardcodées dans VoiceService suivent ce même pattern.

### Anti-Patterns to Avoid

- **`--text "string"` en argument CLI** : se fige sur Windows, n'utiliser que stdin
- **Génération parallèle** : piper.exe est CPU-intensif, risque de surchauffe/contention; séquentiel
- **WaitForExit() sans timeout** : deadlock potentiel si piper plante; toujours passer un timeout
- **Oublier de fermer StandardInput** : piper attend EOF pour traiter l'entrée; `Close()` obligatoire avant `WaitForExit()`
- **Pas de RedirectStandardError** : si piper écrit beaucoup sur stderr et que le buffer est plein, le processus peut se bloquer
- **Générer tous les WAV même si texte inchangé** : comparer avec `_settings.AlertTexts` pour la régénération sélective

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Conversion texte → WAV | Implémentation VITS/ONNX en C# | piper.exe via Process.Start | Piper embarque onnxruntime, espeak-ng, une pipeline complète — ~5000 lignes de C++ |
| Initialisation des 23 textes par défaut | Migration système complexe | `Dictionary<string,string>` avec initialiseur vide + GetAlertText fallback | Simple, pas de migration nécessaire, backward compatible |
| Détection des changements | Diff complexe | `old != kv.Value` string equality | Les textes sont courts, comparaison directe suffisante |
| Progress UI pour génération async | Framework MVVM | `Dispatcher.Invoke` + TextBlock status | Pattern déjà utilisé dans l'app (code-behind pur) |

**Key insight :** Piper embarque toute la pipeline TTS (modèle ONNX + onnxruntime + espeak-ng pour phonétisation française). Il n'y a rien à installer côté app — c'est un exécutable autonome.

---

## Common Pitfalls

### Pitfall 1 : `--text` argument gèle piper.exe sur Windows
**What goes wrong :** `piper.exe --model model.onnx --output_file out.wav --text "bonjour"` — le processus démarre, charge le modèle, puis se fige indéfiniment.
**Why it happens :** Piper lit toujours depuis stdin en boucle. L'argument `--text` n'est pas implémenté dans la version Windows binaire (différent de la version Python).
**How to avoid :** Utiliser `RedirectStandardInput=true`, écrire sur stdin, fermer le stream.
**Warning signs :** Process ne se termine jamais, `WaitForExit()` sans timeout bloque l'UI thread.

### Pitfall 2 : Deadlock `StandardInput` non fermé
**What goes wrong :** `proc.StandardInput.WriteLine(text)` sans `proc.StandardInput.Close()` — piper attend d'autres lignes et ne génère pas le WAV.
**Why it happens :** Piper lit stdin ligne par ligne en boucle. EOF (fermeture du stream) est le signal de fin.
**How to avoid :** Toujours `proc.StandardInput.Close()` immédiatement après l'écriture, avant `WaitForExit`.
**Warning signs :** Timeout déclenché, fichier WAV absent.

### Pitfall 3 : `voice\piper\` non créé
**What goes wrong :** `--output_file voice\piper\BlueFlagWarning.wav` échoue si le dossier n'existe pas.
**Why it happens :** piper.exe ne crée pas les dossiers intermédiaires.
**How to avoid :** `Directory.CreateDirectory(outputDir)` avant la boucle de génération.
**Warning signs :** ExitCode non-zero, fichier absent.

### Pitfall 4 : `AppDomain.CurrentDomain.BaseDirectory` en développement
**What goes wrong :** En mode debug dans Visual Studio, `BaseDirectory` pointe vers `bin\Debug\net8.0-windows\`, pas vers le dossier du projet. Le dossier `piper\` n'existe que dans la distribution.
**Why it happens :** Comportement standard de .NET pendant le développement.
**How to avoid :** Documenter que `piper\` doit être copié dans le répertoire de sortie, ou ajouter la vérification d'existence (`File.Exists(piperExe)`) avec message d'erreur clair dans VoicePanel.
**Warning signs :** Message "piper.exe introuvable" lors des tests manuels en dev.

### Pitfall 5 : `AlertTexts` null après désérialisation ancienne config
**What goes wrong :** `config.json` sans le champ `AlertTexts` → Newtonsoft.Json assigne `null` au property → NullReferenceException dans VoicePanel.
**Why it happens :** JSON sans la clé = null pour les types référence.
**How to avoid :** Initialiseur `= new Dictionary<string, string>()` dans la classe, ET null-check dans `ConfigService.Load()` si nécessaire.
**Warning signs :** NullReferenceException au démarrage sur les anciennes configs.

### Pitfall 6 : `PopulateVoicePacks` après génération
**What goes wrong :** Les WAV sont générés dans `voice\piper\`, le VoicePackName est mis à "piper", mais le ComboBox `CbVoicePack` n'est pas rafraîchi — l'utilisateur voit toujours "(TTS système — aucun pack)".
**Why it happens :** `PopulateVoicePacks` doit être appelé explicitement après que le dossier `voice\piper\` a été créé.
**How to avoid :** Appeler `PopulateVoicePacks("piper")` dans le callback du bouton Appliquer, après la génération.
**Warning signs :** ComboBox ne montre pas "piper" malgré la génération réussie.

---

## Code Examples

### Invocation Piper correcte (stdin, Windows)
```csharp
// Source : investigation GitHub rhasspy/piper issue #810 + .NET BCL docs
// Pattern validé : stdin uniquement, timeout de sécurité
proc.StartInfo = new ProcessStartInfo
{
    FileName               = piperExePath,
    Arguments              = $"--model \"{modelPath}\" --output_file \"{outputWav}\"",
    UseShellExecute        = false,
    RedirectStandardInput  = true,
    RedirectStandardError  = true,
    CreateNoWindow         = true,
};
proc.Start();
proc.StandardInput.WriteLine(text);
proc.StandardInput.Close();          // EOF → piper génère le WAV et quitte
bool ok = proc.WaitForExit(15_000);
```

### Initialisation des 23 textes par défaut dans VoiceService ou au démarrage
```csharp
// Dans VoiceService ou App.xaml.cs — appeler après Load() si AlertTexts vide
private static readonly Dictionary<string, string> DefaultAlertTexts = new()
{
    ["BlueFlagWarning"]     = "Drapeau bleu, laisse passer",
    ["RedFlag"]             = "Drapeau rouge, arrêt immédiat",
    ["GreenFlag"]           = "Drapeau vert, go",
    ["CheckeredFlag"]       = "Drapeau à damiers, belle course",
    ["YellowFlag"]          = "Drapeau jaune, prudence",
    ["YellowFlagPitClosed"] = "Jaune, pit fermé",
    ["YellowFlagResume"]    = "Reprise, drapeau vert",
    ["FuelWindowOpen"]      = "Fenêtre de pit ouverte",
    ["FuelLowLaps"]         = "Moins de trois tours de carburant",
    ["FuelCritical"]        = "Carburant critique",
    ["GapCloseBehind"]      = "Attention derrière",
    ["GapLostAhead"]        = "Tu décroches de la voiture devant",
    ["PositionGained"]      = "Position gagnée",
    ["PositionLost"]        = "Position perdue",
    ["SpotterClear"]        = "Dégagé",
    ["Spotter_1"]           = "Voiture à gauche",
    ["Spotter_2"]           = "Voiture à droite",
    ["Spotter_3"]           = "Voitures des deux côtés",
    ["SectorBeat_S1"]       = "Secteur un, meilleur temps",
    ["SectorBeat_S2"]       = "Secteur deux, meilleur temps",
    ["SectorBeat_S3"]       = "Secteur trois, meilleur temps",
    ["NewPersonalBest"]     = "Nouveau meilleur tour, bravo",
    ["__test__"]            = "Douze Assistance, système vocal actif",
};

public static void EnsureDefaultAlertTexts(GeneralSettings settings)
{
    foreach (var (key, val) in DefaultAlertTexts)
        settings.AlertTexts.TryAdd(key, val);
}
```

### Groupement UI dans VoicePanel (Claude's Discretion)
```xml
<!-- Suggestion : sections avec header collapsible ou simple séparateur -->
<!-- Groupes : DRAPEAUX (7) | CARBURANT (3) | GAP & POSITION (4) | SPOTTER (4) | TOURS (4) + TEST (1) -->
<TextBlock Text="DRAPEAUX" ... />
<!-- BlueFlagWarning, RedFlag, GreenFlag, CheckeredFlag, YellowFlag, YellowFlagPitClosed, YellowFlagResume -->
<TextBlock Text="CARBURANT" ... />
<!-- FuelWindowOpen, FuelLowLaps, FuelCritical -->
<!-- etc. -->
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| System.Speech.Synthesis (SAPI) | Piper TTS (VITS neurales) | Phase 5 | Voix naturelle vs voix robotique |
| Textes hardcodés dans VoiceService | `GeneralSettings.AlertTexts` dict | Phase 5 | L'utilisateur peut customiser |
| Pas de génération WAV dans l'app | Génération batch via Process.Start | Phase 5 | WAV pack "piper" auto-créé |

**Deprecated/outdated dans cette phase :**
- Strings hardcodées dans VoiceService → remplacées par `_settings.GetAlertText(key, default)`
- `--text` argument Piper (version Python pip seulement, pas le binaire Windows)

---

## Open Questions

1. **Nom exact du fichier .json de config du modèle**
   - What we know: Piper requiert un fichier `.onnx.json` à côté du `.onnx`. Le nom doit correspondre exactement (ex: `fr_FR-siwis-medium.onnx.json`).
   - What's unclear: Est-ce que piper.exe le trouve automatiquement par convention de nommage (même nom + .json), ou faut-il un argument `--config` explicite ?
   - Recommendation: Tester lors de l'exécution. Si piper.exe ne le trouve pas automatiquement, ajouter `--config "piper\fr_FR-siwis-medium.onnx.json"` aux arguments. La convention rhasspy est que le .json doit avoir le même nom de base que le .onnx.

2. **Génération async et thread-safety de VoicePanel**
   - What we know: La génération est lancée depuis le UI thread via `await Task.Run(...)`. VoiceService tourne sur son propre thread speech et lit `_wavPackDir` de façon volatile.
   - What's unclear: Si VoiceService est en train de parler pendant la génération, peut-il lire un WAV partiellement écrit ?
   - Recommendation: La génération écrit dans `voice\piper\{key}.wav`. Si le fichier est en cours d'écriture et VoiceService tente de lire le même fichier, `SoundPlayer` lèvera une exception — mais SpeakSync() l'attrape déjà (`catch (Exception ex)`). Pas de problème de cohérence.

3. **Compatibilité ucrtbase.dll sur anciennes machines Windows 10**
   - What we know: Des issues GitHub mentionnent des crashs ucrtbase.dll avec certaines builds de piper.exe sur Windows 10.
   - What's unclear: La release 2023.11.14-2 est-elle affectée ?
   - Recommendation: Utiliser la release `2023.11.14-2` (la plus récente). Documenter la compatibilité Windows 10 x64 dans le guide utilisateur.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.0 |
| Config file | LMUOverlay.Tests.csproj (`net8.0-windows`, `UseWPF=false`) |
| Quick run command | `dotnet test --filter "Category=PiperTTS" -v minimal` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Behavior | Test Type | Automated Command | Pattern |
|----------|-----------|-------------------|---------|
| `AlertTexts` dans `GeneralSettings` survit JSON round-trip | unit | `dotnet test --filter "Category=PiperTTS"` | Comme TwitchVisualConfigTests |
| Ancienne config.json sans `AlertTexts` désérialise sans crash | unit | `dotnet test --filter "Category=PiperTTS"` | Comme TwitchVisualDefaultsTests |
| `GetAlertText` retourne la valeur du dict si présente | unit | `dotnet test --filter "Category=PiperTTS"` | Pure C# |
| `GetAlertText` retourne le fallback si clé absente | unit | `dotnet test --filter "Category=PiperTTS"` | Pure C# |
| 23 clés présentes dans `DefaultAlertTexts` | unit | `dotnet test --filter "Category=PiperTTS"` | Count assertion |
| Bouton Appliquer ne régénère que les WAV modifiés | manual | - | Vérification manuelle (nécessite piper.exe) |
| WAV piper joués par VoiceService.SpeakSync() | manual | - | Vérification manuelle en session |

### Sampling Rate
- **Per task commit :** `dotnet test --filter "Category=PiperTTS" -v minimal`
- **Per wave merge :** `dotnet test`
- **Phase gate :** Full suite green avant `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `LMUOverlay.Tests\PiperTTS\AlertTextsTests.cs` — couvre le round-trip JSON, GetAlertText, defaults
- [ ] Aucune nouvelle infrastructure xUnit nécessaire (framework déjà en place)

---

## Sources

### Primary (HIGH confidence)
- Code source `VoiceService.cs` (lu directement) — architecture SpeakSync, _wavPackDir, SpeechItem
- Code source `VoicePanel.xaml.cs` (lu directement) — pattern Initialize, PopulateVoicePacks, OnSave
- Code source `OverlayConfig.cs` (lu directement) — structure GeneralSettings, pattern propriétés INotifyPropertyChanged
- Code source `ConfigService.cs` (lu directement) — pattern Save(AppConfig), Newtonsoft.Json
- Code source tests existants (lus directement) — pattern TDD RED stubs, Trait Category, xUnit

### Secondary (MEDIUM confidence)
- GitHub rhasspy/piper issue #810 — confirmation que `--text` gèle sur Windows, stdin obligatoire
- .NET docs `ProcessStartInfo.RedirectStandardInput` — UseShellExecute=false requis, Close() avant WaitForExit
- Hugging Face Trelis/piper-fr-fr-siwis-medium — taille modèle ~63 MB confirmée
- dev.to article piper offline — syntaxe stdin `echo text | piper --model ... --output_file ...`

### Tertiary (LOW confidence)
- GitHub discussions #379, #577 — exemples Windows, mais confirmés par l'issue #810 ci-dessus

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — code existant lu directement, binaires Piper bien documentés
- Architecture: HIGH — patterns Process.Start stdin validés par issues GitHub + .NET docs
- Pitfalls: HIGH — pitfall stdin/--text confirmé par issue #810 avec reproduction exacte
- Test patterns: HIGH — infrastructure xUnit existante, pattern TDD RED stubs documenté dans les tests précédents

**Research date:** 2026-05-20
**Valid until:** 2026-08-20 (Piper archivé, stable; .NET 8 BCL stable)
