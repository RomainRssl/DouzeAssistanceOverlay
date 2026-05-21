using System.Windows;

namespace LMUOverlay.Views
{
    public partial class PiperGuideWindow : Window
    {
        private const string GuideText =
"""
ETAPE 1 — Télécharger Piper (l'exécutable)
───────────────────────────────────────────

1. Aller sur : https://github.com/rhasspy/piper/releases/latest
2. Télécharger : piper_windows_amd64.zip
3. Extraire dans : C:\Program Files\Douze Assistance\piper\

   Le dossier doit contenir :
     piper.exe
     espeak-ng.dll
     onnxruntime.dll
     onnxruntime_providers_shared.dll
     piper_phonemize.dll
     libtashkeel_model.ort
     espeak-ng-data\  (dossier)


ETAPE 2 — Télécharger le modèle de voix française
──────────────────────────────────────────────────

1. Aller sur :
   https://huggingface.co/speaches-ai/piper-fr_FR-mls-medium/tree/main

2. Télécharger ces 2 fichiers :
     model.onnx    (~76 Mo)
     config.json   (~7 Ko)

3. Créer le dossier :
   C:\Program Files\Douze Assistance\piper\voices\

4. Placer les fichiers en les renommant :
     model.onnx   -->  fr_FR-mls-medium.onnx
     config.json  -->  fr_FR-mls-medium.onnx.json

   IMPORTANT : config.json DOIT s'appeler
   fr_FR-mls-medium.onnx.json (avec .onnx.json à la fin).

   Structure finale :
     piper\
       piper.exe
       espeak-ng.dll  (+ autres DLL)
       espeak-ng-data\
       voices\
         fr_FR-mls-medium.onnx        (76 Mo)
         fr_FR-mls-medium.onnx.json   (7 Ko)


ETAPE 3 — Vérifier que Piper fonctionne
────────────────────────────────────────

Ouvrir PowerShell dans :
C:\Program Files\Douze Assistance\piper\

Taper :
  echo "Bonjour" | .\piper.exe --model "voices\fr_FR-mls-medium.onnx" --output_file "$env:TEMP\test.wav"

Si tout est correct, la sortie affiche :
  [info] Loaded voice in X second(s)
  [info] Initialized piper
  [info] Real-time factor: ...
  [info] Terminated piper


ETAPE 4 — Configurer dans Douze Assistance
───────────────────────────────────────────

1. Onglet Audio → section "TEXTES DES ALERTES — PIPER TTS"
2. Champ "Modele .onnx" : piper\voices\fr_FR-mls-medium.onnx
3. Vérifier ou modifier les textes des alertes
4. Cliquer APPLIQUER → génère 23 WAV
5. Statut : "23 WAV generés avec succès."


PROBLEMES CONNUS
────────────────

"Erreur : modele introuvable"
  → Vérifier que le fichier .onnx fait ~76 Mo (pas 7 Ko)
    et que le chemin dans "Modele .onnx" est correct.

"Exit -1073740791" (crash Piper)
  → Le fichier fr_FR-mls-medium.onnx.json est manquant
    ou mal nommé. Il doit s'appeler exactement
    fr_FR-mls-medium.onnx.json

"Aucun changement détecté"
  → Tous les WAV existent déjà. Supprimer le dossier
    %APPDATA%\DouzeAssistance\voice\piper\
    puis recliquer APPLIQUER.
""";

        public PiperGuideWindow()
        {
            InitializeComponent();
            TbGuide.Text = GuideText;
        }

        private void OnClose(object s, RoutedEventArgs e) => Close();
    }
}
