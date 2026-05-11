using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Media;
using System.Speech.Synthesis;
using LMUOverlay.Models;

namespace LMUOverlay.Services
{
    /// <summary>
    /// Service d'alertes vocales TTS via System.Speech.Synthesis (Windows SAPI).
    /// Thread dédié qui possède le SpeechSynthesizer — jamais accédé depuis l'UI thread.
    /// Deux files : urgent (drapeaux, carburant critique) et normal.
    /// Phase 2 : chargera des clips WAV depuis %APPDATA%\DouzeAssistance\voice\ si présents.
    /// </summary>
    public sealed class VoiceService : IDisposable
    {
        // ================================================================
        // TYPES INTERNES
        // ================================================================

        private record SpeechItem(string Key, string Text, TimeSpan Cooldown);

        // ================================================================
        // CHAMPS
        // ================================================================

        private readonly GeneralSettings _settings;

        // Files thread-safe (UI → speech thread)
        private readonly ConcurrentQueue<SpeechItem> _urgentQueue = new();
        private readonly ConcurrentQueue<SpeechItem> _normalQueue = new();
        private readonly SemaphoreSlim _signal = new(0, int.MaxValue);

        // Cooldowns — accédés uniquement sur le speech thread
        private readonly Dictionary<string, DateTime> _lastSpoken = new();

        // Pack WAV — chemin vers le dossier du pack sélectionné (null = TTS seul)
        private volatile string? _wavPackDir;

        // Thread dédié
        private readonly Thread _speechThread;
        private readonly CancellationTokenSource _cts = new();
        private SpeechSynthesizer? _synth;     // créé en lazy sur le speech thread
        private bool _synthReady;              // true une fois le synth initialisé

        // Synchronisation SpeakSync
        private readonly ManualResetEventSlim _speakDone = new(true);

        // ── State tracking (volatile — accédés depuis l'UI thread) ──────
        private volatile byte  _prevFlag;
        private volatile sbyte _prevYellowState;
        private volatile int   _prevPosition;
        private volatile int   _prevClassPosition;
        private volatile bool  _fuelWindowAnnounced;
        private volatile bool  _checkeredAnnounced;
        private volatile bool  _fuelLowAnnounced;
        private float          _prevGapAhead;    // float suffit, accédé depuis le timer thread uniquement

        // Spotter — accédés uniquement depuis le timer thread
        private byte _spotterState;    // 0=dégagé, 1=gauche, 2=droite, 3=les deux
        private int  _spotterClearCount; // nombre de lectures consécutives à 0 avant d'annoncer "dégagé"

        // Meilleurs temps en mémoire (mis à jour sur LapCompleted)
        private double _bestLap = double.MaxValue;
        private double _bestS1  = double.MaxValue;
        private double _bestS2  = double.MaxValue;
        private double _bestS3  = double.MaxValue;

        // ================================================================
        // CONSTRUCTEUR
        // ================================================================

        // Chemin racine des packs vocaux.
        // Priorité : dossier voice\ à côté de l'exe (packs livrés avec l'app)
        // Fallback  : %APPDATA%\DouzeAssistance\voice\ (packs installés par l'utilisateur)
        public static string VoiceRootDir
        {
            get
            {
                var exeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice");
                return Directory.Exists(exeDir) ? exeDir
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                   "DouzeAssistance", "voice");
            }
        }

        public VoiceService(GeneralSettings settings)
        {
            _settings = settings;
            LoadWavPack(settings.VoicePackName);

            _speechThread = new Thread(SpeechLoop)
            {
                Name         = "VoiceService-Speech",
                IsBackground = true,
                Priority     = ThreadPriority.BelowNormal
            };
            _speechThread.Start();
        }

        // ================================================================
        // API PUBLIQUE
        // ================================================================

        /// <summary>Vérifie les conditions d'alerte. Appelé ~3Hz depuis OverlayManager.</summary>
        public void CheckAlerts(DataService ds)
        {
            if (!_settings.VoiceEnabled) return;

            CheckFlags(ds);
            CheckFuel(ds);
            CheckGap(ds);
            CheckPosition(ds);
            CheckSpotter(ds);
        }

        /// <summary>Appelé sur LapCompleted — vérifie PB et secteurs.</summary>
        public void SpeakLap(DataService.LapCompletedArgs args)
        {
            if (!_settings.VoiceEnabled || !_settings.AlertLap) return;
            if (args.LapTime <= 0) return;

            if (args.LapTime < _bestLap)
            {
                _bestLap = args.LapTime;
                // Mettre à jour les secteurs aussi si c'est un PB global
                if (args.Sector1 > 0) _bestS1 = args.Sector1;
                if (args.Sector2 > 0) _bestS2 = args.Sector2;
                if (args.Sector3 > 0) _bestS3 = args.Sector3;
                Enqueue(_normalQueue, new SpeechItem("NewPersonalBest", "Nouveau meilleur tour, bravo", TimeSpan.Zero));
            }
            else
            {
                // Secteurs individuels
                if (args.Sector1 > 0 && args.Sector1 < _bestS1)
                {
                    _bestS1 = args.Sector1;
                    Enqueue(_normalQueue, new SpeechItem("SectorBeat_S1", "Secteur un, meilleur temps", TimeSpan.Zero));
                }
                if (args.Sector2 > 0 && args.Sector2 < _bestS2)
                {
                    _bestS2 = args.Sector2;
                    Enqueue(_normalQueue, new SpeechItem("SectorBeat_S2", "Secteur deux, meilleur temps", TimeSpan.Zero));
                }
                if (args.Sector3 > 0 && args.Sector3 < _bestS3)
                {
                    _bestS3 = args.Sector3;
                    Enqueue(_normalQueue, new SpeechItem("SectorBeat_S3", "Secteur trois, meilleur temps", TimeSpan.Zero));
                }
            }
        }

        /// <summary>Bouton TEST du VoicePanel.</summary>
        public void SpeakTest()
        {
            Enqueue(_urgentQueue, new SpeechItem("__test__", "Douze Assistance, système vocal actif", TimeSpan.Zero));
        }

        /// <summary>Charge un pack WAV. Appelé depuis le thread UI — volatile pour visibilité.</summary>
        public void SetWavPack(string packName)
        {
            _settings.VoicePackName = packName;
            LoadWavPack(packName);
        }

        private void LoadWavPack(string packName)
        {
            if (string.IsNullOrWhiteSpace(packName))
            {
                _wavPackDir = null;
                return;
            }
            var dir = Path.Combine(VoiceRootDir, packName);
            _wavPackDir = Directory.Exists(dir) ? dir : null;
        }

        /// <summary>Applique les nouveaux réglages (voix, volume, vitesse) au synthétiseur.</summary>
        public void ApplySettings()
        {
            // Envoie un sentinel spécial qui sera intercepté par le speech thread
            Enqueue(_urgentQueue, new SpeechItem("__settings__", "", TimeSpan.Zero));
        }

        /// <summary>Réinitialise l'état de session (à appeler lors d'une connexion).</summary>
        public void ResetSession()
        {
            _prevFlag            = 0;
            _prevYellowState     = 0;
            _prevPosition        = 0;
            _prevClassPosition   = 0;
            _prevGapAhead        = 0;
            _fuelWindowAnnounced = false;
            _checkeredAnnounced  = false;
            _fuelLowAnnounced    = false;
            _spotterState        = 0;
            _spotterClearCount   = 0;
            _bestLap = _bestS1 = _bestS2 = _bestS3 = double.MaxValue;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _signal.Release();        // débloque le Wait si le thread est en attente
            _speechThread.Join(2000);
            _synth?.Dispose();
            _cts.Dispose();
            _signal.Dispose();
            _speakDone.Dispose();
        }

        // ================================================================
        // LOGIQUE D'ALERTES
        // ================================================================

        private void CheckFlags(DataService ds)
        {
            if (!_settings.AlertFlags) return;

            byte  flag = ds.GetCurrentFlag();
            sbyte ys   = ds.GetYellowFlagState();

            // Drapeau bleu
            if (flag == 6 && _prevFlag != 6)
                Enqueue(_urgentQueue, new SpeechItem("BlueFlagWarning", "Drapeau bleu, laisse passer", TimeSpan.FromSeconds(_settings.CooldownBlueFlagSeconds)));

            // Drapeau rouge
            if (flag == 3 && _prevFlag != 3)
                Enqueue(_urgentQueue, new SpeechItem("RedFlag", "Drapeau rouge, arrêt immédiat", TimeSpan.Zero));

            // Drapeau vert
            if (flag == 1 && _prevFlag != 1)
                Enqueue(_normalQueue, new SpeechItem("GreenFlag", "Drapeau vert, go", TimeSpan.Zero));

            // Drapeau à damier
            if (flag == 8 && !_checkeredAnnounced)
            {
                _checkeredAnnounced = true;
                Enqueue(_normalQueue, new SpeechItem("CheckeredFlag", "Drapeau à damiers, belle course", TimeSpan.Zero));
            }

            // Drapeaux jaunes (sous-états)
            if ((flag == 2) && ys != _prevYellowState)
            {
                switch (ys)
                {
                    case 1:
                        Enqueue(_urgentQueue, new SpeechItem("YellowFlag", "Drapeau jaune, prudence", TimeSpan.FromSeconds(_settings.CooldownYellowFlagSeconds)));
                        break;
                    case 2:
                        Enqueue(_urgentQueue, new SpeechItem("YellowFlagPitClosed", "Jaune, pit fermé", TimeSpan.FromSeconds(_settings.CooldownYellowFlagSeconds)));
                        break;
                    case 4:
                        Enqueue(_normalQueue, new SpeechItem("YellowFlagResume", "Reprise, drapeau vert", TimeSpan.Zero));
                        break;
                }
            }

            _prevFlag        = flag;
            _prevYellowState = ys;
        }

        private void CheckFuel(DataService ds)
        {
            if (!_settings.AlertFuel) return;

            var fd = ds.GetFuelData();
            if (fd.RealAutonomy <= 0) return; // pas encore de données

            if (fd.WindowState == PitWindowState.WindowOpen && !_fuelWindowAnnounced)
            {
                _fuelWindowAnnounced = true;
                Enqueue(_normalQueue, new SpeechItem("FuelWindowOpen", "Fenêtre de pit ouverte", TimeSpan.Zero));
            }

            if (fd.RealAutonomy < 3.0 && !_fuelLowAnnounced)
            {
                _fuelLowAnnounced = true;
                Enqueue(_urgentQueue, new SpeechItem("FuelLowLaps", "Moins de trois tours de carburant", TimeSpan.Zero));
            }

            if (fd.WindowState == PitWindowState.Critical || fd.RealAutonomy < 1.5)
                Enqueue(_urgentQueue, new SpeechItem("FuelCritical", "Carburant critique", TimeSpan.FromSeconds(_settings.CooldownFuelCriticalSeconds)));
        }

        private void CheckGap(DataService ds)
        {
            if (!_settings.AlertGap) return;
            if (ds.GetPlayerInPits()) return;

            var (ahead, gapAhead, behind, gapBehind) = ds.GetGapData();
            double thresh = _settings.GapAlertThresholdSeconds;

            if (behind != null && gapBehind > 0 && gapBehind < thresh)
                Enqueue(_normalQueue, new SpeechItem("GapCloseBehind", "Attention derrière", TimeSpan.FromSeconds(_settings.CooldownGapSeconds)));

            if (ahead != null && _prevGapAhead > 0 && _prevGapAhead < thresh * 2 && gapAhead > thresh * 3)
                Enqueue(_normalQueue, new SpeechItem("GapLostAhead", "Tu décroches de la voiture devant", TimeSpan.FromSeconds(_settings.CooldownGapSeconds)));

            _prevGapAhead = (float)gapAhead;
        }

        private void CheckPosition(DataService ds)
        {
            if (!_settings.AlertPosition) return;

            var vehicles = ds.GetAllVehicles();
            var player = vehicles.FirstOrDefault(v => v.IsPlayer);
            if (player == null) return;

            // Position générale (toutes classes) — uniquement pour mise à jour interne
            _prevPosition = player.Position;

            // Position dans la classe du joueur uniquement :
            // - ignorer les dépassements d'autres classes
            // - ignorer les tours pris par une voiture plus rapide (qui sera forcément d'une
            //   autre classe ou aura plus de tours → ne change pas la position de classe)
            int classPos = vehicles.Count(v =>
                v.VehicleClass == player.VehicleClass &&
                v.Position < player.Position) + 1;

            if (_prevClassPosition > 0 && classPos != _prevClassPosition)
            {
                if (classPos < _prevClassPosition)
                    Enqueue(_normalQueue, new SpeechItem("PositionGained", "Position gagnée", TimeSpan.FromSeconds(_settings.CooldownPositionSeconds)));
                else
                    Enqueue(_normalQueue, new SpeechItem("PositionLost", "Position perdue", TimeSpan.FromSeconds(_settings.CooldownPositionSeconds)));
            }
            _prevClassPosition = classPos;
        }

        private void CheckSpotter(DataService ds)
        {
            if (!_settings.AlertSpotter) return;
            if (ds.GetPlayerInPits()) { _spotterState = 0; _spotterClearCount = 0; return; }

            var (left, right) = ds.GetBlindSpots();
            const double threshold = 0.1;

            bool hasLeft  = left  > threshold;
            bool hasRight = right > threshold;

            byte newState = (byte)((hasLeft ? 1 : 0) | (hasRight ? 2 : 0));

            if (newState != 0)
            {
                _spotterClearCount = 0;

                if (newState != _spotterState)
                {
                    _spotterState = newState;
                    string text = newState switch
                    {
                        1 => "Voiture à gauche",
                        2 => "Voiture à droite",
                        3 => "Voitures des deux côtés",
                        _ => ""
                    };
                    // Priorité urgente — interrompt les normaux, cooldown court anti-oscillation
                    Enqueue(_urgentQueue, new SpeechItem(
                        $"Spotter_{newState}", text,
                        TimeSpan.FromSeconds(_settings.CooldownSpotterSeconds)));
                }
            }
            else
            {
                // Hysteresis : attendre N lectures consécutives "vides" avant d'annoncer dégagé
                if (_spotterState != 0)
                {
                    _spotterClearCount++;
                    if (_spotterClearCount >= 4) // ~1.3 s à 3 Hz
                    {
                        _spotterState      = 0;
                        _spotterClearCount = 0;
                        Enqueue(_normalQueue, new SpeechItem("SpotterClear", "Dégagé",
                            TimeSpan.FromSeconds(_settings.CooldownSpotterSeconds)));
                    }
                }
            }
        }

        // ================================================================
        // FILE D'ATTENTE
        // ================================================================

        private void Enqueue(ConcurrentQueue<SpeechItem> queue, SpeechItem item)
        {
            queue.Enqueue(item);
            _signal.Release();
        }

        // ================================================================
        // THREAD SPEECH
        // ================================================================

        private void SpeechLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    _signal.Wait(_cts.Token);

                    if (_cts.IsCancellationRequested) break;

                    // Initialisation lazy du synth TTS (seulement si voix activée et pas de pack WAV)
                    if (!_synthReady && _settings.VoiceEnabled && _wavPackDir == null)
                        InitSynth();

                    // Items urgents d'abord (interrompent la parole en cours)
                    if (_urgentQueue.TryDequeue(out var urgent))
                    {
                        if (urgent.Key == "__settings__") { ApplySynthSettings(); continue; }
                        if (urgent.Key == "__test__") { SpeakSync("__test__", urgent.Text); continue; }

                        if (CheckCooldown(urgent.Key, urgent.Cooldown))
                        {
                            _synth?.SpeakAsyncCancelAll();
                            _speakDone.Wait();   // attendre la fin du cancel
                            _speakDone.Reset();
                            SpeakSync(urgent.Key, urgent.Text);
                            MarkSpoken(urgent.Key);
                        }
                        continue;
                    }

                    // Items normaux
                    if (_normalQueue.TryDequeue(out var normal))
                    {
                        if (CheckCooldown(normal.Key, normal.Cooldown))
                        {
                            SpeakSync(normal.Key, normal.Text);
                            MarkSpoken(normal.Key);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* arrêt propre */ }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceService] Erreur thread speech : {ex.Message}");
            }
        }

        private void InitSynth()
        {
            try
            {
                _synth = new SpeechSynthesizer();
                _synth.SetOutputToDefaultAudioDevice();
                _synth.SpeakCompleted += (_, _) => _speakDone.Set();
                ApplySynthSettings();
                _synthReady = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceService] Impossible d'initialiser SAPI : {ex.Message}");
            }
        }

        private void ApplySynthSettings()
        {
            if (_synth == null) return;
            _synth.Volume = _settings.VoiceVolume;
            _synth.Rate   = _settings.VoiceRate;

            if (!string.IsNullOrWhiteSpace(_settings.VoiceName))
            {
                try { _synth.SelectVoice(_settings.VoiceName); }
                catch { /* voix inconnue — garder la voix actuelle */ }
            }
            else
            {
                // Tenter une voix française par défaut
                try
                {
                    _synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0,
                        new CultureInfo("fr-FR"));
                }
                catch { /* pas de voix fr-FR — garder la voix système */ }
            }
        }

        private void SpeakSync(string key, string fallbackText)
        {
            // Essayer le fichier WAV du pack vocal d'abord
            var wavDir = _wavPackDir;
            if (wavDir != null)
            {
                var wavPath = Path.Combine(wavDir, $"{key}.wav");
                if (File.Exists(wavPath))
                {
                    try
                    {
                        using var player = new SoundPlayer(wavPath);
                        player.PlaySync();
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VoiceService] WAV '{wavPath}' : {ex.Message}");
                    }
                }
            }

            // Fallback TTS
            if (!_synthReady || _synth == null || string.IsNullOrWhiteSpace(fallbackText)) return;
            _speakDone.Reset();
            try
            {
                _synth.SpeakAsync(fallbackText);
                _speakDone.Wait(_cts.Token);
            }
            catch (OperationCanceledException) { }
        }

        private bool CheckCooldown(string key, TimeSpan cooldown)
        {
            if (cooldown == TimeSpan.Zero) return true;
            if (!_lastSpoken.TryGetValue(key, out var last)) return true;
            return DateTime.UtcNow - last >= cooldown;
        }

        private void MarkSpoken(string key)
            => _lastSpoken[key] = DateTime.UtcNow;
    }
}
