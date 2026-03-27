using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LMUOverlay.Models
{
    /// <summary>
    /// Root configuration object, serialized to JSON for persistence.
    /// </summary>
    public class AppConfig : INotifyPropertyChanged
    {
        public OverlaySettings ProximityRadar { get; set; } = new("Radar de Proximité", true);
        public OverlaySettings StandingsOverall { get; set; } = new("Classement Général", true);
        public OverlaySettings StandingsRelative { get; set; } = new("Classement Relatif", true);
        public OverlaySettings TrackMap { get; set; } = new("Carte du Circuit", true);
        public OverlaySettings InputGraph { get; set; } = new("Graphique Inputs", true);
        public OverlaySettings GapTimer { get; set; } = new("Écarts Temps", true);
        public OverlaySettings Weather { get; set; } = new("Météo", true);
        public OverlaySettings Flags { get; set; } = new("Drapeaux", true);
        public OverlaySettings TireInfo { get; set; } = new("Pneus & Freins", true);
        public OverlaySettings FuelInfo { get; set; } = new("Essence", true);

        // NEW overlays
        public OverlaySettings DeltaTime { get; set; } = new("Delta Temps", false);
        public OverlaySettings PitStrategy { get; set; } = new("Stratégie Pit", false);
        public OverlaySettings Damage { get; set; } = new("Dommages", false);
        public OverlaySettings LapHistory { get; set; } = new("Historique Tours", false);
        public OverlaySettings GForce { get; set; } = new("Force G", false);
        public OverlaySettings Dashboard { get; set; } = new("Dashboard", false);
        public OverlaySettings RelativeAheadBehind { get; set; } = new("Devant / Derrière", false);
        public OverlaySettings TrackLimits { get; set; } = new("Track Limits", false);
        public OverlaySettings BlindSpot { get; set; } = new("Angles Morts", false);
        public OverlaySettings Rejoin { get; set; } = new("Retour en Piste", false);

        public GeneralSettings General { get; set; } = new();
        public StandingsColumnConfig StandingsColumns { get; set; } = new();
        public StandingsDisplayConfig StandingsDisplay { get; set; } = new();
        public DashboardDisplayConfig DashboardConfig { get; set; } = new();
        public InputDisplayConfig InputConfig { get; set; } = new();
        public RelativeConfig RelativeConfig { get; set; } = new();

        // Profile / streaming
        public string CurrentProfile { get; set; } = "Default";
        public bool ChromaKeyEnabled { get; set; }
        public string ChromaKeyColor { get; set; } = "#00FF00";
        public bool VREnabled { get; set; }
        public float VRGlobalScale { get; set; } = 1.0f;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Per-overlay settings: enabled, position, scale, opacity, and specific options.
    /// </summary>
    public class OverlaySettings : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private double _scale = 1.0;
        private double _opacity = 0.9;
        private double _posX = 100;
        private double _posY = 100;
        private bool _isLocked;
        private double _overlayWidth;   // 0 = auto (natural size)
        private double _overlayHeight;  // 0 = auto (natural size)

        public string Name { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public double Scale
        {
            get => _scale;
            set { _scale = Math.Clamp(value, 0.3, 3.0); OnPropertyChanged(); }
        }

        public double Opacity
        {
            get => _opacity;
            set { _opacity = Math.Clamp(value, 0.1, 1.0); OnPropertyChanged(); }
        }

        public double PosX
        {
            get => _posX;
            set { _posX = value; OnPropertyChanged(); }
        }

        public double PosY
        {
            get => _posY;
            set { _posY = value; OnPropertyChanged(); }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(); }
        }

        /// <summary>Custom width in pixels (0 = auto, use natural content size).</summary>
        public double OverlayWidth
        {
            get => _overlayWidth;
            set { _overlayWidth = Math.Max(0, value); OnPropertyChanged(); }
        }

        /// <summary>Custom height in pixels (0 = auto, use natural content size).</summary>
        public double OverlayHeight
        {
            get => _overlayHeight;
            set { _overlayHeight = Math.Max(0, value); OnPropertyChanged(); }
        }

        // Dictionary for overlay-specific custom settings
        public Dictionary<string, object> CustomOptions { get; set; } = new();

        public OverlaySettings() { Name = ""; }

        public OverlaySettings(string name, bool enabled)
        {
            Name = name;
            _isEnabled = enabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// General app settings.
    /// </summary>
    public class StandingsColumnConfig
    {
        public bool Position { get; set; } = true;
        public bool ClassBar { get; set; } = true;
        public bool Driver { get; set; } = true;
        public bool CarName { get; set; } = true;
        public bool TotalLaps { get; set; } = true;
        public bool LapProgress { get; set; } = true;
        public bool BestLap { get; set; } = true;
        public bool LastLap { get; set; } = true;
        public bool Delta { get; set; } = true;
        public bool GapToLeader { get; set; } = true;
        public bool GapToNext { get; set; } = true;
        public bool Sector1 { get; set; } = false;
        public bool Sector2 { get; set; } = false;
        public bool Sector3 { get; set; } = false;
        public bool TireCompound { get; set; } = false;
        public bool PitStops { get; set; } = true;
        public bool Indicator { get; set; } = true;
        public bool Speed { get; set; } = false;
        public bool Penalties { get; set; } = false;
        public bool SectorStatus { get; set; } = true;
        public int MaxEntriesPerClass { get; set; } = 10;
        public int OtherClassCount { get; set; } = 3;
        public bool ShowSessionInfo { get; set; } = true;

        public static readonly (string Key, string Label)[] AllColumns =
        {
            ("Position", "Position"),
            ("ClassBar", "Barre de classe"),
            ("Driver", "Pilote"),
            ("CarName", "Nom voiture"),
            ("TotalLaps", "Tours"),
            ("LapProgress", "Progression %"),
            ("BestLap", "Meilleur tour"),
            ("LastLap", "Dernier tour"),
            ("Delta", "Delta"),
            ("GapToLeader", "Écart leader"),
            ("GapToNext", "Écart devant"),
            ("Sector1", "Secteur 1"),
            ("Sector2", "Secteur 2"),
            ("Sector3", "Secteur 3"),
            ("SectorStatus", "Secteurs (indicateurs)"),
            ("TireCompound", "Pneus"),
            ("PitStops", "Arrêts"),
            ("Speed", "Vitesse"),
            ("Penalties", "Pénalités"),
            ("Indicator", "Indicateur pit/flag"),
        };

        public bool IsVisible(string key) => key switch
        {
            "Position" => Position, "ClassBar" => ClassBar, "Driver" => Driver,
            "CarName" => CarName, "TotalLaps" => TotalLaps, "LapProgress" => LapProgress,
            "BestLap" => BestLap, "LastLap" => LastLap, "Delta" => Delta,
            "GapToLeader" => GapToLeader, "GapToNext" => GapToNext,
            "Sector1" => Sector1, "Sector2" => Sector2, "Sector3" => Sector3,
            "SectorStatus" => SectorStatus,
            "TireCompound" => TireCompound, "PitStops" => PitStops,
            "Speed" => Speed, "Penalties" => Penalties, "Indicator" => Indicator,
            _ => true
        };

        public void SetVisible(string key, bool val)
        {
            var prop = GetType().GetProperty(key);
            if (prop != null && prop.PropertyType == typeof(bool))
                prop.SetValue(this, val);
        }
    }

    public class GeneralSettings : INotifyPropertyChanged
    {
        private int _updateRateHz = 30;
        private bool _autoConnect = true;
        private bool _startMinimized;
        private bool _alwaysOnTop = true;
        private string _theme = "Dark";
        private bool _hideInMenus = true;

        public int UpdateRateHz
        {
            get => _updateRateHz;
            set { _updateRateHz = Math.Clamp(value, 10, 60); OnPropertyChanged(); }
        }

        public bool AutoConnect
        {
            get => _autoConnect;
            set { _autoConnect = value; OnPropertyChanged(); }
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set { _startMinimized = value; OnPropertyChanged(); }
        }

        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set { _alwaysOnTop = value; OnPropertyChanged(); }
        }

        public string Theme
        {
            get => _theme;
            set { _theme = value; OnPropertyChanged(); }
        }

        /// <summary>Hide overlays when in game menus (gamePhase &lt; 3).</summary>
        public bool HideInMenus
        {
            get => _hideInMenus;
            set { _hideInMenus = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ========================================================================
    // Processed data models for overlays
    // ========================================================================

    public class VehicleData
    {
        public int Id { get; set; }
        public string DriverName { get; set; } = "";
        public string VehicleName { get; set; } = "";
        public string VehicleClass { get; set; } = "";
        public int Position { get; set; }
        public int TotalLaps { get; set; }
        public double LapDistance { get; set; }
        public double BestLapTime { get; set; }
        public double LastLapTime { get; set; }
        public double TimeBehindNext { get; set; }
        public double TimeBehindLeader { get; set; }
        public double Speed { get; set; }
        public bool IsPlayer { get; set; }
        public bool InPits { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        public double YawAngle { get; set; }
        public byte Flag { get; set; }

        // Extended standings fields
        public double LapProgress { get; set; }     // 0-100% around track
        public string CarNumber { get; set; } = "";
        public double LastLapDelta { get; set; }     // last lap - best lap
        public double Sector1 { get; set; }
        public double Sector2 { get; set; }
        public double CurSector1 { get; set; }
        public double CurSector2 { get; set; }
        public int NumPitstops { get; set; }
        public int NumPenalties { get; set; }
        public double EstimatedLapTime { get; set; }
        public double TimeIntoLap { get; set; }
        public double LapStartET { get; set; }
        public byte PitState { get; set; }
        public int LapsBehindLeader { get; set; }
        public string TireCompound { get; set; } = "";
        public double Fuel { get; set; }

        // Sector status (for color-coded improvement indicators)
        public int CurrentSector { get; set; }     // 0=S3, 1=S1, 2=S2
        public double BestSector1 { get; set; }    // personal best S1
        public double BestSector2 { get; set; }    // personal best S1+S2 (cumulative)
        public SectorStatus S1Status { get; set; }
        public SectorStatus S2Status { get; set; }
        public SectorStatus S3Status { get; set; }
        public double S1Time { get; set; }         // last/current S1 time
        public double S2Time { get; set; }         // last/current S2 time
        public double S3Time { get; set; }         // last/current S3 time
        public double Sector3 { get; set; }

        // Extra telemetry for standings
        public double TireWearAvg { get; set; }      // 0-100%
        public double DamagePercent { get; set; }     // 0-100%
        public double EnergyPercent { get; set; }     // 0-100% battery
        public int ClassPosition { get; set; }        // position within class
    }

    public enum SectorStatus
    {
        None,           // no data (gray)
        Slower,         // slower than personal best (yellow)
        PersonalBest,   // improved personal best (green)
        SessionBest     // overall session best (purple)
    }

    public class TireData
    {
        public double[] Temperature { get; set; } = new double[3]; // Inner, Mid, Outer
        public double Wear { get; set; }
        public double Pressure { get; set; }
        public double BrakeTemp { get; set; }
        public double GripFraction { get; set; }
        public string Compound { get; set; } = "";
        public bool IsFlat { get; set; }
    }

    public class WeatherData
    {
        public double AmbientTemp { get; set; }
        public double TrackTemp { get; set; }
        public double Raining { get; set; }
        public double CloudCover { get; set; }
        public double WindSpeedX { get; set; }
        public double WindSpeedY { get; set; }
        public double WindSpeedZ { get; set; }
        public double TrackWetness { get; set; }
        public double MinWetness { get; set; }
        public double MaxWetness { get; set; }
        // Forecast (trend-based)
        public double RainTrend { get; set; }        // positive = increasing rain
        public double CloudTrend { get; set; }       // positive = clouds increasing
        public string ForecastText { get; set; } = "";
    }

    public class FuelData
    {
        public double CurrentFuel { get; set; }
        public double FuelCapacity { get; set; }
        public double FuelPerLap { get; set; }
        public double FuelAutonomy { get; set; }         // tours possibles avec fuel

        public double CurrentEnergy { get; set; }        // 0-100%
        public double EnergyCapacity { get; set; }       // 100%
        public double EnergyPerLap { get; set; }
        public double EnergyAutonomy { get; set; }       // tours possibles avec énergie

        public double RealAutonomy { get; set; }         // min(fuel, energy) = GOULOT
        public LimitingFactor Limiter { get; set; }      // qu'est-ce qui limite

        public int RaceLapsRemaining { get; set; }
        public double FuelToAdd { get; set; }
        public double FuelToEnd { get; set; }
        public double FuelDeficit { get; set; }
        public int StopsRequired { get; set; }
        public double TimeRemaining { get; set; }

        // Pit Window
        public int MaxStintLaps { get; set; }
        public double WindowClose { get; set; }
        public int WindowOpen { get; set; }
        public PitWindowState WindowState { get; set; }
        public int ValidFuelSamples { get; set; }
        public int ValidEnergySamples { get; set; }
    }

    public enum PitWindowState
    {
        NoData, TooEarly, WindowOpen, Critical
    }

    public enum LimitingFactor
    {
        None, Fuel, Energy, Balanced
    }

    public class InputData
    {
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Steering { get; set; }
        public double Clutch { get; set; }
        public int Gear { get; set; }
        public double RPM { get; set; }
        public double MaxRPM { get; set; }
        public double Speed { get; set; }
    }

    // ========================================================================
    // NEW DATA MODELS
    // ========================================================================

    public class LapRecord
    {
        public int LapNumber { get; set; }
        public double LapTime { get; set; }
        public double Sector1 { get; set; }
        public double Sector2 { get; set; }
        public double Sector3 { get; set; }
        public double FuelUsed { get; set; }
        public double FuelRemaining { get; set; }
        public string TireCompound { get; set; } = "";
        public double TrackTemp { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class TelemetryPoint
    {
        public double TrackPos  { get; set; }  // 0-1
        public double Speed     { get; set; }  // km/h
        public double Throttle  { get; set; }  // 0-1
        public double Brake     { get; set; }  // 0-1
        public int    Gear      { get; set; }
        public double RPM       { get; set; }
        public double Steering  { get; set; }  // -1 to +1
        public double Elapsed   { get; set; }  // seconds since lap start
    }

    public class LapTrace
    {
        public int    LapNumber { get; set; }
        public double LapTime   { get; set; }
        public string Compound  { get; set; } = "";
        public List<TelemetryPoint> Points { get; set; } = new();
    }

    public class DeltaData
    {
        public double CurrentDelta { get; set; }      // delta vs best lap at current track position
        public double LastLapDelta { get; set; }       // last lap time vs best lap time
        public double BestLapTime { get; set; }
        public double LastLapTime { get; set; }
        public double CurrentLapTime { get; set; }
        public double PredictedLapTime { get; set; }
        public bool IsImproving { get; set; }
    }

    public class DamageData
    {
        public double[] DentSeverity { get; set; } = new double[8]; // 0=none, 1=some, 2=more
        public double MaxImpactMagnitude { get; set; }
        public double AccumulatedImpact { get; set; }
        public double LastImpactMagnitude { get; set; }
        public bool AnyDetached { get; set; }
        public bool Overheating { get; set; }
        public double EstimatedRepairTime { get; set; } // seconds
    }

    public class GForceData
    {
        public double Lateral { get; set; }    // G left/right
        public double Longitudinal { get; set; } // G front/back
        public double Vertical { get; set; }   // G up/down
        public double Combined { get; set; }   // total magnitude
    }

    public class DashboardData
    {
        public double Speed { get; set; }
        public double RPM { get; set; }
        public double MaxRPM { get; set; }
        public int Gear { get; set; }
        public int Position { get; set; }
        public int TotalLaps { get; set; }
        public double Fuel { get; set; }
        public double FuelCapacity { get; set; }
        public double FuelPerLap { get; set; }
        public double Energy { get; set; }
        public double EnergyPerLap { get; set; }
        public int EnergyLapsRemaining { get; set; }
        public int LapsRemaining { get; set; }
        public double TimeRemaining { get; set; }
        public byte TC { get; set; }
        public byte ABS { get; set; }
        public byte Stability { get; set; }
        public byte ElectricMotorState { get; set; }
        public byte TCSlipAngle { get; set; }
        public byte TCPowerCut { get; set; }
    }

    public class SessionInfo
    {
        public string SessionName { get; set; } = "";
        public double SessionTimeElapsed { get; set; }
        public double SessionTimeRemaining { get; set; }
        public int MaxLaps { get; set; }
        public int NumVehicles { get; set; }
        public double AmbientTemp { get; set; }
        public double TrackTemp { get; set; }
        public bool Raining { get; set; }
        public byte GamePhase { get; set; }
    }

    public class PitStrategyData
    {
        // Fuel
        public double CurrentFuel { get; set; }
        public double FuelPerLap { get; set; }
        public double FuelToAdd { get; set; }
        public double FuelAutonomy { get; set; }

        // Energy
        public double CurrentEnergy { get; set; }
        public double EnergyPerLap { get; set; }
        public double EnergyAutonomy { get; set; }

        // Combined
        public double RealAutonomy { get; set; }
        public LimitingFactor Limiter { get; set; }
        public int RaceLapsRemaining { get; set; }
        public int MaxStintLaps { get; set; }

        // Pit Window
        public PitWindowState WindowState { get; set; }
        public double WindowClose { get; set; }
        public int WindowOpen { get; set; }

        // Tires
        public int LapsOnTires { get; set; }
        public double TireWearAvg { get; set; }
        public int TireLapsLeft { get; set; }
        public string TireCompound { get; set; } = "";

        // Stops
        public int StopsRemaining { get; set; }
        public double PitTimeLoss { get; set; } = 25.0;

        public int ValidFuelSamples { get; set; }
        public int ValidEnergySamples { get; set; }
    }

    public class TrackLimitsData
    {
        public int PenaltyCount { get; set; }
        public int TrackLimitWarnings { get; set; }
        public bool[] WheelOffTrack { get; set; } = new bool[4];
        public string[] WheelSurface { get; set; } = new string[4];
        public int OffTrackCount { get; set; }
        public double LastOffTrackTime { get; set; }
        public string LastLSIMessage { get; set; } = "";
        public string StatusMessage { get; set; } = "";
        public bool IsOffTrackNow { get; set; }
        public List<TrackLimitEvent> RecentEvents { get; set; } = new();
    }

    public class TrackLimitEvent
    {
        public int LapNumber { get; set; }
        public double ElapsedTime { get; set; }
        public string Type { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    // ========================================================================
    // HAZARD DETECTION (for yellow flag side indication)
    // ========================================================================

    public enum HazardSide { Left, Right, Center, Unknown }

    public class HazardInfo
    {
        public double Distance { get; set; }    // meters ahead
        public HazardSide Side { get; set; }
        public string DriverName { get; set; } = "";
        public int Sector { get; set; }         // 1, 2, or 3
        public double Speed { get; set; }       // target speed m/s
    }

    public class PisteLibreData
    {
        public bool IsPlayerSlow { get; set; }
        public bool IsClear { get; set; }
        public double NearestBehindDist { get; set; }
    }

    // ========================================================================
    // DISPLAY CONFIG — toggles for Dashboard and Input overlays
    // ========================================================================

    public class DashboardDisplayConfig
    {
        public bool ShowSpeed { get; set; } = true;
        public bool ShowRPM { get; set; } = true;
        public bool ShowGear { get; set; } = true;
        public bool ShowPosition { get; set; } = true;
        public bool ShowLap { get; set; } = true;
        public bool ShowFuel { get; set; } = true;
        public bool ShowEnergy { get; set; } = true;
        public bool ShowFuelPerLap { get; set; } = true;
        public bool ShowEnergyPerLap { get; set; } = true;
        public bool ShowLapsRemaining { get; set; } = true;
        public bool ShowTimeRemaining { get; set; } = true;
        public bool ShowTC { get; set; } = true;
        public bool ShowABS { get; set; } = true;
        public bool ShowTCSlip { get; set; } = true;
        public bool ShowTCCut { get; set; } = true;

        public static readonly (string Key, string Label)[] AllItems =
        {
            ("ShowSpeed", "Vitesse"),
            ("ShowRPM", "RPM"),
            ("ShowGear", "Rapport"),
            ("ShowPosition", "Position"),
            ("ShowLap", "Tour"),
            ("ShowFuel", "Carburant"),
            ("ShowEnergy", "Énergie (batterie)"),
            ("ShowFuelPerLap", "Conso fuel/tour"),
            ("ShowEnergyPerLap", "Conso énergie/tour"),
            ("ShowLapsRemaining", "Tours restants"),
            ("ShowTimeRemaining", "Temps restant"),
            ("ShowTC", "Traction Control"),
            ("ShowABS", "ABS"),
            ("ShowTCSlip", "TC Slip Angle"),
            ("ShowTCCut", "TC Power Cut"),
        };

        public bool IsVisible(string key)
        {
            var prop = GetType().GetProperty(key);
            return prop != null && (bool)(prop.GetValue(this) ?? false);
        }

        public void SetVisible(string key, bool val)
        {
            var prop = GetType().GetProperty(key);
            if (prop != null) prop.SetValue(this, val);
        }
    }

    public class InputDisplayConfig
    {
        public bool ShowThrottle { get; set; } = true;
        public bool ShowBrake { get; set; } = true;
        public bool ShowClutch { get; set; } = true;
        public bool ShowSteering { get; set; } = true;
        public bool ShowGear { get; set; } = true;
        public bool ShowSpeed { get; set; } = true;
        public bool ShowRPM { get; set; } = true;
        public bool ShowGraph { get; set; } = true;
        public bool ShowSteeringBar { get; set; } = true;

        // Customizable colors (hex strings)
        public string ThrottleColor { get; set; } = "#4CD964";  // green
        public string BrakeColor { get; set; } = "#FF3B30";     // red
        public string ClutchColor { get; set; } = "#007AFF";    // blue
        public string SteeringColor { get; set; } = "#FF9500";  // orange

        // Line thickness for graph traces
        public double LineThickness { get; set; } = 2.0;

        // Trail overlap alert (throttle + brake at same time)
        public bool TrailBrakeAlert { get; set; } = true;

        public static readonly (string Key, string Label)[] AllItems =
        {
            ("ShowThrottle", "Accélérateur"),
            ("ShowBrake", "Frein"),
            ("ShowClutch", "Embrayage"),
            ("ShowSteering", "Volant"),
            ("ShowGear", "Rapport"),
            ("ShowSpeed", "Vitesse"),
            ("ShowRPM", "RPM"),
            ("ShowGraph", "Graphique traces"),
            ("ShowSteeringBar", "Barre direction"),
        };

        public bool IsVisible(string key)
        {
            var prop = GetType().GetProperty(key);
            return prop != null && (bool)(prop.GetValue(this) ?? false);
        }

        public void SetVisible(string key, bool val)
        {
            var prop = GetType().GetProperty(key);
            if (prop != null) prop.SetValue(this, val);
        }

        /// <summary>Parse hex color string to WPF Color.</summary>
        public static System.Windows.Media.Color ParseColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                    return System.Windows.Media.Color.FromRgb(
                        Convert.ToByte(hex[..2], 16),
                        Convert.ToByte(hex[2..4], 16),
                        Convert.ToByte(hex[4..6], 16));
            }
            catch { }
            return System.Windows.Media.Color.FromRgb(200, 200, 200);
        }
    }

    public class StandingsDisplayConfig
    {
        public int MaxCarsPerClass { get; set; } = 10;
        public bool ShowSessionInfo { get; set; } = true;
        public bool ShowClassHeaders { get; set; } = true;
        public bool ShowTireWear { get; set; } = true;
        public bool ShowDamage { get; set; } = true;
        public bool ShowEnergy { get; set; } = true;
        public bool ShowBestLap { get; set; } = true;
        public bool ShowLastLap { get; set; } = true;
        public bool ShowGap { get; set; } = true;
        public bool ShowPitIndicator { get; set; } = true;
        public bool ShowCarNumber { get; set; } = true;
        public bool ShowSectorBlocks { get; set; } = true;

        public static readonly (string Key, string Label)[] AllItems =
        {
            ("ShowSessionInfo", "Barre session"),
            ("ShowClassHeaders", "En-têtes classes"),
            ("ShowBestLap", "Meilleur tour"),
            ("ShowLastLap", "Dernier tour / Écart"),
            ("ShowGap", "Gap"),
            ("ShowTireWear", "Usure pneus %"),
            ("ShowEnergy", "Énergie %"),
            ("ShowDamage", "Dommages %"),
            ("ShowPitIndicator", "Indicateur PIT"),
            ("ShowCarNumber", "Numéro voiture"),
            ("ShowSectorBlocks", "Blocs secteur"),
        };

        public bool IsVisible(string key)
        {
            var prop = GetType().GetProperty(key);
            return prop != null && (bool)(prop.GetValue(this) ?? false);
        }
    }

    public class RelativeConfig
    {
        public int AheadCount { get; set; } = 5;
        public int BehindCount { get; set; } = 5;
    }
}
