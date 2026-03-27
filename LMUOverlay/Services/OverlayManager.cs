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
        private bool _initialized;

        // VR — backend abstrait (SteamVR ou OpenXR selon le runtime actif)
        private IVRService? _vrService;

        public bool IsConnected => _reader.IsConnected;
        public DataService DataService => _dataService;
        public IVRService? VRService => _vrService;
        public bool IsVRActive => _vrService?.IsVRActive ?? false;

        public event EventHandler<bool>? ConnectionChanged;
        public event EventHandler<bool>? VRStatusChanged;

        public OverlayManager(AppConfig config)
        {
            _config = config;
            _reader = new SharedMemoryReader();
            _dataService = new DataService(_reader);

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

            RegisterOverlay("ProximityRadar", new ProximityRadarOverlay(_dataService, _config.ProximityRadar));
            RegisterOverlay("StandingsOverall", new StandingsOverlay(_dataService, _config.StandingsOverall, _config.StandingsColumns));
            RegisterOverlay("StandingsRelative", new RelativeOverlay(_dataService, _config.StandingsRelative, _config.RelativeConfig));
            RegisterOverlay("TrackMap", new TrackMapOverlay(_dataService, _config.TrackMap));
            RegisterOverlay("InputGraph", new InputGraphOverlay(_dataService, _config.InputGraph, _config.InputConfig));
            RegisterOverlay("GapTimer", new GapOverlay(_dataService, _config.GapTimer));
            RegisterOverlay("Weather", new WeatherOverlay(_dataService, _config.Weather));
            RegisterOverlay("Flags", new FlagOverlay(_dataService, _config.Flags));
            RegisterOverlay("TireInfo", new TireInfoOverlay(_dataService, _config.TireInfo));
            RegisterOverlay("FuelStrategy", new FuelStrategyOverlay(_dataService, _config.FuelInfo));
            RegisterOverlay("DeltaTime", new DeltaOverlay(_dataService, _config.DeltaTime));
            RegisterOverlay("Damage", new DamageOverlay(_dataService, _config.Damage));
            RegisterOverlay("LapHistory", new LapHistoryOverlay(_dataService, _config.LapHistory));
            RegisterOverlay("LapGraph",   new LapGraphOverlay(_dataService,   _config.LapGraph));
            RegisterOverlay("GForce", new GForceOverlay(_dataService, _config.GForce));
            RegisterOverlay("Dashboard", new DashboardOverlay(_dataService, _config.Dashboard, _config.DashboardConfig));
            RegisterOverlay("RelativeAheadBehind", new RelativeAheadBehindOverlay(_dataService, _config.RelativeAheadBehind));
            RegisterOverlay("TrackLimits", new TrackLimitsOverlay(_dataService, _config.TrackLimits));
            RegisterOverlay("BlindSpot", new BlindSpotOverlay(_dataService, _config.BlindSpot));
            RegisterOverlay("Rejoin", new RejoinOverlay(_dataService, _config.Rejoin));

            _initialized = true;

            if (_config.General.AutoConnect)
                _connectionTimer.Start();
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
                _connectionTimer.Stop();
                _updateTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _config.General.UpdateRateHz);
                _updateTimer.Start();
                ConnectionChanged?.Invoke(this, true);
                RefreshOverlayVisibility();
            }
        }

        public void Disconnect()
        {
            _updateTimer.Stop();
            HideAll();
            _reader.Disconnect();
            _connectionTimer.Start();
        }

        private void OnConnectionTick(object? sender, EventArgs e) => Connect();

        // ================================================================
        // UPDATE LOOP
        // ================================================================

        private void OnUpdateTick(object? sender, EventArgs e)
        {
            if (!_reader.IsConnected) return;

            try { _reader.Update(); }
            catch { Disconnect(); return; }

            _dataService.UpdateTelemetryTrace();

            // Hide overlays when in game menus / garage
            if (_config.General.HideInMenus)
            {
                byte gp = _reader.ScoringInfo.mGamePhase;
                int numVehicles = _reader.ScoringInfo.mNumVehicles;
                double sessionET = _reader.ScoringInfo.mCurrentET;

                // On track = gamePhase >= 3 AND vehicles present AND session running
                // In garage/menus: numVehicles = 0, or gamePhase < 3, or mCurrentET = 0
                bool onTrack = gp >= 3 && numVehicles > 0 && sessionET > 0;

                foreach (var overlay in _overlays.Values)
                {
                    if (overlay.Settings.IsEnabled)
                    {
                        if (onTrack)
                        {
                            if (!overlay.IsVisible) overlay.Show();
                            try { overlay.UpdateData(); }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"Overlay error ({overlay.Settings.Name}): {ex.Message}");
                            }
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
                // Normal: update all visible overlays
                foreach (var overlay in _overlays.Values)
                {
                    if (overlay.IsVisible)
                    {
                        try { overlay.UpdateData(); }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"Overlay error ({overlay.Settings.Name}): {ex.Message}");
                        }
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

        public void RefreshOverlayVisibility()
        {
            if (!_reader.IsConnected)
            {
                foreach (var o in _overlays.Values) o.Hide();
                return;
            }
            foreach (var o in _overlays.Values)
            {
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
                if (!_reader.IsConnected) return;
                if (o.Settings.IsEnabled) o.Show(); else o.Hide();
            }
        }

        public void ShowAll()
        {
            foreach (var o in _overlays.Values)
            {
                o.Settings.IsEnabled = true;
                if (_reader.IsConnected) o.Show();
            }
        }

        /// <summary>Returns true if the player is currently on track (not in menus/garage).</summary>
        public bool IsOnTrack()
        {
            if (!_reader.IsConnected) return false;
            byte gp = _reader.ScoringInfo.mGamePhase;
            int numV = _reader.ScoringInfo.mNumVehicles;
            double et = _reader.ScoringInfo.mCurrentET;
            return gp >= 3 && numV > 0 && et > 0;
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
        // DISPOSE
        // ================================================================

        public void Dispose()
        {
            _updateTimer.Stop();
            _connectionTimer.Stop();

            _vrService?.Dispose();

            foreach (var o in _overlays.Values)
            {
                try { o.Close(); } catch { }
            }

            _reader.Dispose();
        }
    }
}
