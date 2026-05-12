using System.Windows;
using System.Windows.Threading;
using LMUOverlay.Models;
using LMUOverlay.Views.Overlays;
using LMUOverlay.VR;
using rF2SharedMemory;

namespace LMUOverlay.Services
{
    public class OverlayManager : IDisposable
    {
        private readonly SharedMemoryReader _reader;
        private readonly DataService _dataService;
        private readonly AppConfig _config;
        private readonly DispatcherTimer _updateTimer;
        private readonly DispatcherTimer _connectionTimer;
        private readonly Dictionary<string, BaseOverlayWindow> _overlays = new();
        private readonly LeaderboardService _leaderboard;
        private readonly VoiceService _voice;
        private int _voiceTickCount;
        private bool _initialized;

        // VR — backend abstrait (SteamVR ou OpenXR selon le runtime actif)
        private IVRService? _vrService;

        // Twitch chat
        private readonly TwitchChatService _twitchChatService = new();

        public bool IsConnected => _reader.IsConnected;
        public DataService DataService => _dataService;
        public VoiceService VoiceService => _voice;
        public TwitchChatService TwitchChatService => _twitchChatService;
        public IVRService? VRService => _vrService;
        public bool IsVRActive => _vrService?.IsVRActive ?? false;

        /// <summary>
        /// Quand true, affiche tous les overlays actifs indépendamment de HideInMenus.
        /// Se désactive en appelant SetForceDisplay(false), qui masque tous les overlays.
        /// </summary>
        public bool ForceDisplay { get; private set; }

        /// <summary>
        /// Compteur de ticks pour le throttling des overlays lents.
        /// </summary>
        private int _tickCount;

        /// <summary>
        /// Détection de pause via gel de mCurrentET.
        /// LMU ne change ni mGamePhase ni mInRealtime lors du menu ESC —
        /// seul mCurrentET se fige. Après 10 ticks sans changement (~330ms) → pause détectée.
        /// </summary>
        private double _lastET = -1;
        private int _frozenETTicks;
        private const int FrozenETThreshold = 45; // ~1.5s à 30Hz — réduit les faux positifs en conduite

        /// <summary>
        /// Overlays qui n'ont pas besoin d'être rafraîchis à 30Hz.
        /// Ils se mettent à jour 1 tick sur 3 (≈10Hz).
        /// </summary>
        private static readonly HashSet<string> _slowOverlays = new()
        {
            "StandingsOverall", "StandingsRelative", "TrackMap", "LapHistory", "LapGraph"
        };

        /// <summary>
        /// Overlays qui peuvent s'afficher sans connexion LMU (indépendants du jeu).
        /// </summary>
        private static readonly HashSet<string> _persistentOverlays = new() { "Clock", "TwitchChat" };

        /// <summary>
        /// Sous-ensemble de _persistentOverlays : ces overlays ignorent aussi HideInMenus.
        /// Clock = toujours visible. TwitchChat = visible sans LMU mais se cache dans les menus.
        /// </summary>
        private static readonly HashSet<string> _alwaysVisible = new() { "Clock" };

        public event EventHandler<bool>? ConnectionChanged;
        public event EventHandler<bool>? VRStatusChanged;

        public OverlayManager(AppConfig config)
        {
            _config = config;
            _reader = new SharedMemoryReader();
            _dataService = new DataService(_reader);
            _leaderboard = new LeaderboardService(config.General);
            _voice       = new VoiceService(config.General);

            Helpers.ThemeManager.ThemeChanged += OnThemeChangedRestart;

            _dataService.LapCompleted += OnLapCompleted;
            _dataService.LapCompleted += args => _voice.SpeakLap(args);

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / config.General.UpdateRateHz)
            };
            _updateTimer.Tick += OnUpdateTick;

            _connectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _connectionTimer.Tick += OnConnectionTick;

            _reader.Disconnected += (s, e) =>
            {
                _updateTimer.Stop();
                _connectionTimer.Start();
                ConnectionChanged?.Invoke(this, false);
            };
        }

        // ================================================================
        // INITIALIZE
        // ================================================================

        public void Initialize()
        {
            if (_initialized) return;

            void Reg(string key, Func<BaseOverlayWindow> factory)
            {
                App.WriteCrashLog($"[OVERLAY] creating {key}\n");
                RegisterOverlay(key, factory());
                App.WriteCrashLog($"[OVERLAY] ok {key}\n");
            }

            Reg("ProximityRadar",       () => new ProximityRadarOverlay(_dataService, _config.ProximityRadar));
            Reg("StandingsOverall",     () => new StandingsOverlay(_dataService, _config.StandingsOverall, _config.StandingsColumns));
            Reg("StandingsRelative",    () => new RelativeOverlay(_dataService, _config.StandingsRelative, _config.RelativeConfig));
            Reg("TrackMap",             () => new TrackMapOverlay(_dataService, _config.TrackMap));
            Reg("InputGraph",           () => new InputGraphOverlay(_dataService, _config.InputGraph, _config.InputConfig));
            Reg("GapTimer",             () => new GapOverlay(_dataService, _config.GapTimer));
            Reg("Weather",              () => new WeatherOverlay(_dataService, _config.Weather));
            Reg("Flags",                () => new FlagOverlay(_dataService, _config.Flags));
            Reg("TireInfo",             () => new TireInfoOverlay(_dataService, _config.TireInfo));
            Reg("FuelStrategy",         () => new FuelStrategyOverlay(_dataService, _config.FuelInfo));
            Reg("DeltaTime",            () => new DeltaOverlay(_dataService, _config.DeltaTime));
            Reg("Damage",               () => new DamageOverlay(_dataService, _config.Damage));
            Reg("LapHistory",           () => new LapHistoryOverlay(_dataService, _config.LapHistory));
            Reg("LapGraph",             () => new LapGraphOverlay(_dataService, _config.LapGraph));
            Reg("GForce",               () => new GForceOverlay(_dataService, _config.GForce));
            Reg("Dashboard",            () => new DashboardOverlay(_dataService, _config.Dashboard, _config.DashboardConfig));
            Reg("RelativeAheadBehind",  () => new RelativeAheadBehindOverlay(_dataService, _config.RelativeAheadBehind));
            Reg("BlindSpot",            () => new BlindSpotOverlay(_dataService, _config.BlindSpot));
            Reg("Rejoin",               () => new RejoinOverlay(_dataService, _config.Rejoin));
            Reg("Note",                 () => new NoteOverlay(_dataService, _config.Note));
            Reg("Compteur",             () => new CompteurOverlay(_dataService, _config.Compteur));
            Reg("Clock",                () => new ClockOverlay(_dataService, _config.Clock));
            Reg("TwitchChat",           () => new TwitchChatOverlay(_dataService, _config.TwitchChat, _twitchChatService, _config));
            Reg("PitDistance",          () => new PitDistanceOverlay(_dataService, _config.PitDistance, _config.PitDistanceConfig));

            _initialized = true;
            App.WriteCrashLog("[OVERLAY] all overlays created\n");

            // Démarrer le chat Twitch si un canal est configuré
            if (!string.IsNullOrWhiteSpace(_config.Twitch.Channel))
            {
                App.WriteCrashLog($"[OVERLAY] connecting Twitch: {_config.Twitch.Channel}\n");
                _twitchChatService.Connect(_config.Twitch.Channel);
                App.WriteCrashLog("[OVERLAY] Twitch connect called\n");
            }

            // Persistent overlays are shown immediately regardless of connection
            App.WriteCrashLog("[OVERLAY] showing persistent overlays\n");
            foreach (var key in _persistentOverlays)
            {
                if (_overlays.TryGetValue(key, out var o) && o.Settings.IsEnabled)
                {
                    App.WriteCrashLog($"[OVERLAY] showing {key}\n");
                    o.Show();
                    App.WriteCrashLog($"[OVERLAY] shown {key}\n");
                }
            }

            App.WriteCrashLog("[OVERLAY] starting connection timer\n");
            if (_config.General.AutoConnect)
                _connectionTimer.Start();

            App.WriteCrashLog("[OVERLAY] Initialize complete\n");
        }

        private void RegisterOverlay(string key, BaseOverlayWindow overlay)
        {
            _overlays[key] = overlay;
        }

        // ================================================================
        // VR
        // ================================================================

        /// <summary>
        /// Start VR mode: initialize SteamVR and register all overlays.
        /// </summary>
        public bool StartVR(out string errorMessage)
        {
            errorMessage = "";

            // ── Sélection automatique du backend VR ───────────────────────────
            // Priorité : OpenXR natif si disponible ET si XR_EXTX_overlay est
            // supporté → sinon fallback sur SteamVR (IVROverlay).
            IVRService candidate;

            if (OpenXRService.IsRuntimeAvailable())
            {
                var oxr = new OpenXRService();
                if (oxr.Initialize())
                {
                    candidate = oxr;
                    goto BackendReady;
                }
                // OpenXR disponible mais init échouée (ex. runtime sans XR_EXTX_overlay)
                // On note l'erreur et on tente SteamVR
                System.Diagnostics.Debug.WriteLine(
                    $"[VR] OpenXR non utilisable ({oxr.LastError}), tentative SteamVR…");
                oxr.Dispose();
            }

            {
                var svr = new VROverlayService();
                if (!svr.Initialize())
                {
                    errorMessage = svr.LastError;
                    svr.Dispose();
                    return false;
                }
                candidate = svr;
            }

            BackendReady:
            _vrService = candidate;

            foreach (var (key, window) in _overlays)
                _vrService.RegisterOverlay(key, window, window.Settings);

            _vrService.VRStatusChanged += (s, active) => VRStatusChanged?.Invoke(this, active);
            VRStatusChanged?.Invoke(this, true);
            return true;
        }

        /// <summary>
        /// Stop VR mode.
        /// </summary>
        public void StopVR()
        {
            _vrService?.Shutdown();
            _vrService = null;
            VRStatusChanged?.Invoke(this, false);
        }

        // ================================================================
        // CONNECTION
        // ================================================================

        public void Connect()
        {
            if (_reader.Connect())
            {
                // If ForceDisplay was ON (preview while disconnected), reset it
                if (ForceDisplay)
                {
                    ForceDisplay = false;
                    foreach (var (key, o) in _overlays)
                    {
                        if (_persistentOverlays.Contains(key)) continue;
                        if (o.IsVisible) o.Hide();
                    }
                }

                _connectionTimer.Stop();
                _updateTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _config.General.UpdateRateHz);
                _updateTimer.Start();
                _voice.ResetSession();
                ConnectionChanged?.Invoke(this, true);
                RefreshOverlayVisibility();
            }
        }

        public void Disconnect()
        {
            _updateTimer.Stop();
            ForceDisplay = false;
            // Hide race overlays without disabling them (IsEnabled stays intact)
            foreach (var (key, o) in _overlays)
            {
                if (_persistentOverlays.Contains(key)) continue;
                if (o.IsVisible) o.Hide();
            }
            _reader.Disconnect();
            _connectionTimer.Start();
        }

        private void OnConnectionTick(object? sender, EventArgs e) => Connect();

        // ================================================================
        // UPDATE LOOP
        // ================================================================

        private void OnUpdateTick(object? sender, EventArgs e)
        {
            // _alwaysVisible (Clock) : s'affiche toujours, indépendamment de LMU et HideInMenus.
            // _persistentOverlays \ _alwaysVisible (TwitchChat) : s'affiche sans LMU,
            //   mais respecte HideInMenus quand LMU est connecté.
            foreach (var (key, overlay) in _overlays)
            {
                if (!_persistentOverlays.Contains(key)) continue;
                if (!overlay.Settings.IsEnabled) continue;
                // Clock → toujours affiché ; TwitchChat → affiché seulement si pas connecté à LMU
                if (_alwaysVisible.Contains(key) || !_reader.IsConnected)
                    if (!overlay.IsVisible) overlay.Show();
            }

            if (!_reader.IsConnected) return;

            try { _reader.Update(); }
            catch { Disconnect(); return; }

            _dataService.UpdateTelemetryTrace();
            _dataService.UpdateOpponentTraces();

            // Alertes vocales throttlées à ~3Hz (toutes les 10 ticks à 30Hz)
            if (++_voiceTickCount % 10 == 0)
                _voice.CheckAlerts(_dataService);

            // ── Throttling : les overlays lents ne se mettent à jour qu'1 tick sur 3 ──
            bool isSlowTick = _tickCount++ % 3 == 0;

            // ── Visibilité des overlays selon HideInMenus ──────────────────
            // ForceDisplay est réservé au mode déconnecté (géré dans SetForceDisplay).
            // Quand connecté : HideInMenus ON = piste uniquement, OFF = toujours.
            if (_config.General.HideInMenus)
            {
                byte gp = _reader.ScoringInfo.mGamePhase;
                int numVehicles = _reader.ScoringInfo.mNumVehicles;
                double sessionET = _reader.ScoringInfo.mCurrentET;

                // Détection pause ESC : LMU ne change ni gp ni inRealtime lors du menu ESC.
                // Seul mCurrentET se fige. Après FrozenETThreshold ticks sans avancement → pause.
                if (sessionET != _lastET) { _lastET = sessionET; _frozenETTicks = 0; }
                else { _frozenETTicks++; }
                bool etRunning = _frozenETTicks < FrozenETThreshold;

                // mInRealtime=0 dans le lobby pré-session (joueur pas encore dans la voiture).
                // LMU ne le passe pas à 0 pour le menu ESC (d'où la détection par gel d'ET ci-dessus),
                // mais il est bien 0 dans l'écran de sélection/attente avant de commencer à piloter.
                bool inRealtime = _reader.ScoringInfo.mInRealtime != 0;

                bool onTrack = gp >= 3 && gp <= 6 && numVehicles > 0 && sessionET > 0 && etRunning && inRealtime;

                foreach (var (key, overlay) in _overlays)
                {
                    if (_alwaysVisible.Contains(key)) continue; // Clock géré dans le bloc persistent
                    if (overlay.Settings.IsEnabled)
                    {
                        if (onTrack)
                        {
                            if (!overlay.IsVisible) overlay.Show();
                            if (_slowOverlays.Contains(key) && !isSlowTick) continue;
                            try { overlay.UpdateData(); }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Overlay error ({overlay.Settings.Name}): {ex.Message}"); }
                        }
                        else
                        {
                            if (overlay.IsVisible) overlay.Hide();
                        }
                    }
                }
            }
            else
            {
                // HideInMenus OFF : show all enabled overlays all the time
                foreach (var (key, overlay) in _overlays)
                {
                    if (_alwaysVisible.Contains(key)) continue; // Clock géré dans le bloc persistent
                    if (overlay.Settings.IsEnabled)
                    {
                        if (!overlay.IsVisible) overlay.Show();
                        if (_slowOverlays.Contains(key) && !isSlowTick) continue;
                        try { overlay.UpdateData(); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Overlay error ({overlay.Settings.Name}): {ex.Message}"); }
                    }
                }
            }

            // Update VR overlays (SteamVR ou OpenXR selon le backend actif)
            if (_vrService is OpenXRService oxrSvc)
            {
                // OpenXR : s'assurer que les swapchains sont créés (nécessite le layout WPF)
                foreach (var (key, window) in _overlays)
                    oxrSvc.EnsureSwapchainForOverlay(key, window);
            }
            try { _vrService?.UpdateAll(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VR update error: {ex.Message}");
            }
        }

        // ================================================================
        // VISIBILITY / LOCK
        // ================================================================

        /// <summary>
        /// Active ou désactive le mode "forcer l'affichage".
        /// Uniquement disponible quand DÉCONNECTÉ (préview / positionnement).
        /// ON  → affiche immédiatement tous les overlays activés.
        /// OFF → cache tous les overlays.
        /// </summary>
        public void SetForceDisplay(bool force)
        {
            ForceDisplay = force;
            if (force)
            {
                foreach (var o in _overlays.Values)
                    if (o.Settings.IsEnabled) o.Show();
            }
            else
            {
                foreach (var (key, o) in _overlays)
                {
                    if (_persistentOverlays.Contains(key)) continue;
                    if (o.IsVisible) o.Hide();
                }
            }
        }

        public void RefreshOverlayVisibility()
        {
            // Quand déconnecté : ForceDisplay gère la visibilité (bouton activé côté UI)
            if (!_reader.IsConnected) return;

            // HideInMenus ON : cacher si pas en piste
            if (_config.General.HideInMenus && !IsOnTrack())
            {
                foreach (var (key, o) in _overlays)
                {
                    if (_alwaysVisible.Contains(key)) continue; // Clock reste visible
                    o.Hide(); // TwitchChat se cache aussi dans les menus
                }
                return;
            }

            // HideInMenus OFF ou en piste : afficher tous les activés
            foreach (var (key, o) in _overlays)
            {
                if (_alwaysVisible.Contains(key)) continue;
                if (o.Settings.IsEnabled) o.Show(); else o.Hide();
            }
        }

        public T? GetOverlay<T>(string key) where T : BaseOverlayWindow =>
            _overlays.TryGetValue(key, out var o) ? o as T : null;

        public void ToggleOverlay(string key)
        {
            if (_overlays.TryGetValue(key, out var o))
            {
                o.Settings.IsEnabled = !o.Settings.IsEnabled;
                // Overlays persistants : toggle actif même sans connexion LMU
                if (!_reader.IsConnected && !_persistentOverlays.Contains(key)) return;
                if (o.Settings.IsEnabled) o.Show(); else o.Hide();
            }
        }

        public void ShowAll()
        {
            // Coche uniquement toutes les cases — l'affichage réel est géré par
            // RefreshOverlayVisibility() qui respecte HideInMenus et l'état de connexion.
            foreach (var o in _overlays.Values)
                o.Settings.IsEnabled = true;
            RefreshOverlayVisibility();
        }

        /// <summary>Returns true if the player is currently on track (not in menus/garage).</summary>
        public bool IsOnTrack()
        {
            if (!_reader.IsConnected) return false;
            byte gp = _reader.ScoringInfo.mGamePhase;
            int numV = _reader.ScoringInfo.mNumVehicles;
            double et = _reader.ScoringInfo.mCurrentET;
            bool etRunning = _frozenETTicks < FrozenETThreshold;
            return gp >= 3 && gp <= 6 && numV > 0 && et > 0 && etRunning;
        }

        public void HideAll()
        {
            foreach (var o in _overlays.Values)
            {
                o.Settings.IsEnabled = false;
                o.Hide();
            }
        }

        public void SetAllLocked(bool locked)
        {
            foreach (var o in _overlays.Values)
            {
                o.Settings.IsLocked = locked;
                o.UpdateLockState();
            }
        }

        // ================================================================
        // LEADERBOARD
        // ================================================================

        private void OnUpdateOpponents() => _dataService.UpdateOpponentTraces();

        private void OnLapCompleted(DataService.LapCompletedArgs args)
        {
            string version = System.Reflection.Assembly
                .GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

            _leaderboard.SubmitLap(
                args.Circuit, args.CarClass, args.CarName,
                args.LapTime, args.Sector1, args.Sector2, args.Sector3,
                version);
        }

        // ================================================================
        // DISPOSE
        // ================================================================

        /// <summary>
        /// Ferme tous les overlays et les recrée pour appliquer le nouveau thème.
        /// </summary>
        public void ReinitializeOverlays()
        {
            foreach (var o in _overlays.Values)
                try { o.Close(); } catch { }
            _overlays.Clear();
            _initialized = false;

            Initialize();

            // Restore visibility of all overlays that were active
            if (_reader.IsConnected)
                RefreshOverlayVisibility();
        }

        private void OnThemeChangedRestart() => ReinitializeOverlays();

        public void Dispose()
        {
            Helpers.ThemeManager.ThemeChanged -= OnThemeChangedRestart;
            _updateTimer.Stop();
            _connectionTimer.Stop();

            _voice.Dispose();
            _vrService?.Dispose();
            _twitchChatService.Dispose();

            foreach (var o in _overlays.Values)
            {
                try { o.Close(); } catch { }
            }

            _reader.Dispose();
        }
    }
}
