using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views
{
    public partial class VoicePanel : UserControl
    {
        private GeneralSettings? _settings;
        private VoiceService?    _voice;
        private ConfigService?   _config;
        private AppConfig?       _appConfig;
        private bool             _loading;

        public VoicePanel() => InitializeComponent();

        // ================================================================
        // INITIALISATION
        // ================================================================

        public void Initialize(GeneralSettings settings, VoiceService voice,
                               ConfigService config, AppConfig appConfig)
        {
            _settings  = settings;
            _voice     = voice;
            _config    = config;
            _appConfig = appConfig;

            _loading = true;

            // Afficher le chemin du dossier voix
            RunVoiceDir.Text = $" {VoiceService.VoiceRootDir}\\<nom_du_pack>\\";

            // Peupler les packs vocaux disponibles
            PopulateVoicePacks(settings.VoicePackName);

            // Peupler la liste des voix installées (crée un synth temporaire)
            try
            {
                using var tmp = new SpeechSynthesizer();
                var voices = tmp.GetInstalledVoices();

                // Voix par défaut (utilise la sélection automatique fr-FR)
                CbVoice.Items.Add(new ComboBoxItem
                {
                    Content = "(Voix système par défaut)",
                    Tag     = ""
                });

                foreach (var v in voices)
                {
                    CbVoice.Items.Add(new ComboBoxItem
                    {
                        Content = $"{v.VoiceInfo.Name} ({v.VoiceInfo.Culture})",
                        Tag     = v.VoiceInfo.Name
                    });
                }

                // Sélectionner la voix sauvegardée
                int selIdx = 0;
                for (int i = 0; i < CbVoice.Items.Count; i++)
                {
                    if (CbVoice.Items[i] is ComboBoxItem item &&
                        item.Tag?.ToString() == settings.VoiceName)
                    {
                        selIdx = i;
                        break;
                    }
                }
                CbVoice.SelectedIndex = selIdx;
            }
            catch
            {
                CbVoice.Items.Add(new ComboBoxItem
                {
                    Content = "(SAPI non disponible)",
                    Tag     = ""
                });
                CbVoice.SelectedIndex = 0;
            }

            // Lier les contrôles
            ChkEnabled.IsChecked  = settings.VoiceEnabled;
            ChkFuel.IsChecked     = settings.AlertFuel;
            ChkFlags.IsChecked    = settings.AlertFlags;
            ChkGap.IsChecked      = settings.AlertGap;
            ChkLap.IsChecked      = settings.AlertLap;
            ChkPosition.IsChecked = settings.AlertPosition;
            ChkSpotter.IsChecked  = settings.AlertSpotter;

            SlVolume.Value       = settings.VoiceVolume;
            SlRate.Value         = settings.VoiceRate;
            SlGapThreshold.Value = settings.GapAlertThresholdSeconds;

            TbVolume.Text        = $"{settings.VoiceVolume}%";
            TbRate.Text          = settings.VoiceRate.ToString("+0;-0;0");
            TbGapThreshold.Text  = $"{settings.GapAlertThresholdSeconds:F1}s";

            // Cooldowns
            SlCooldownFuel.Value     = settings.CooldownFuelCriticalSeconds;
            SlCooldownBlue.Value     = settings.CooldownBlueFlagSeconds;
            SlCooldownYellow.Value   = settings.CooldownYellowFlagSeconds;
            SlCooldownGap.Value      = settings.CooldownGapSeconds;
            SlCooldownPosition.Value = settings.CooldownPositionSeconds;
            SlCooldownSpotter.Value  = settings.CooldownSpotterSeconds;

            TbCooldownFuel.Text     = $"{settings.CooldownFuelCriticalSeconds}s";
            TbCooldownBlue.Text     = $"{settings.CooldownBlueFlagSeconds}s";
            TbCooldownYellow.Text   = $"{settings.CooldownYellowFlagSeconds}s";
            TbCooldownGap.Text      = $"{settings.CooldownGapSeconds}s";
            TbCooldownPosition.Text = $"{settings.CooldownPositionSeconds}s";
            TbCooldownSpotter.Text  = $"{settings.CooldownSpotterSeconds}s";

            _loading = false;

            // Peupler les TextBox des textes d'alertes Piper (Phase 5)
            PopulatePiperTexts();
        }

        // ================================================================
        // HANDLERS
        // ================================================================

        private void PopulateVoicePacks(string selectedPack)
        {
            CbVoicePack.Items.Clear();
            CbVoicePack.Items.Add(new ComboBoxItem { Content = "(TTS système — aucun pack)", Tag = "" });

            var root = VoiceService.VoiceRootDir;
            if (Directory.Exists(root))
            {
                foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d))
                {
                    var name = Path.GetFileName(dir);
                    CbVoicePack.Items.Add(new ComboBoxItem { Content = name, Tag = name });
                }
            }

            int idx = 0;
            for (int i = 0; i < CbVoicePack.Items.Count; i++)
            {
                if (CbVoicePack.Items[i] is ComboBoxItem ci &&
                    ci.Tag?.ToString() == selectedPack)
                { idx = i; break; }
            }
            CbVoicePack.SelectedIndex = idx;
        }

        private void OnTest(object s, RoutedEventArgs e) => _voice?.SpeakTest();

        private void OnVoicePackChanged(object s, SelectionChangedEventArgs e)
        {
            if (_loading || _voice == null) return;
            if (CbVoicePack.SelectedItem is ComboBoxItem item)
                _voice.SetWavPack(item.Tag?.ToString() ?? "");
        }

        private void OnOpenVoiceDir(object s, RoutedEventArgs e)
        {
            var dir = VoiceService.VoiceRootDir;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }

        private void OnRefreshPacks(object s, RoutedEventArgs e)
        {
            var currentPack = _settings?.VoicePackName ?? "";
            _loading = true;
            PopulateVoicePacks(currentPack);
            _loading = false;
        }

        private void OnVoiceChanged(object s, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CbVoice.SelectedItem is ComboBoxItem item)
            {
                _settings.VoiceName = item.Tag?.ToString() ?? "";
                _voice?.ApplySettings();
            }
        }

        private void OnVolumeChanged(object s, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            _settings.VoiceVolume = (int)SlVolume.Value;
            TbVolume.Text = $"{_settings.VoiceVolume}%";
            _voice?.ApplySettings();
        }

        private void OnRateChanged(object s, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            _settings.VoiceRate = (int)SlRate.Value;
            TbRate.Text = _settings.VoiceRate.ToString("+0;-0;0");
            _voice?.ApplySettings();
        }

        private void OnGapThresholdChanged(object s, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            _settings.GapAlertThresholdSeconds = Math.Round(SlGapThreshold.Value, 1);
            TbGapThreshold.Text = $"{_settings.GapAlertThresholdSeconds:F1}s";
        }

        private void OnCooldownChanged(object s, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            _settings.CooldownFuelCriticalSeconds = (int)SlCooldownFuel.Value;
            _settings.CooldownBlueFlagSeconds     = (int)SlCooldownBlue.Value;
            _settings.CooldownYellowFlagSeconds   = (int)SlCooldownYellow.Value;
            _settings.CooldownGapSeconds          = (int)SlCooldownGap.Value;
            _settings.CooldownPositionSeconds     = (int)SlCooldownPosition.Value;
            _settings.CooldownSpotterSeconds      = (int)SlCooldownSpotter.Value;

            TbCooldownFuel.Text     = $"{_settings.CooldownFuelCriticalSeconds}s";
            TbCooldownBlue.Text     = $"{_settings.CooldownBlueFlagSeconds}s";
            TbCooldownYellow.Text   = $"{_settings.CooldownYellowFlagSeconds}s";
            TbCooldownGap.Text      = $"{_settings.CooldownGapSeconds}s";
            TbCooldownPosition.Text = $"{_settings.CooldownPositionSeconds}s";
            TbCooldownSpotter.Text  = $"{_settings.CooldownSpotterSeconds}s";
        }

        private void OnSettingChanged(object s, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            _settings.VoiceEnabled  = ChkEnabled.IsChecked  == true;
            _settings.AlertFuel     = ChkFuel.IsChecked     == true;
            _settings.AlertFlags    = ChkFlags.IsChecked    == true;
            _settings.AlertGap      = ChkGap.IsChecked      == true;
            _settings.AlertLap      = ChkLap.IsChecked      == true;
            _settings.AlertPosition = ChkPosition.IsChecked == true;
            _settings.AlertSpotter  = ChkSpotter.IsChecked  == true;
        }

        private void OnSave(object s, RoutedEventArgs e)
        {
            if (_settings == null || _config == null || _appConfig == null) return;

            // Mettre à jour depuis les sliders (pour être sûr)
            _settings.VoiceEnabled  = ChkEnabled.IsChecked  == true;
            _settings.AlertFuel     = ChkFuel.IsChecked     == true;
            _settings.AlertFlags    = ChkFlags.IsChecked    == true;
            _settings.AlertGap      = ChkGap.IsChecked      == true;
            _settings.AlertLap      = ChkLap.IsChecked      == true;
            _settings.AlertPosition = ChkPosition.IsChecked == true;
            _settings.AlertSpotter  = ChkSpotter.IsChecked  == true;
            _settings.VoiceVolume   = (int)SlVolume.Value;
            _settings.VoiceRate     = (int)SlRate.Value;
            _settings.GapAlertThresholdSeconds = Math.Round(SlGapThreshold.Value, 1);

            _settings.CooldownFuelCriticalSeconds = (int)SlCooldownFuel.Value;
            _settings.CooldownBlueFlagSeconds     = (int)SlCooldownBlue.Value;
            _settings.CooldownYellowFlagSeconds   = (int)SlCooldownYellow.Value;
            _settings.CooldownGapSeconds          = (int)SlCooldownGap.Value;
            _settings.CooldownPositionSeconds     = (int)SlCooldownPosition.Value;
            _settings.CooldownSpotterSeconds      = (int)SlCooldownSpotter.Value;

            if (CbVoice.SelectedItem is ComboBoxItem item)
                _settings.VoiceName = item.Tag?.ToString() ?? "";

            _config.Save(_appConfig);
            _voice?.ApplySettings();
        }

        // ================================================================
        // PIPER TTS — TEXTES DES ALERTES (Phase 5)
        // ================================================================

        private static readonly Dictionary<string, string> _piperTextBoxMapping = new()
        {
            { "TbAlert_BlueFlagWarning",     "BlueFlagWarning" },
            { "TbAlert_RedFlag",             "RedFlag" },
            { "TbAlert_GreenFlag",           "GreenFlag" },
            { "TbAlert_CheckeredFlag",       "CheckeredFlag" },
            { "TbAlert_YellowFlag",          "YellowFlag" },
            { "TbAlert_YellowFlagPitClosed", "YellowFlagPitClosed" },
            { "TbAlert_YellowFlagResume",    "YellowFlagResume" },
            { "TbAlert_FuelWindowOpen",      "FuelWindowOpen" },
            { "TbAlert_FuelLowLaps",         "FuelLowLaps" },
            { "TbAlert_FuelCritical",        "FuelCritical" },
            { "TbAlert_GapCloseBehind",      "GapCloseBehind" },
            { "TbAlert_GapLostAhead",        "GapLostAhead" },
            { "TbAlert_PositionGained",      "PositionGained" },
            { "TbAlert_PositionLost",        "PositionLost" },
            { "TbAlert_SpotterClear",        "SpotterClear" },
            { "TbAlert_Spotter_1",           "Spotter_1" },
            { "TbAlert_Spotter_2",           "Spotter_2" },
            { "TbAlert_Spotter_3",           "Spotter_3" },
            { "TbAlert_SectorBeat_S1",       "SectorBeat_S1" },
            { "TbAlert_SectorBeat_S2",       "SectorBeat_S2" },
            { "TbAlert_SectorBeat_S3",       "SectorBeat_S3" },
            { "TbAlert_NewPersonalBest",     "NewPersonalBest" },
            { "TbAlert_Test",                "__test__" },
        };

        private void PopulatePiperTexts()
        {
            if (_settings == null) return;
            TbPiperModelPath.Text = _settings.PiperModelPath;
            foreach (var (tbName, key) in _piperTextBoxMapping)
            {
                if (FindName(tbName) is TextBox tb)
                    tb.Text = _settings.GetAlertText(key, "");
            }
        }

        private Dictionary<string, string> CollectAlertTexts()
        {
            var result = new Dictionary<string, string>();
            foreach (var (tbName, key) in _piperTextBoxMapping)
            {
                if (FindName(tbName) is TextBox tb)
                    result[key] = tb.Text.Trim();
            }
            return result;
        }

        // IMPORTANT : N'utiliser JAMAIS --text en argument — se fige sur Windows (issue #810).
        private static (bool ok, string error) GenerateWav(string piperExe, string modelPath, string outputWav, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputWav)!);

                using var proc = new System.Diagnostics.Process();
                proc.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = piperExe,
                    Arguments              = $"--model \"{modelPath}\" --output_file \"{outputWav}\"",
                    WorkingDirectory       = Path.GetDirectoryName(piperExe)!,
                    UseShellExecute        = false,
                    RedirectStandardInput  = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };

                proc.Start();

                proc.StandardInput.WriteLine(text);
                proc.StandardInput.Close();  // CRITIQUE : fermer avant WaitForExit

                bool exited = proc.WaitForExit(15_000);
                string stderr = proc.StandardError.ReadToEnd().Trim();

                if (!exited) { proc.Kill(); return (false, "Timeout (>15s)"); }
                if (proc.ExitCode != 0) return (false, $"Exit {proc.ExitCode}: {stderr}");
                if (!File.Exists(outputWav)) return (false, $"WAV absent apres generation. {stderr}");

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async void OnApplyPiperTexts(object s, RoutedEventArgs e)
        {
            if (_settings == null || _config == null || _appConfig == null) return;

            var baseDir   = AppDomain.CurrentDomain.BaseDirectory;
            var piperExe  = Path.Combine(baseDir, "piper", "piper.exe");
            var savedPath = TbPiperModelPath.Text.Trim();
            var modelPath = string.IsNullOrWhiteSpace(savedPath)
                ? Path.Combine(baseDir, "piper", "fr_FR-mls-medium.onnx")
                : (Path.IsPathRooted(savedPath) ? savedPath : Path.Combine(baseDir, savedPath));
            var outputDir = Path.Combine(VoiceService.VoiceRootDir, "piper");

            if (!File.Exists(piperExe))
            {
                TbPiperStatus.Text = "Erreur : piper.exe introuvable dans piper\\";
                return;
            }

            if (!File.Exists(modelPath))
            {
                TbPiperStatus.Text = $"Erreur : modele introuvable — {modelPath}";
                return;
            }

            var newTexts   = CollectAlertTexts();
            var toGenerate = newTexts
                .Where(kv => _settings.GetAlertText(kv.Key, "") != kv.Value
                         || !File.Exists(Path.Combine(outputDir, $"{kv.Key}.wav")))
                .ToList();

            if (toGenerate.Count == 0)
            {
                TbPiperStatus.Text = "Aucun changement detecte et tous les WAV existent deja.";
                return;
            }

            TbPiperStatus.Text = $"Generation en cours... (0/{toGenerate.Count})";
            BtnApplyPiper.IsEnabled = false;

            int done      = 0;
            bool allOk    = true;
            string lastErr = "";

            await Task.Run(() =>
            {
                foreach (var (key, text) in toGenerate)
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var outputWav = Path.Combine(outputDir, $"{key}.wav");
                    var (ok, err) = GenerateWav(piperExe, modelPath, outputWav, text);
                    if (!ok) { allOk = false; lastErr = err; }
                    done++;
                    Dispatcher.Invoke(() =>
                        TbPiperStatus.Text = $"Generation en cours... ({done}/{toGenerate.Count})");
                }
            });

            foreach (var (key, text) in newTexts)
                _settings.AlertTexts[key] = text;

            _settings.PiperModelPath = savedPath;
            _settings.VoicePackName  = "piper";
            _config.Save(_appConfig);
            _voice?.SetWavPack("piper");

            _loading = true;
            PopulateVoicePacks("piper");
            _loading = false;

            TbPiperStatus.Text = allOk
                ? $"{toGenerate.Count} WAV generes avec succes."
                : $"Erreur Piper : {lastErr}";
            BtnApplyPiper.IsEnabled = true;
        }
    }
}
