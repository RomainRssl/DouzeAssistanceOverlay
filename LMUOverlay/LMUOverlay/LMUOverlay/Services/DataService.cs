using LMUOverlay.Models;
using rF2SharedMemory;
using rF2SharedMemory.rFactor2Data;

namespace LMUOverlay.Services
{
    public class DataService
    {
        private readonly SharedMemoryReader _reader;

        // ---- CONSUMPTION TRACKER (dual: fuel + energy) ----
        private readonly List<double> _fuelSamples = new();
        private readonly List<double> _energySamples = new();
        private const int MAX_SAMPLES = 5;
        private const double SAFETY_MARGIN_LAPS = 1.0;

        // Fuel tracking
        private double _fuelAtStartOfLap = -1;

        // REST API — énergie virtuelle réglementaire (LMU GT3 / LMDh)
        private readonly LmuRestApiService _restApi = new();

        // Energy tracking — net change per lap (pour détecter vraie déplétion race battery)
        private double _energyAtStartOfLap = -1;
        // Energy tracking — décharge cumulée par tour (pour affichage ENRG/TOUR exact)
        private readonly List<double> _energyDeployedSamples = new();
        private double _energyDeployedThisLap = 0;
        private double _prevEnergyTick = -1;
        // Lissage affichage barre énergie (évite oscillation charge/décharge chaque tour)
        private double _smoothedEnergy = -1;
        // Valeur lissée en début de tour (pour calcul net sans bruit d'oscillation)
        private double _smoothedEnergyAtLapStart = -1;
        // Virtual Energy (REST API) — suivi par tour
        private double _veAtStartOfLap = -1;
        private readonly List<double> _veSamples = new(); // VE%/tour consommé

        // Lap detection
        private int _trackedLap = -1;
        private bool _wasInPitsThisLap;
        private bool _wasLapInvalidThisLap;

        // Stint tracking (par véhicule)
        private readonly Dictionary<int, int>    _prevPitstops   = new();
        private readonly Dictionary<int, int>    _stintLapCount  = new();
        private readonly Dictionary<int, double> _stintStartET   = new();
        private readonly Dictionary<int, int>    _prevTotalLaps  = new();

        // Session best sector tracking (for purple indicators)
        private double _sessionBestS1 = double.MaxValue;
        private double _sessionBestS2 = double.MaxValue; // S2 alone, not cumulative
        private double _sessionBestS3 = double.MaxValue;
        // Per-vehicle personal best sectors (key = vehicle ID)
        private readonly Dictionary<int, (double s1, double s2, double s3)> _personalBestSectors = new();

        // Radar cache — keeps last valid result to avoid flickering when scoring data updates at 2-5 Hz
        private List<(VehicleData Vehicle, double RelX, double RelZ)> _lastNearbyVehicles = new();

        // Radar EMA smoothing — per-vehicle smoothed relative positions (keyed by vehicle ID)
        // Eliminates position jumps caused by 5 Hz scoring vs 60 Hz player telemetry mismatch
        private readonly Dictionary<int, (double relX, double relZ)> _smoothedRadarPos = new();
        private const double RADAR_ALPHA = 0.40; // blend toward new pos per frame (~30 Hz → smooth but responsive)

        public DataService(SharedMemoryReader reader) { _reader = reader; }

        // ====================================================================
        // VEHICLE DATA
        // ====================================================================

        public List<VehicleData> GetAllVehicles()
        {
            var vehicles = new List<VehicleData>();
            if (!_reader.IsConnected) return vehicles;

            var scoring = _reader.Scoring;
            var telemetry = _reader.Telemetry;
            if (scoring.mVehicles == null) return vehicles;

            double trackLen = scoring.mScoringInfo.mLapDist;
            int count = Math.Min(scoring.mScoringInfo.mNumVehicles, scoring.mVehicles.Length);

            for (int i = 0; i < count; i++)
            {
                var v = scoring.mVehicles[i];

                double speed = Math.Sqrt(
                    v.mLocalVel.x * v.mLocalVel.x +
                    v.mLocalVel.y * v.mLocalVel.y +
                    v.mLocalVel.z * v.mLocalVel.z);

                double yaw = 0;
                if (v.mOri != null && v.mOri.Length >= 3)
                    yaw = Math.Atan2(v.mOri[rFactor2Constants.RowZ].x, v.mOri[rFactor2Constants.RowZ].z);

                double lapProgress = trackLen > 0 ? (v.mLapDist / trackLen) * 100.0 : 0;
                double lastDelta = (v.mBestLapTime > 0 && v.mLastLapTime > 0)
                    ? v.mLastLapTime - v.mBestLapTime : 0;

                // Try to find matching telemetry for tire/fuel
                string tireCompound = "", rearTireCompound = "";
                double fuel = 0;
                if (telemetry.mVehicles != null)
                {
                    int tCount = Math.Min(telemetry.mNumVehicles, telemetry.mVehicles.Length);
                    for (int t = 0; t < tCount; t++)
                    {
                        if (telemetry.mVehicles[t].mID == v.mID)
                        {
                            tireCompound     = rF2Helper.Str(telemetry.mVehicles[t].mFrontTireCompoundName);
                            rearTireCompound = rF2Helper.Str(telemetry.mVehicles[t].mRearTireCompoundName);
                            fuel = telemetry.mVehicles[t].mFuel;
                            break;
                        }
                    }
                }

                vehicles.Add(new VehicleData
                {
                    Id = v.mID,
                    DriverName = rF2Helper.Str(v.mDriverName),
                    VehicleName = rF2Helper.Str(v.mVehicleName),
                    VehicleClass = rF2Helper.Str(v.mVehicleClass),
                    Position = v.mPlace,
                    TotalLaps = v.mTotalLaps,
                    LapDistance = v.mLapDist,
                    BestLapTime = v.mBestLapTime,
                    LastLapTime = v.mLastLapTime,
                    TimeBehindNext = v.mTimeBehindNext,
                    TimeBehindLeader = v.mTimeBehindLeader,
                    Speed = speed,
                    IsPlayer = v.mIsPlayer != 0,
                    InPits = v.mInPits != 0,
                    PosX = v.mPos.x,
                    PosY = v.mPos.y,
                    PosZ = v.mPos.z,
                    YawAngle = yaw,
                    Flag = v.mFlag,

                    // Extended
                    LapProgress = lapProgress,
                    CarNumber = v.mID.ToString(),
                    LastLapDelta = lastDelta,
                    Sector1 = v.mLastSector1,
                    Sector2 = v.mLastSector2 > 0 ? v.mLastSector2 - v.mLastSector1 : 0,
                    CurSector1 = v.mCurSector1,
                    CurSector2 = v.mCurSector2,
                    NumPitstops = v.mNumPitstops,
                    NumPenalties = v.mNumPenalties,
                    EstimatedLapTime = v.mEstimatedLapTime,
                    TimeIntoLap = v.mTimeIntoLap,
                    LapStartET = v.mLapStartET,
                    PitState    = v.mPitState,
                    PitLapDist  = v.mPitLapDist,
                    LapsBehindLeader = v.mLapsBehindLeader,
                    TireCompound      = tireCompound,
                    FrontTireCompound = tireCompound,
                    RearTireCompound  = rearTireCompound,
                    UpgradePack       = rF2Helper.Str(v.mUpgradePack),
                    Fuel = fuel,

                    // Sector status (computed below)
                    CurrentSector = v.mSector,
                    BestSector1 = v.mBestSector1,
                    BestSector2 = v.mBestSector2,
                });
            }

            // Stint tracking
            double sessionET = scoring.mScoringInfo.mCurrentET;
            foreach (var v in vehicles)
            {
                int id = v.Id;

                // Arrêt pit détecté → reset stint
                if (_prevPitstops.TryGetValue(id, out int prevPit) && v.NumPitstops > prevPit)
                {
                    _stintLapCount[id] = 0;
                    _stintStartET[id]  = sessionET;
                    _prevTotalLaps[id] = v.TotalLaps;
                }
                _prevPitstops[id] = v.NumPitstops;

                // Initialisation au premier tick
                if (!_stintStartET.ContainsKey(id))  _stintStartET[id]  = sessionET;
                if (!_prevTotalLaps.ContainsKey(id))  _prevTotalLaps[id] = v.TotalLaps;
                if (!_stintLapCount.ContainsKey(id))  _stintLapCount[id] = 0;

                // Nouveau tour complété → incrémenter le compteur de relais
                if (v.TotalLaps > _prevTotalLaps[id])
                {
                    _stintLapCount[id] += v.TotalLaps - _prevTotalLaps[id];
                    _prevTotalLaps[id]  = v.TotalLaps;
                }

                v.StintLaps = _stintLapCount[id];
                v.StintTime = Math.Max(0, sessionET - _stintStartET[id]);
            }

            // Compute sector status for all vehicles
            ComputeSectorStatus(vehicles);

            // Compute class positions
            var ordered = vehicles.OrderBy(v => v.Position).ToList();
            var classCounters = new Dictionary<string, int>();
            foreach (var v in ordered)
            {
                string cls = ClassifyVehicle(v.VehicleClass);
                if (!classCounters.ContainsKey(cls)) classCounters[cls] = 0;
                classCounters[cls]++;
                v.ClassPosition = classCounters[cls];
            }

            return ordered;
        }

        private void ComputeSectorStatus(List<VehicleData> vehicles)
        {
            foreach (var v in vehicles)
            {
                // S1/S2 are already set from GetAllVehicles:
                //   v.Sector1 = mLastSector1 (last completed S1)
                //   v.Sector2 = mLastSector2 - mLastSector1 (last completed S2 standalone)
                // Compute S3 from last lap
                double s3 = (v.LastLapTime > 0 && v.Sector1 > 0 && v.Sector2 > 0)
                    ? v.LastLapTime - v.Sector1 - v.Sector2 : 0;
                v.S3Time = s3;

                // For live sectors: use CurSector data when available
                double liveS1 = v.CurSector1;  // set when S1 is crossed this lap
                double liveS2 = (v.CurSector2 > 0 && v.CurSector1 > 0)
                    ? v.CurSector2 - v.CurSector1 : 0;

                // Decide which times to use for status
                // CurrentSector: 1=in S1 (no sectors done yet), 2=in S2 (S1 done), 0=in S3 (S1+S2 done)
                double useS1, useS2, useS3;

                if (v.CurrentSector == 2 && liveS1 > 0)
                {
                    // Just crossed S1, S2 and S3 from last lap
                    useS1 = liveS1;
                    useS2 = v.Sector2;
                    useS3 = s3;
                }
                else if (v.CurrentSector == 0 && liveS1 > 0 && liveS2 > 0)
                {
                    // Just crossed S2, S3 from last lap
                    useS1 = liveS1;
                    useS2 = liveS2;
                    useS3 = s3;
                }
                else
                {
                    // Use last lap data
                    useS1 = v.Sector1;
                    useS2 = v.Sector2;
                    useS3 = s3;
                }

                v.S1Time = useS1;
                v.S2Time = useS2;
                v.S3Time = useS3;

                // Get/update personal bests
                if (!_personalBestSectors.ContainsKey(v.Id))
                    _personalBestSectors[v.Id] = (double.MaxValue, double.MaxValue, double.MaxValue);

                var pb = _personalBestSectors[v.Id];
                bool pb1Up = useS1 > 0 && useS1 < pb.s1;
                bool pb2Up = useS2 > 0 && useS2 < pb.s2;
                bool pb3Up = useS3 > 0 && useS3 < pb.s3;

                if (pb1Up) pb.s1 = useS1;
                if (pb2Up) pb.s2 = useS2;
                if (pb3Up) pb.s3 = useS3;
                _personalBestSectors[v.Id] = pb;

                // Update session bests
                if (useS1 > 0 && useS1 < _sessionBestS1) _sessionBestS1 = useS1;
                if (useS2 > 0 && useS2 < _sessionBestS2) _sessionBestS2 = useS2;
                if (useS3 > 0 && useS3 < _sessionBestS3) _sessionBestS3 = useS3;

                // Determine status
                // For current sector being driven: show None (gray = in progress)
                if (v.CurrentSector == 1)
                {
                    // Driving S1 = no current sectors done, show last lap results
                    v.S1Status = GetSectorStatus(v.Sector1, pb.s1, _sessionBestS1, false);
                    v.S2Status = GetSectorStatus(v.Sector2, pb.s2, _sessionBestS2, false);
                    v.S3Status = GetSectorStatus(s3, pb.s3, _sessionBestS3, false);
                }
                else if (v.CurrentSector == 2)
                {
                    // Driving S2 = S1 just completed
                    v.S1Status = GetSectorStatus(useS1, pb.s1, _sessionBestS1, pb1Up);
                    v.S2Status = SectorStatus.None; // in progress
                    v.S3Status = GetSectorStatus(s3, pb.s3, _sessionBestS3, false);
                }
                else // CurrentSector == 0 → driving S3
                {
                    v.S1Status = GetSectorStatus(useS1, pb.s1, _sessionBestS1, pb1Up);
                    v.S2Status = GetSectorStatus(useS2, pb.s2, _sessionBestS2, pb2Up);
                    v.S3Status = SectorStatus.None; // in progress
                }
            }
        }

        private static SectorStatus GetSectorStatus(double time, double personalBest, double sessionBest, bool justImproved)
        {
            if (time <= 0) return SectorStatus.None;

            // Allow small tolerance for float comparison
            const double eps = 0.001;

            if (Math.Abs(time - sessionBest) < eps)
                return SectorStatus.SessionBest;   // purple
            if (justImproved || Math.Abs(time - personalBest) < eps)
                return SectorStatus.PersonalBest;  // green
            return SectorStatus.Slower;            // yellow
        }

        public static string ClassifyVehicle(string vehicleClass)
        {
            string c = (vehicleClass ?? "").ToUpperInvariant();
            if (c.Contains("HYPERCAR") || c.Contains("LMH") || c.Contains("LMDH")) return "HYPERCAR";
            if (c.Contains("LMP2")) return "LMP2";
            if (c.Contains("LMP3")) return "LMP3";
            if (c.Contains("GTE") || c.Contains("LMGT")) return "GTE";
            if (c.Contains("GT3")) return "GT3";
            if (c.Contains("GT4")) return "GT4";
            return string.IsNullOrEmpty(c) ? "OTHER" : c;
        }

        /// <summary>
        /// Returns the best lap time recorded this session among all vehicles in the same class as the player.
        /// Returns -1 if the player is not found or no valid lap times exist.
        /// </summary>
        public double GetClassSessionBestLapTime()
        {
            var all = GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);
            if (player == null) return -1;
            string playerClass = ClassifyVehicle(player.VehicleClass);
            return all
                .Where(v => ClassifyVehicle(v.VehicleClass) == playerClass && v.BestLapTime > 0)
                .Select(v => v.BestLapTime)
                .DefaultIfEmpty(-1)
                .Min();
        }

        public List<VehicleData> GetRelativeStandings(int ahead = 5, int behind = 5)
        {
            var all = GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);
            if (player == null) return all;

            int idx = all.IndexOf(player);
            var result = new List<VehicleData>();
            for (int i = Math.Max(0, idx - ahead); i < idx; i++) result.Add(all[i]);
            result.Add(player);
            for (int i = idx + 1; i < Math.Min(all.Count, idx + behind + 1); i++) result.Add(all[i]);
            return result;
        }

        /// <summary>
        /// Get vehicles sorted by on-track position relative to the player.
        /// Uses LapDistance for on-track proximity, converts to time gap.
        /// Negative gap = car is AHEAD on track (shown on top).
        /// Positive gap = car is BEHIND on track (shown on bottom).
        /// </summary>
        public List<(VehicleData Vehicle, double GapToPlayer)> GetRelativeByTime(int ahead = 5, int behind = 5)
        {
            var all = GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);
            if (player == null)
                return all.Select(v => (v, 0.0)).ToList();

            double trackLength = GetTrackLength();
            if (trackLength <= 0) trackLength = 5000;

            double playerLapTime = player.LastLapTime > 10 ? player.LastLapTime : 90;
            double trackSpeed = trackLength / playerLapTime; // m/s average

            var withGap = new List<(VehicleData Vehicle, double GapToPlayer)>();

            foreach (var v in all)
            {
                if (v.IsPlayer) { withGap.Add((v, 0)); continue; }

                // On-track distance difference
                double distDiff = v.LapDistance - player.LapDistance;

                // Wraparound: shortest path on track circle
                if (distDiff > trackLength / 2) distDiff -= trackLength;
                if (distDiff < -trackLength / 2) distDiff += trackLength;

                // distDiff > 0 = car is physically ahead on track
                // distDiff < 0 = car is physically behind on track
                // Convert to time gap: ahead = negative display, behind = positive display
                double gapSeconds = trackSpeed > 0 ? -(distDiff / trackSpeed) : 0;

                withGap.Add((v, gapSeconds));
            }

            // Sort: most negative (furthest ahead) first → top of display
            withGap.Sort((a, b) => a.GapToPlayer.CompareTo(b.GapToPlayer));

            int playerIdx = withGap.FindIndex(x => x.Vehicle.IsPlayer);
            if (playerIdx < 0) return withGap;

            var result = new List<(VehicleData, double)>();
            int startAhead = Math.Max(0, playerIdx - ahead);
            int endBehind = Math.Min(withGap.Count - 1, playerIdx + behind);

            for (int i = startAhead; i <= endBehind; i++)
                result.Add(withGap[i]);

            return result;
        }

        // ====================================================================
        // PROXIMITY RADAR
        // ====================================================================

        public List<(VehicleData Vehicle, double RelX, double RelZ)> GetNearbyVehicles(double range = 30)
        {
            var all = GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);
            if (player == null) return new();

            // Use telemetry for player position + yaw (60 Hz) instead of scoring (2-5 Hz)
            // to avoid radar lag when turning.
            double playerPosX = player.PosX;
            double playerPosZ = player.PosZ;
            double playerYaw  = player.YawAngle;
            // Only use telemetry for yaw (smoother 60 Hz rotation).
            // mPos in the telemetry buffer is unreliable in LMU (often zero) — keep scoring positions.
            var tel = _reader.GetPlayerTelemetry();
            if (tel.HasValue)
            {
                var ori = tel.Value.mOri;
                if (ori != null && ori.Length > rFactor2Constants.RowZ)
                    playerYaw = Math.Atan2(ori[rFactor2Constants.RowZ].x, ori[rFactor2Constants.RowZ].z);
            }

            var rawResult = new List<(VehicleData v, double rawX, double rawZ)>();
            double cos = Math.Cos(-playerYaw), sin = Math.Sin(-playerYaw);
            foreach (var v in all)
            {
                if (v.IsPlayer) continue;
                double dx = v.PosX - playerPosX, dz = v.PosZ - playerPosZ;
                double dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist > range) continue;

                double relX = -(dx * cos - dz * sin); // negate: rF2 X axis is inverted
                double relZ = -(dx * sin + dz * cos); // negate: rF2 Z axis is inverted
                rawResult.Add((v, relX, relZ));
            }

            // If scoring returned nothing (mid-update gap), keep last known smoothed positions
            if (rawResult.Count == 0)
                return _lastNearbyVehicles;

            // Apply per-vehicle EMA smoothing to eliminate jump artifacts
            var result = new List<(VehicleData, double, double)>(rawResult.Count);
            var activeIds = new HashSet<int>(rawResult.Count);
            foreach (var (v, rawX, rawZ) in rawResult)
            {
                activeIds.Add(v.Id);
                if (_smoothedRadarPos.TryGetValue(v.Id, out var prev))
                {
                    double sX = prev.relX + RADAR_ALPHA * (rawX - prev.relX);
                    double sZ = prev.relZ + RADAR_ALPHA * (rawZ - prev.relZ);
                    _smoothedRadarPos[v.Id] = (sX, sZ);
                    result.Add((v, sX, sZ));
                }
                else
                {
                    _smoothedRadarPos[v.Id] = (rawX, rawZ);
                    result.Add((v, rawX, rawZ));
                }
            }
            // Remove stale entries for vehicles no longer nearby
            foreach (var key in _smoothedRadarPos.Keys.Where(k => !activeIds.Contains(k)).ToList())
                _smoothedRadarPos.Remove(key);

            _lastNearbyVehicles = result;
            return result;
        }

        // ====================================================================
        // BLIND SPOT DETECTION
        // ====================================================================

        /// <summary>
        /// Returns (leftAlert, rightAlert) where each is 0.0 (no car) to 1.0 (very close).
        /// A car is in blind spot when laterally offset 1.5-6m and alongside (-8m to +3m).
        /// </summary>
        public (double Left, double Right) GetBlindSpots()
        {
            var nearby = GetNearbyVehicles(15);
            double left = 0, right = 0;

            foreach (var (v, relX, relZ) in nearby)
            {
                if (relZ < -8 || relZ > 3) continue;

                double absX = Math.Abs(relX);
                if (absX < 1.5 || absX > 6.0) continue;

                // Closer = stronger
                double intensity = Math.Clamp(1.0 - (absX - 1.5) / 4.5, 0.3, 1.0);
                double longClose = 1.0 - Math.Abs(relZ) / 8.0;
                intensity *= Math.Clamp(longClose + 0.5, 0.5, 1.0);
                intensity = Math.Clamp(intensity, 0, 1);

                if (relX < 0) left = Math.Max(left, intensity);
                else right = Math.Max(right, intensity);
            }

            return (left, right);
        }

        // ====================================================================
        // TRACK CLEAR BEHIND (for rejoin after off-track)
        // ====================================================================

        /// <summary>
        /// Returns (isClear, nearestBehindDistance, nearestBehindSpeed).
        /// Clear = no car within 'safeDistance' meters behind on track.
        /// </summary>
        public (bool IsClear, double NearestDist, double NearestSpeed) GetTrackClearBehind(double safeDistance = 150)
        {
            var all = GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);
            if (player == null) return (true, 999, 0);

            double trackLength = GetTrackLength();
            if (trackLength <= 0) trackLength = 10000;

            double nearestDist = 999;
            double nearestSpeed = 0;

            foreach (var v in all)
            {
                if (v.IsPlayer || v.InPits) continue;

                // Distance behind: how far behind is this car
                double distBehind = player.LapDistance - v.LapDistance;
                if (distBehind < 0) distBehind += trackLength;

                // Only care about cars approaching from behind (within safe distance)
                if (distBehind > 0 && distBehind < safeDistance)
                {
                    if (distBehind < nearestDist)
                    {
                        nearestDist = distBehind;
                        nearestSpeed = v.Speed * 3.6; // km/h
                    }
                }
            }

            return (nearestDist > safeDistance, nearestDist, nearestSpeed);
        }

        /// <summary>Player speed in m/s.</summary>
        public double GetPlayerSpeed()
        {
            if (!_reader.IsConnected) return 0;
            var pt = _reader.GetPlayerTelemetry();
            if (pt == null) return 0;
            var tel = pt.Value;
            return Math.Sqrt(tel.mLocalVel.x * tel.mLocalVel.x +
                             tel.mLocalVel.y * tel.mLocalVel.y +
                             tel.mLocalVel.z * tel.mLocalVel.z);
        }

        // ====================================================================
        // TIRE / BRAKE DATA
        // ====================================================================

        public TireData[] GetTireData()
        {
            if (!_reader.IsConnected) return Array.Empty<TireData>();
            var pt = _reader.GetPlayerTelemetry();
            if (pt == null) return Array.Empty<TireData>();

            var tel = pt.Value;
            if (tel.mWheels == null || tel.mWheels.Length < 4) return Array.Empty<TireData>();

            var tires = new TireData[4];
            for (int i = 0; i < 4; i++)
            {
                var w = tel.mWheels[i];
                double[] temps = w.mTemperature ?? new double[3];

                tires[i] = new TireData
                {
                    // Official: temperatures are Kelvin — subtract 273.15 for Celsius
                    Temperature = new[]
                    {
                        SafeKelvinToCelsius(temps.Length > 0 ? temps[0] : 0),
                        SafeKelvinToCelsius(temps.Length > 1 ? temps[1] : 0),
                        SafeKelvinToCelsius(temps.Length > 2 ? temps[2] : 0)
                    },
                    Wear = w.mWear,
                    Pressure = w.mPressure,
                    BrakeTemp = w.mBrakeTemp, // already Celsius per official docs
                    GripFraction = w.mGripFract,
                    Compound = rF2Helper.Str(i < 2 ? tel.mFrontTireCompoundName : tel.mRearTireCompoundName),
                    IsFlat = w.mFlat != 0
                };
            }
            return tires;
        }

        private static double SafeKelvinToCelsius(double k)
        {
            if (k <= 0 || double.IsNaN(k) || double.IsInfinity(k)) return 0;
            double c = k - 273.15;
            return c is > -50 and < 500 ? c : 0;
        }

        // ====================================================================
        // FUEL + ENERGY DATA (dual consumption tracker)
        // ====================================================================

        /// <summary>
        /// Called ONCE per tick (from UpdateTelemetryTrace) to accumulate
        /// fuel/energy measurements.  GetFuelData() is read-only after this.
        /// </summary>
        private void UpdateEnergyAndFuelTracking()
        {
            if (!_reader.IsConnected) return;
            var pt = _reader.GetPlayerTelemetry();
            var ps = _reader.GetPlayerScoring();
            if (pt == null || ps == null) return;

            var tel = pt.Value;
            var scr = ps.Value;

            double V_actuel = tel.mFuel;
            double E_raw = tel.mBatteryChargeFraction * 100;

            // ── Décharge cumulée par tick ────────────────────────────────────
            // On accumule uniquement les BAISSES de batterie (décharge), pas les
            // recharges (récup freinage). Cela donne l'énergie réellement
            // déployée par tour, indépendamment de l'équilibre charge/décharge.
            if (_prevEnergyTick >= 0 && scr.mInPits == 0)
            {
                double delta = _prevEnergyTick - E_raw;   // positif = décharge
                if (delta > 0.0) _energyDeployedThisLap += delta;
            }
            _prevEnergyTick = E_raw;

            // ── Lissage barre énergie ────────────────────────────────────────
            // EMA 0.05 : très lisse, retard ~1 s mais barre stable visuellement
            if (_smoothedEnergy < 0)
                _smoothedEnergy = E_raw;
            else
                _smoothedEnergy = 0.05 * E_raw + 0.95 * _smoothedEnergy;

            // Track anomalies
            if (scr.mInPits != 0) _wasInPitsThisLap = true;

            // Lap completion trigger
            int currentLap = scr.mTotalLaps;
            if (currentLap > _trackedLap && _trackedLap >= 0)
            {
                bool isValid = !_wasInPitsThisLap && !_wasLapInvalidThisLap;

                // FUEL sample
                if (_fuelAtStartOfLap > 0 && isValid)
                {
                    double fuelUsed = _fuelAtStartOfLap - V_actuel;
                    if (fuelUsed > 0.5 && fuelUsed < 50.0)
                    {
                        _fuelSamples.Add(fuelUsed);
                        if (_fuelSamples.Count > MAX_SAMPLES) _fuelSamples.RemoveAt(0);
                    }
                }

                // ENERGY samples (deux métriques indépendantes)
                if (_smoothedEnergyAtLapStart >= 0 && isValid)
                {
                    // 1) Décharge cumulée → pour affichage ENRG/TOUR (brute, toutes les baisses)
                    if (_energyDeployedThisLap > 0.5 && _energyDeployedThisLap < 500.0)
                    {
                        _energyDeployedSamples.Add(_energyDeployedThisLap);
                        if (_energyDeployedSamples.Count > MAX_SAMPLES) _energyDeployedSamples.RemoveAt(0);
                    }

                    // 2) Variation nette (LISSÉE) → tendance réelle du niveau de batterie sur le stint
                    // Utiliser _smoothedEnergy des deux côtés élimine le bruit de l'oscillation intra-tour
                    // GT3 équilibré  : net ≈ 0 après lissage → énergie non limitante
                    // GT3 / LMDh avec déplétion réelle : net > 0 → énergie limitante détectée correctement
                    double energyNet = _smoothedEnergyAtLapStart - _smoothedEnergy;
                    if (energyNet > 0.3 && energyNet < 80.0)
                    {
                        _energySamples.Add(energyNet);
                        if (_energySamples.Count > MAX_SAMPLES) _energySamples.RemoveAt(0);
                    }
                }

                // VE% sample (REST API — énergie virtuelle réglementaire)
                if (_veAtStartOfLap >= 0 && _restApi.IsAvailable && isValid)
                {
                    double veUsed = _veAtStartOfLap - _restApi.VirtualEnergyPct;
                    if (veUsed > 0.1 && veUsed < 100.0)
                    {
                        _veSamples.Add(veUsed);
                        if (_veSamples.Count > MAX_SAMPLES) _veSamples.RemoveAt(0);
                    }
                }

                _fuelAtStartOfLap = V_actuel;
                _energyAtStartOfLap = E_raw;
                _smoothedEnergyAtLapStart = _smoothedEnergy;
                _veAtStartOfLap = _restApi.IsAvailable ? _restApi.VirtualEnergyPct : -1;
                _energyDeployedThisLap = 0;
                _wasInPitsThisLap = scr.mInPits != 0;
                _wasLapInvalidThisLap = false;
            }

            if (_trackedLap < 0)
            {
                _fuelAtStartOfLap = V_actuel;
                _energyAtStartOfLap = E_raw;
                _smoothedEnergyAtLapStart = _smoothedEnergy;
                _veAtStartOfLap = _restApi.IsAvailable ? _restApi.VirtualEnergyPct : -1;
                _energyDeployedThisLap = 0;
            }
            _trackedLap = currentLap;
        }

        public FuelData GetFuelData()
        {
            if (!_reader.IsConnected) return new FuelData();
            var pt = _reader.GetPlayerTelemetry();
            var ps = _reader.GetPlayerScoring();
            if (pt == null || ps == null) return new FuelData();

            var tel = pt.Value;
            var scr = ps.Value;
            var info = _reader.ScoringInfo;

            double V_actuel = tel.mFuel;
            double V_max = tel.mFuelCapacity;
            double E_max = 100.0;
            double sessionLeft = info.mEndET - info.mCurrentET;
            int currentLap = scr.mTotalLaps;

            // ── Moyenne carburant ────────────────────────────────────────────
            double C_fuel = _fuelSamples.Count > 0 ? _fuelSamples.Average() : 0;

            // ── Énergie virtuelle (VE) — priorité API REST LMU ───────────────
            // Si l'API REST est disponible : on utilise la VE réglementaire (GT3 / LMDh)
            // Sinon : fallback sur la variation nette de mBatteryChargeFraction (lissée)
            bool useVE = _restApi.IsAvailable && _restApi.VirtualEnergyPct >= 0;
            double C_ve  = _veSamples.Count > 0 ? _veSamples.Average() : 0;

            // Fallback batterie (uniquement si VE indisponible)
            double C_energyDeployed = _energyDeployedSamples.Count > 0 ? _energyDeployedSamples.Average() : 0;
            double C_energyNet      = _energySamples.Count > 0 ? _energySamples.Average() : 0;

            // Valeur courante d'énergie à afficher
            double currentEnergyDisplay = useVE
                ? _restApi.VirtualEnergyPct
                : (_smoothedEnergy >= 0 ? _smoothedEnergy : 0);

            // Consommation énergie par tour (pour l'affichage ENRG/TOUR)
            double C_energyPerLap = useVE
                ? (C_ve > 0 ? C_ve : 0)
                : (C_energyDeployed > 0 ? C_energyDeployed : 0);

            // Autonomies
            double L_fuel = C_fuel > 0.1 ? V_actuel / C_fuel : 0;

            // Autonomie énergie :
            // - VE disponible : énergie limitante dès qu'on a ≥ 2 tours d'historique
            // - Fallback batterie : limitante uniquement si dérive nette > 0.3 %/tour
            bool energyIsLimiting = useVE
                ? (_veSamples.Count >= 2 && C_ve > 0.1)
                : (C_energyNet > 0.3 && _energySamples.Count >= 2);

            double L_energy = energyIsLimiting
                ? currentEnergyDisplay / (useVE ? C_ve : C_energyNet)
                : 0;

            // Real autonomy = GOULOT
            double L_real;
            LimitingFactor limiter;
            bool hasFuel = _fuelSamples.Count >= 2;
            bool hasEnergy = energyIsLimiting;

            if (hasFuel && hasEnergy)
            {
                L_real = Math.Min(L_fuel, L_energy);
                double diff = Math.Abs(L_fuel - L_energy);
                limiter = diff < 0.5 ? LimitingFactor.Balanced
                    : L_fuel < L_energy ? LimitingFactor.Fuel : LimitingFactor.Energy;
            }
            else if (hasFuel) { L_real = L_fuel; limiter = LimitingFactor.Fuel; }
            else if (hasEnergy) { L_real = L_energy; limiter = LimitingFactor.Energy; }
            else { L_real = 0; limiter = LimitingFactor.None; }

            // Race laps remaining
            double T_tour = scr.mLastLapTime > 10 ? scr.mLastLapTime : 0;
            int raceLapsLeft;
            if (info.mMaxLaps > 0 && info.mMaxLaps < 10000)
                raceLapsLeft = Math.Max(0, info.mMaxLaps - currentLap);
            else if (T_tour > 10 && sessionLeft > 0)
                raceLapsLeft = (int)Math.Ceiling(sessionLeft / T_tour);
            else raceLapsLeft = 0;

            // Player vehicle class + VE eligibility
            string playerClass = "";
            var scoringVehicles = _reader.Scoring.mVehicles;
            int numVeh = Math.Min(info.mNumVehicles, scoringVehicles?.Length ?? 0);
            for (int i = 0; i < numVeh; i++)
            {
                var veh = scoringVehicles![i];
                if (veh.mIsPlayer != 0) { playerClass = rF2Helper.Str(veh.mVehicleClass); break; }
            }
            string playerClassKey = ClassifyVehicle(playerClass);
            bool hasVE = playerClassKey == "HYPERCAR" || playerClassKey == "GT3";

            // Fuel to add (only meaningful in race with enough samples)
            bool fuelDataReady = C_fuel > 0.1 && _fuelSamples.Count >= 2 && raceLapsLeft > 0;
            double V_marge = SAFETY_MARGIN_LAPS * C_fuel;
            double fuelToEnd = fuelDataReady ? raceLapsLeft * C_fuel : 0;
            double fuelToAdd = fuelDataReady
                ? Math.Max(0, (raceLapsLeft * C_fuel) - V_actuel + V_marge) : 0;
            double deficit = fuelToEnd > 0 ? Math.Max(0, fuelToEnd - V_actuel) : 0;
            int stopsRequired = (V_max > 0 && deficit > 0)
                ? Math.Min(99, (int)Math.Ceiling(deficit / V_max)) : 0;

            // VE to finish (Hypercar / GT3 only)
            double energyToEnd = 0, energyDeficit = 0;
            if (hasVE && raceLapsLeft > 0 && C_ve > 0.1 && _veSamples.Count >= 2)
            {
                energyToEnd   = raceLapsLeft * C_ve;
                energyDeficit = Math.Max(0, energyToEnd - currentEnergyDisplay);
            }

            // Pit window (based on REAL autonomy)
            int maxStintFuel = (C_fuel > 0.1 && V_max > 0) ? (int)Math.Floor(V_max / C_fuel) : 999;
            int maxStintEnergy = energyIsLimiting
                ? (int)Math.Floor(currentEnergyDisplay / (useVE ? C_ve : C_energyNet))
                : 999;
            int maxStintLaps = Math.Min(maxStintFuel, maxStintEnergy);
            double windowClose = L_real > 0 ? L_real - SAFETY_MARGIN_LAPS : 0;
            int windowOpen = raceLapsLeft - maxStintLaps;

            PitWindowState windowState;
            if (!hasFuel && !hasEnergy) windowState = PitWindowState.NoData;
            else if (windowClose <= 0) windowState = PitWindowState.Critical;
            else if (windowOpen <= 0) windowState = PitWindowState.WindowOpen;
            else windowState = PitWindowState.TooEarly;

            return new FuelData
            {
                CurrentFuel = V_actuel, FuelCapacity = V_max,
                FuelPerLap = C_fuel, FuelAutonomy = L_fuel,
                CurrentEnergy = currentEnergyDisplay,
                EnergyCapacity = E_max,
                EnergyPerLap = C_energyPerLap,
                EnergyAutonomy = L_energy,
                RealAutonomy = L_real, Limiter = limiter,
                RaceLapsRemaining = raceLapsLeft,
                FuelToAdd = fuelDataReady ? Math.Min(fuelToAdd, V_max) : 0,
                FuelToEnd = fuelToEnd, FuelDeficit = deficit,
                StopsRequired = stopsRequired, TimeRemaining = sessionLeft,
                MaxStintLaps = maxStintLaps,
                WindowClose = windowClose, WindowOpen = windowOpen,
                WindowState = windowState,
                ValidFuelSamples = _fuelSamples.Count,
                ValidEnergySamples = useVE ? _veSamples.Count : _energyDeployedSamples.Count,
                PlayerVehicleClass = playerClassKey,
                HasVirtualEnergy   = hasVE,
                EnergyToEnd        = energyToEnd,
                EnergyDeficit      = energyDeficit
            };
        }

        // ====================================================================
        // WEATHER
        // ====================================================================

        // Weather trend tracking
        private readonly List<(double rain, double cloud, double time)> _weatherHistory = new();
        private double _lastWeatherET;

        public WeatherData GetWeatherData()
        {
            if (!_reader.IsConnected) return new WeatherData();
            var info = _reader.ScoringInfo;

            double rain = info.mRaining;
            double cloud = info.mDarkCloud;
            double et = info.mCurrentET;

            // Sample every 30 seconds
            if (et - _lastWeatherET > 30)
            {
                _weatherHistory.Add((rain, cloud, et));
                if (_weatherHistory.Count > 40) _weatherHistory.RemoveAt(0); // keep ~20 min
                _lastWeatherET = et;
            }

            // Compute trends (compare last 5 min vs current)
            double rainTrend = 0, cloudTrend = 0;
            string forecast = "";

            if (_weatherHistory.Count >= 4)
            {
                // Average of oldest quarter vs newest quarter
                int q = _weatherHistory.Count / 4;
                double oldRain = _weatherHistory.Take(q).Average(w => w.rain);
                double newRain = _weatherHistory.Skip(_weatherHistory.Count - q).Average(w => w.rain);
                double oldCloud = _weatherHistory.Take(q).Average(w => w.cloud);
                double newCloud = _weatherHistory.Skip(_weatherHistory.Count - q).Average(w => w.cloud);

                rainTrend = newRain - oldRain;
                cloudTrend = newCloud - oldCloud;

                // Forecast logic
                if (rain > 0.3)
                {
                    if (rainTrend > 0.05) forecast = "Pluie en augmentation";
                    else if (rainTrend < -0.05) forecast = "Pluie en diminution";
                    else forecast = "Pluie stable";
                }
                else if (rain > 0.05)
                {
                    if (rainTrend > 0.02) forecast = "Pluie en approche";
                    else forecast = "Bruine légère";
                }
                else
                {
                    if (cloudTrend > 0.1 && cloud > 0.5) forecast = "Risque de pluie";
                    else if (cloudTrend > 0.05) forecast = "Nuages en hausse";
                    else if (cloud > 0.6) forecast = "Couvert, sec";
                    else if (cloud < 0.2) forecast = "Dégagé";
                    else forecast = "Sec";
                }
            }

            return new WeatherData
            {
                AmbientTemp = info.mAmbientTemp,
                TrackTemp = info.mTrackTemp,
                Raining = rain,
                CloudCover = cloud,
                WindSpeedX = info.mWind.x,
                WindSpeedY = info.mWind.y,
                WindSpeedZ = info.mWind.z,
                TrackWetness = info.mAvgPathWetness,
                MinWetness = info.mMinPathWetness,
                MaxWetness = info.mMaxPathWetness,
                RainTrend = rainTrend,
                CloudTrend = cloudTrend,
                ForecastText = forecast
            };
        }

        // ====================================================================
        // INPUT DATA
        // ====================================================================

        public InputData GetInputData()
        {
            if (!_reader.IsConnected) return new InputData();
            var pt = _reader.GetPlayerTelemetry();
            var ps = _reader.GetPlayerScoring();
            if (pt == null) return new InputData();

            var tel = pt.Value;

            // Speed from local velocity (mLocalVel.z is forward in vehicle frame)
            double speed = Math.Sqrt(
                tel.mLocalVel.x * tel.mLocalVel.x +
                tel.mLocalVel.y * tel.mLocalVel.y +
                tel.mLocalVel.z * tel.mLocalVel.z) * 3.6; // m/s → km/h

            double trackPos = 0;
            if (ps != null)
            {
                double trackLen = _reader.ScoringInfo.mLapDist;
                if (trackLen > 0)
                    trackPos = Math.Clamp(ps.Value.mLapDist / trackLen, 0, 1);
            }

            return new InputData
            {
                Throttle = tel.mUnfilteredThrottle,
                Brake    = tel.mUnfilteredBrake,
                Steering = tel.mUnfilteredSteering,
                Clutch   = tel.mUnfilteredClutch,
                Gear     = tel.mGear,
                RPM      = tel.mEngineRPM,
                MaxRPM   = tel.mEngineMaxRPM,
                Speed    = speed,
                TrackPos = trackPos
            };
        }

        /// <summary>
        /// Interpolation linéaire des inputs du meilleur tour all-time à une position donnée (0-1).
        /// Retourne null si aucune trace n'est disponible.
        /// </summary>
        public TelemetryPoint? GetGhostInputsAt(double trackPos)
        {
            var pts = _allTimeBestTrace?.Points;
            if (pts == null || pts.Count < 2) return null;

            // Recherche dichotomique du point juste avant trackPos
            int lo = 0, hi = pts.Count - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) / 2;
                if (pts[mid].TrackPos <= trackPos) lo = mid;
                else                               hi = mid;
            }

            var a = pts[lo];
            var b = pts[hi];
            double span = b.TrackPos - a.TrackPos;
            if (span <= 0) return a;

            double t = Math.Clamp((trackPos - a.TrackPos) / span, 0, 1);
            return new TelemetryPoint
            {
                TrackPos = trackPos,
                Throttle = a.Throttle + t * (b.Throttle - a.Throttle),
                Brake    = a.Brake    + t * (b.Brake    - a.Brake),
                Steering = a.Steering + t * (b.Steering - a.Steering),
                Speed    = a.Speed    + t * (b.Speed    - a.Speed),
                Gear     = (int)Math.Round(a.Gear + t * (b.Gear - a.Gear)),
                RPM      = a.RPM      + t * (b.RPM      - a.RPM)
            };
        }

        // ====================================================================
        // DASHBOARD DATA (extended for VR dashboard)
        // ====================================================================

        public DashboardData GetDashboardData()
        {
            if (!_reader.IsConnected) return new DashboardData();
            var pt = _reader.GetPlayerTelemetry();
            var ps = _reader.GetPlayerScoring();
            if (pt == null || ps == null) return new DashboardData();

            var tel = pt.Value;
            var scr = ps.Value;
            var ext = _reader.Extended;

            double speed = Math.Sqrt(
                tel.mLocalVel.x * tel.mLocalVel.x +
                tel.mLocalVel.y * tel.mLocalVel.y +
                tel.mLocalVel.z * tel.mLocalVel.z) * 3.6;

            // Get fuel data (which now includes energy tracking)
            var fuel = GetFuelData();

            return new DashboardData
            {
                Speed = speed,
                RPM = tel.mEngineRPM,
                MaxRPM = tel.mEngineMaxRPM,
                Gear = tel.mGear,
                Position = scr.mPlace,
                TotalLaps = scr.mTotalLaps,
                Fuel = tel.mFuel,
                FuelCapacity = tel.mFuelCapacity,
                FuelPerLap = fuel.FuelPerLap,
                Energy = fuel.CurrentEnergy,
                EnergyPerLap = fuel.EnergyPerLap,
                EnergyLapsRemaining = (int)fuel.EnergyAutonomy,
                LapsRemaining = fuel.RaceLapsRemaining,
                TimeRemaining = fuel.TimeRemaining,
                ABS = ext.mPhysics.mAntiLockBrakes,
                Stability = ext.mPhysics.mStabilityControl,
                ElectricMotorState = tel.mElectricBoostMotorState,
                WaterTemp = tel.mEngineWaterTemp,
                OilTemp = tel.mEngineOilTemp,
                Overheating = tel.mOverheating != 0,
                PitLimiter = scr.mInPits != 0,
            };
        }

        // ====================================================================
        // GAP DATA
        // ====================================================================

        public (VehicleData? Ahead, double GapAhead, VehicleData? Behind, double GapBehind) GetGapData()
        {
            // Use the relative-by-time method for real-time on-track gaps
            var relative = GetRelativeByTime(1, 1);
            if (relative.Count == 0) return (null, 0, null, 0);

            VehicleData? ahead = null, behind = null;
            double gapAhead = 0, gapBehind = 0;

            foreach (var (v, gap) in relative)
            {
                if (v.IsPlayer) continue;
                if (gap < 0 && ahead == null) { ahead = v; gapAhead = Math.Abs(gap); }
                if (gap > 0 && behind == null) { behind = v; gapBehind = gap; }
            }

            return (ahead, gapAhead, behind, gapBehind);
        }

        // ====================================================================
        // FLAGS
        // ====================================================================

        public byte GetCurrentFlag()
        {
            if (!_reader.IsConnected) return 0;
            var ps = _reader.GetPlayerScoring();
            return ps.HasValue ? ps.Value.mFlag : (byte)0;
        }

        public sbyte GetYellowFlagState()
        {
            if (!_reader.IsConnected) return 0;
            return _reader.ScoringInfo.mYellowFlagState;
        }

        public byte GetGamePhase()
        {
            if (!_reader.IsConnected) return 0;
            return _reader.ScoringInfo.mGamePhase;
        }

        public bool GetPlayerInPits()
        {
            if (!_reader.IsConnected) return false;
            var ps = _reader.GetPlayerScoring();
            return ps.HasValue && ps.Value.mInPits != 0;
        }

        /// <summary>
        /// Retourne la distance entre la voiture du joueur et l'entrée de la voie des stands.
        /// DistanceToPit  : mètres restants avant l'entrée des pits (0 si déjà dans les pits).
        /// PitState       : 0=Aucun, 1=Demandé, 2=Entrée, 3=Arrêté, 4=Sortie.
        /// InPits         : true si la voiture est actuellement dans la pit lane.
        /// </summary>
        public (double DistanceToPit, byte PitState, bool InPits) GetPitDistanceData()
        {
            if (!_reader.IsConnected) return (0, 0, false);
            var ps = _reader.GetPlayerScoring();
            if (!ps.HasValue) return (0, 0, false);

            var  v          = ps.Value;
            bool inPits     = v.mInPits != 0;
            byte pitState   = v.mPitState;
            double trackLen = _reader.ScoringInfo.mLapDist;

            if (inPits || trackLen <= 0)
                return (0, pitState, inPits);

            // mPitLapDist can be 0 or negative on some tracks — treat as unknown
            if (v.mPitLapDist <= 0)
                return (0, pitState, inPits);

            double dist = v.mPitLapDist - v.mLapDist;
            if (dist < 0) dist += trackLen;   // wraparound après la ligne S/F

            // Sanity check: if distance is more than 90% of the track, the value is suspect
            if (dist > trackLen * 0.90)
                return (0, pitState, inPits);

            return (dist, pitState, inPits);
        }

        /// <summary>
        /// true quand la voiture du joueur est dans son garage (mInGarageStall != 0).
        /// Ce champ rF2 est 1 uniquement quand la voiture est physiquement dans un box garage,
        /// pas lors d'un arrêt pit normal sur la pit lane.
        /// </summary>
        public bool GetPlayerInGarage()
        {
            if (!_reader.IsConnected) return true;
            var ps = _reader.GetPlayerScoring();
            if (!ps.HasValue) return true;
            return ps.Value.mInGarageStall != 0;
        }

        public sbyte[] GetSectorFlags()
        {
            if (!_reader.IsConnected) return new sbyte[3];
            return _reader.ScoringInfo.mSectorFlag ?? new sbyte[3];
        }

        public double GetTrackLength()
        {
            return _reader.IsConnected ? _reader.ScoringInfo.mLapDist : 0;
        }

        // ====================================================================
        // HAZARD DETECTION (yellow flag side: left/right/center)
        // ====================================================================

        // Persistence: must be slow for > 0.3 second
        private readonly Dictionary<int, double> _hazardFirstSeen = new();

        public List<HazardInfo> GetNearbyHazards(double maxDistance = 500)
        {
            if (!_reader.IsConnected) return new();

            var all = GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);
            if (player == null) return new();

            // If WE are slow (< 50 km/h), don't show hazards — we ARE the hazard
            if (player.Speed < 13.9) return new();

            double trackLength = GetTrackLength();
            if (trackLength <= 0) trackLength = 10000;

            double currentET = _reader.ScoringInfo.mCurrentET;
            var hazards = new List<HazardInfo>();

            double playerSpeed = player.Speed;

            // Player forward vector
            double fwdX = Math.Sin(player.YawAngle);
            double fwdZ = Math.Cos(player.YawAngle);

            foreach (var v in all)
            {
                if (v.IsPlayer || v.InPits) continue;

                // Hazard = EITHER very slow (< 30 m/s = 108 km/h) OR much slower than player (< 40%)
                bool isSlow = v.Speed < 30.0;
                bool isMuchSlower = playerSpeed > 10 && v.Speed < playerSpeed * 0.4;

                if (!isSlow && !isMuchSlower)
                {
                    _hazardFirstSeen.Remove(v.Id);
                    continue;
                }

                // Must be ahead on track
                double distAhead = v.LapDistance - player.LapDistance;
                if (distAhead < 0) distAhead += trackLength;
                if (distAhead > maxDistance) continue;

                // Persistence: must be slow for > 0.3 second
                if (!_hazardFirstSeen.ContainsKey(v.Id))
                {
                    _hazardFirstSeen[v.Id] = currentET;
                    continue;
                }
                if (currentET - _hazardFirstSeen[v.Id] < 0.3) continue;

                // Cross product: determine LEFT / RIGHT / CENTER
                double dx = v.PosX - player.PosX;
                double dz = v.PosZ - player.PosZ;
                double cross = fwdX * dz - fwdZ * dx;

                HazardSide side;
                if (Math.Abs(cross) < 2.5)
                    side = HazardSide.Center;
                else if (cross > 0)
                    side = HazardSide.Right;
                else
                    side = HazardSide.Left;

                int sector = (int)(v.LapDistance / (trackLength / 3.0)) + 1;
                sector = Math.Clamp(sector, 1, 3);

                hazards.Add(new HazardInfo
                {
                    Distance = distAhead,
                    Side = side,
                    DriverName = v.DriverName,
                    Sector = sector,
                    Speed = v.Speed
                });
            }

            // Clean up
            var toRemove = _hazardFirstSeen.Keys
                .Where(id => !all.Any(v => v.Id == id && (v.Speed < 30.0 || (playerSpeed > 10 && v.Speed < playerSpeed * 0.4))))
                .ToList();
            foreach (var id in toRemove) _hazardFirstSeen.Remove(id);

            hazards.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return hazards;
        }

        // ====================================================================
        // PISTE LIBRE — safe to rejoin when slow/off-track
        // ====================================================================

        public PisteLibreData GetPisteLibre()
        {
            var all = GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);
            if (player == null) return new PisteLibreData();

            // Player is "slow" if speed < 50 km/h (13.9 m/s)
            bool isSlow = player.Speed < 13.9;
            if (!isSlow) return new PisteLibreData { IsPlayerSlow = false };

            double trackLength = GetTrackLength();
            if (trackLength <= 0) trackLength = 10000;

            double clearDistance = 150; // meters
            double nearestBehind = double.MaxValue;

            foreach (var v in all)
            {
                if (v.IsPlayer || v.InPits) continue;
                if (v.Speed < 5) continue; // ignore other slow/stopped cars

                // Distance behind: how far back is this car
                double distBehind = player.LapDistance - v.LapDistance;
                if (distBehind < 0) distBehind += trackLength;

                // Only care about cars approaching from behind (within half track)
                if (distBehind > trackLength / 2) continue;
                if (distBehind < nearestBehind) nearestBehind = distBehind;
            }

            return new PisteLibreData
            {
                IsPlayerSlow = true,
                IsClear = nearestBehind > clearDistance,
                NearestBehindDist = nearestBehind < double.MaxValue ? nearestBehind : 999
            };
        }

        public SessionInfo GetSessionInfo()
        {
            if (!_reader.IsConnected) return new SessionInfo();
            var info = _reader.ScoringInfo;
            string[] sessionNames = { "Test", "Practice", "Qualification", "Warmup", "Race" };
            int sessionIdx = Math.Clamp(info.mSession, 0, sessionNames.Length - 1);

            double elapsed = info.mCurrentET;
            double remaining = info.mEndET - info.mCurrentET;

            return new SessionInfo
            {
                SessionName = sessionNames[sessionIdx],
                SessionTimeElapsed = elapsed,
                SessionTimeRemaining = remaining,
                MaxLaps = info.mMaxLaps,
                NumVehicles = info.mNumVehicles,
                AmbientTemp = info.mAmbientTemp,
                TrackTemp = info.mTrackTemp,
                Raining = info.mRaining > 0,
                GamePhase = info.mGamePhase
            };
        }

        // ====================================================================
        // DELTA TIME (vs best lap) — smooth interpolated position
        // ====================================================================

        private double _bestLapTime = -1;
        private readonly List<(double dist, double time)> _bestLapProfile = new();
        private readonly List<(double dist, double time)> _currentLapProfile = new();
        private int _deltaLastLap = -1;
        private double _lastProfileDist;

        // Smooth position interpolation
        private double _lastScoringDist;   // last mLapDist from scoring
        private double _lastScoringET;     // elapsed time when we got it
        private double _smoothDist;        // interpolated distance

        // Delta smoothing (exponential moving average)
        private double _smoothDelta;
        private const double DELTA_SMOOTH = 0.15; // 0 = no smoothing, 1 = frozen

        public DeltaData GetDeltaData()
        {
            if (!_reader.IsConnected) return new DeltaData();
            var ps = _reader.GetPlayerScoring();
            var pt = _reader.GetPlayerTelemetry();
            if (ps == null || pt == null) return new DeltaData();

            var scr = ps.Value;
            var tel = pt.Value;

            // Telemetry times (60Hz)
            double currentET = tel.mElapsedTime;
            double lapStartET = tel.mLapStartET > 0 ? tel.mLapStartET : scr.mLapStartET;
            double currentLapTime = Math.Max(0, currentET - lapStartET);

            // Scoring distance (5-10Hz) — detect when it updates
            double scoringDist = scr.mLapDist;
            if (Math.Abs(scoringDist - _lastScoringDist) > 0.1)
            {
                _lastScoringDist = scoringDist;
                _lastScoringET = currentET;
                _smoothDist = scoringDist;
            }
            else
            {
                // Interpolate between scoring updates using vehicle speed
                double dt = currentET - _lastScoringET;
                double speed = Math.Sqrt(
                    tel.mLocalVel.x * tel.mLocalVel.x +
                    tel.mLocalVel.z * tel.mLocalVel.z); // m/s on track plane
                _smoothDist = _lastScoringDist + speed * dt;
            }

            double lapDist = _smoothDist;

            // New lap detection
            if (scr.mTotalLaps != _deltaLastLap)
            {
                if (_deltaLastLap >= 0 && scr.mLastLapTime > 0)
                {
                    if (_bestLapTime < 0 || scr.mLastLapTime < _bestLapTime)
                    {
                        _bestLapTime = scr.mLastLapTime;
                        _bestLapProfile.Clear();
                        _bestLapProfile.AddRange(_currentLapProfile);
                    }

                    double s1 = scr.mLastSector1;
                    double s2 = scr.mLastSector2 > 0 ? scr.mLastSector2 - scr.mLastSector1 : 0;
                    double s3 = scr.mLastSector2 > 0 ? scr.mLastLapTime - scr.mLastSector2 : 0;

                    _lapHistory.Add(new LapRecord
                    {
                        LapNumber     = _deltaLastLap,
                        LapTime       = scr.mLastLapTime,
                        Sector1       = s1,
                        Sector2       = s2,
                        Sector3       = s3,
                        FuelUsed      = _fuelSamples.Count > 0 ? _fuelSamples[^1] : 0,
                        FuelRemaining = tel.mFuel,
                        TireCompound  = rF2Helper.Str(tel.mFrontTireCompoundName),
                        TrackTemp     = _reader.ScoringInfo.mTrackTemp
                    });

                    LapCompleted?.Invoke(new LapCompletedArgs(
                        Circuit:  rF2Helper.Str(_reader.ScoringInfo.mTrackName),
                        CarClass: rF2Helper.Str(scr.mVehicleClass),
                        CarName:  rF2Helper.Str(scr.mVehicleName),
                        LapTime:  scr.mLastLapTime,
                        Sector1:  s1,
                        Sector2:  s2,
                        Sector3:  s3));
                }
                _currentLapProfile.Clear();
                _lastProfileDist = -1;
                _smoothDist = scoringDist;
                _lastScoringDist = scoringDist;
                _lastScoringET = currentET;
                _deltaLastLap = scr.mTotalLaps;
            }

            // Record profile every 5m (using smooth dist for finer resolution)
            if (lapDist > _lastProfileDist + 5)
            {
                _currentLapProfile.Add((lapDist, currentLapTime));
                _lastProfileDist = lapDist;
            }

            // Calculate raw delta with interpolation
            double rawDelta = 0;
            if (_bestLapProfile.Count > 10 && lapDist > 0)
            {
                int lo = 0, hi = _bestLapProfile.Count - 1;
                while (lo < hi - 1)
                {
                    int mid = (lo + hi) / 2;
                    if (_bestLapProfile[mid].dist <= lapDist) lo = mid;
                    else hi = mid;
                }

                var pLo = _bestLapProfile[lo];
                var pHi = lo < _bestLapProfile.Count - 1 ? _bestLapProfile[lo + 1] : pLo;

                double bestTimeAtDist;
                if (pHi.dist > pLo.dist)
                {
                    double frac = Math.Clamp((lapDist - pLo.dist) / (pHi.dist - pLo.dist), 0, 1);
                    bestTimeAtDist = pLo.time + frac * (pHi.time - pLo.time);
                }
                else
                {
                    bestTimeAtDist = pLo.time;
                }

                rawDelta = currentLapTime - bestTimeAtDist;
            }

            // Smooth delta (EMA filter to remove jitter)
            _smoothDelta = _smoothDelta * DELTA_SMOOTH + rawDelta * (1.0 - DELTA_SMOOTH);

            double lastDelta = (_bestLapTime > 0 && scr.mLastLapTime > 0)
                ? scr.mLastLapTime - _bestLapTime : 0;

            return new DeltaData
            {
                CurrentDelta = _smoothDelta,
                LastLapDelta = lastDelta,
                BestLapTime = _bestLapTime > 0 ? _bestLapTime : scr.mBestLapTime,
                LastLapTime = scr.mLastLapTime,
                CurrentLapTime = currentLapTime,
                PredictedLapTime = _bestLapTime > 0 ? _bestLapTime + _smoothDelta : currentLapTime,
                IsImproving = _smoothDelta < 0
            };
        }

        // ====================================================================
        // LAP HISTORY
        // ====================================================================

        private readonly List<LapRecord> _lapHistory = new();

        public List<LapRecord> GetLapHistory() => new(_lapHistory);

        /// <summary>
        /// Déclenché chaque fois qu'un tour valide est enregistré.
        /// Payload : circuit, classe, voiture, temps + secteurs.
        /// </summary>
        public event Action<LapCompletedArgs>? LapCompleted;

        public record LapCompletedArgs(
            string Circuit,
            string CarClass,
            string CarName,
            double LapTime,
            double Sector1,
            double Sector2,
            double Sector3);

        // ====================================================================
        // TELEMETRY TRACES
        // ====================================================================

        private readonly List<LapTrace>       _lapTraces    = new();
        private readonly List<TelemetryPoint> _currentTrace = new();
        private double _lastTraceDist = -1;
        private const int MAX_TRACES = 30;
        private const double TRACE_INTERVAL_M = 5.0; // sample every 5m

        // ── Tours de référence ──────────────────────────────────────────────────
        // All-time best : persisté par piste+classe dans %APPDATA%\DouzeAssistance\bestlaps\
        private LapTrace? _allTimeBestTrace;
        private string    _allTimeBestKey  = ""; // "trackName|carClass"
        // Meilleur adversaire de la session (vitesse uniquement)
        private LapTrace? _opponentBestTrace;
        private double    _opponentBestTime = double.MaxValue;
        // Suivi temps réel des adversaires
        private readonly Dictionary<int, List<TelemetryPoint>> _vehicleTraces   = new();
        private readonly Dictionary<int, int>    _vehicleLastLap  = new();
        private readonly Dictionary<int, double> _vehicleLastDist = new();

        /// <summary>
        /// Quand false, la collecte de points de télémétrie est suspendue.
        /// UpdateEnergyAndFuelTracking() continue de tourner indépendamment.
        /// </summary>
        public bool IsRecordingTelemetry { get; set; } = true;

        public void UpdateTelemetryTrace()
        {
            // Update fuel/energy tracking once per tick (before any GetFuelData() calls)
            UpdateEnergyAndFuelTracking();

            if (!IsRecordingTelemetry) return;
            if (!_reader.IsConnected) return;
            var ps = _reader.GetPlayerScoring();
            var pt = _reader.GetPlayerTelemetry();
            if (ps == null || pt == null) return;

            var scr = ps.Value;
            var tel = pt.Value;
            var info = _reader.ScoringInfo;

            // ── Détection de changement de piste/classe → charger le best all-time ──
            string trackName = rF2Helper.Str(info.mTrackName);
            string carClass  = rF2Helper.Str(scr.mVehicleClass);
            string trackKey  = $"{trackName}|{carClass}";
            if (trackKey != _allTimeBestKey && !string.IsNullOrEmpty(trackName))
            {
                _allTimeBestKey = trackKey;
                TryLoadAllTimeBest(trackName, carClass);
                TryLoadOpponentBest(trackName, carClass);
                // Reset all session data — new circuit/class, nothing carries over
                _lapTraces.Clear();
                _currentTrace.Clear();
                _lastTraceDist = -1;
                _traceLastLap  = -1;
                _vehicleTraces.Clear();
                _vehicleLastLap.Clear();
                _vehicleLastDist.Clear();
                _opponentBestTrace = null;
                _opponentBestTime  = double.MaxValue;
            }

            double lapLength = info.mLapDist; // mLapDist in ScoringInfo = total track length
            if (lapLength <= 0) return;

            double dist = scr.mLapDist;
            if (dist < 0) return;

            // Detect new lap: save current trace
            if (scr.mTotalLaps != _traceLastLap)
            {
                if (_traceLastLap >= 0 && _currentTrace.Count > 10 && scr.mLastLapTime > 0)
                {
                    var trace = new LapTrace
                    {
                        LapNumber = _traceLastLap,
                        LapTime   = scr.mLastLapTime,
                        Compound  = rF2Helper.Str(tel.mFrontTireCompoundName),
                        Points    = new List<TelemetryPoint>(_currentTrace)
                    };
                    _lapTraces.Add(trace);
                    if (_lapTraces.Count > MAX_TRACES)
                        _lapTraces.RemoveAt(0);

                    // ── Mise à jour du meilleur tour all-time ──────────────────
                    if (_allTimeBestTrace == null || scr.mLastLapTime < _allTimeBestTrace.LapTime)
                    {
                        _allTimeBestTrace = new LapTrace
                        {
                            LapNumber = -1,
                            LapTime   = scr.mLastLapTime,
                            Compound  = "ALL-TIME BEST",
                            Points    = new List<TelemetryPoint>(_currentTrace)
                        };
                        SaveAllTimeBest(trackName, carClass);
                    }
                }
                _currentTrace.Clear();
                _lastTraceDist = -1;
                _traceLastLap = scr.mTotalLaps;
            }

            // Sample every TRACE_INTERVAL_M
            if (dist < _lastTraceDist + TRACE_INTERVAL_M) return;
            _lastTraceDist = dist;

            double lapStartET = tel.mLapStartET > 0 ? tel.mLapStartET : scr.mLapStartET;
            double speed = Math.Sqrt(
                tel.mLocalVel.x * tel.mLocalVel.x +
                tel.mLocalVel.z * tel.mLocalVel.z) * 3.6; // m/s → km/h

            _currentTrace.Add(new TelemetryPoint
            {
                TrackPos = Math.Clamp(dist / lapLength, 0, 1),
                Speed    = speed,
                Throttle = tel.mUnfilteredThrottle,
                Brake    = tel.mUnfilteredBrake,
                Gear     = tel.mGear,
                RPM      = tel.mEngineRPM,
                Steering = tel.mUnfilteredSteering,
                Elapsed  = Math.Max(0, tel.mElapsedTime - lapStartET)
            });
        }

        private int _traceLastLap = -1;

        public List<LapTrace>  GetLapTraces()        => new(_lapTraces);
        public LapTrace?       GetAllTimeBestTrace() => _allTimeBestTrace;
        public LapTrace?       GetOpponentBestTrace() => _opponentBestTrace;

        // ====================================================================
        // SUIVI DES ADVERSAIRES
        // ====================================================================

        /// <summary>
        /// À appeler à chaque tick depuis OverlayManager.
        /// Suit la position de chaque adversaire (hors pits) et sauvegarde
        /// la trace du meilleur temps de session quand un tour est complété.
        /// </summary>
        public void UpdateOpponentTraces()
        {
            if (!_reader.IsConnected) return;

            var    scoring  = _reader.Scoring;
            double trackLen = scoring.mScoringInfo.mLapDist;
            if (trackLen <= 0 || scoring.mVehicles == null) return;

            int count = Math.Min(scoring.mScoringInfo.mNumVehicles, scoring.mVehicles.Length);

            // Determine player vehicle class so we only track same-class opponents
            string playerClass = "";
            for (int i = 0; i < count; i++)
            {
                if (scoring.mVehicles[i].mIsPlayer != 0)
                {
                    playerClass = rF2Helper.Str(scoring.mVehicles[i].mVehicleClass);
                    break;
                }
            }

            for (int i = 0; i < count; i++)
            {
                var v = scoring.mVehicles[i];
                if (v.mIsPlayer != 0) continue; // ignorer le joueur
                if (v.mInPits != 0)  continue;  // ignorer les voitures aux stands
                // Only track opponents of the same class as the player
                if (!string.IsNullOrEmpty(playerClass) &&
                    rF2Helper.Str(v.mVehicleClass) != playerClass) continue;

                int    vid        = v.mID;
                int    currentLap = v.mTotalLaps;
                double dist       = v.mLapDist;

                // ── Détection fin de tour ─────────────────────────────────────
                if (_vehicleLastLap.TryGetValue(vid, out int lastLap) &&
                    lastLap >= 0 && lastLap != currentLap)
                {
                    double lapTime = v.mLastLapTime;
                    if (lapTime > 10 &&
                        _vehicleTraces.TryGetValue(vid, out var vtrace) &&
                        vtrace.Count > 10 &&
                        lapTime < _opponentBestTime)
                    {
                        _opponentBestTime  = lapTime;
                        string driverName  = rF2Helper.Str(v.mDriverName);
                        _opponentBestTrace = new LapTrace
                        {
                            LapNumber = -2,
                            LapTime   = lapTime,
                            Compound  = string.IsNullOrEmpty(driverName) ? "ADVERSAIRE" : driverName,
                            Points    = new List<TelemetryPoint>(vtrace)
                        };
                        // Persister immédiatement sur disque
                        var kp = _allTimeBestKey.Split('|');
                        if (kp.Length == 2) SaveOpponentBest(kp[0], kp[1]);
                    }
                    // Nouveau tour : réinitialiser la trace
                    _vehicleTraces[vid]   = new List<TelemetryPoint>();
                    _vehicleLastDist[vid] = -999;
                }

                _vehicleLastLap[vid] = currentLap;

                if (!_vehicleTraces.ContainsKey(vid))
                {
                    _vehicleTraces[vid]   = new List<TelemetryPoint>();
                    _vehicleLastDist[vid] = -999;
                }

                // ── Échantillonnage tous les TRACE_INTERVAL_M ─────────────────
                double lastDist = _vehicleLastDist.GetValueOrDefault(vid, -999);
                if (dist < lastDist + TRACE_INTERVAL_M) continue;
                _vehicleLastDist[vid] = dist;

                double speed = Math.Sqrt(
                    v.mLocalVel.x * v.mLocalVel.x +
                    v.mLocalVel.z * v.mLocalVel.z) * 3.6; // m/s → km/h

                _vehicleTraces[vid].Add(new TelemetryPoint
                {
                    TrackPos = Math.Clamp(dist / trackLen, 0, 1),
                    Speed    = speed
                    // Throttle, Brake, RPM, etc. non disponibles pour les adversaires
                });
            }
        }

        // ====================================================================
        // PROPRIÉTÉS PUBLIQUES — circuit/classe courants
        // ====================================================================

        public string CurrentTrackName
        {
            get
            {
                if (string.IsNullOrEmpty(_allTimeBestKey)) return "";
                var p = _allTimeBestKey.Split('|');
                return p.Length > 0 ? p[0] : "";
            }
        }

        public string CurrentCarClass
        {
            get
            {
                if (string.IsNullOrEmpty(_allTimeBestKey)) return "";
                var p = _allTimeBestKey.Split('|');
                return p.Length > 1 ? p[1] : "";
            }
        }

        /// <summary>
        /// Permet à TelemetryPanel de forcer le chargement des bests
        /// pour le circuit/classe d'un fichier importé.
        /// </summary>
        public void LoadBestsForTrack(string trackName, string carClass)
        {
            TryLoadAllTimeBest(trackName, carClass);
            TryLoadOpponentBest(trackName, carClass);
        }

        // ====================================================================
        // PERSISTANCE DU BEST ALL-TIME
        // ====================================================================

        private static string BestLapFilePath(string track, string carClass)
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DouzeAssistance", "bestlaps");
            System.IO.Directory.CreateDirectory(dir);
            string key  = $"{track}_{carClass}";
            string safe = new string(key.Select(c =>
                System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
            return System.IO.Path.Combine(dir, safe + ".json");
        }

        private void TryLoadAllTimeBest(string track, string carClass)
        {
            _allTimeBestTrace = null;
            string path = BestLapFilePath(track, carClass);
            if (!System.IO.File.Exists(path)) return;
            try
            {
                var json = System.IO.File.ReadAllText(path);
                var dto  = Newtonsoft.Json.JsonConvert.DeserializeObject<BestLapDto>(json);
                if (dto?.Points == null || dto.Points.Count == 0) return;

                _allTimeBestTrace = new LapTrace
                {
                    LapNumber = -1,
                    LapTime   = dto.LapTime,
                    Compound  = "ALL-TIME BEST",
                    Points    = dto.Points.Select(p => new TelemetryPoint
                    {
                        TrackPos = p[0], Speed    = p[1],
                        Throttle = p[2], Brake    = p[3],
                        Gear     = (int)p[4], RPM = p[5],
                        Steering = p[6], Elapsed  = p[7]
                    }).ToList()
                };
            }
            catch { /* fichier corrompu : on ignore */ }
        }

        private void SaveAllTimeBest(string track, string carClass)
        {
            if (_allTimeBestTrace == null) return;
            try
            {
                var dto = new BestLapDto
                {
                    TrackName = track,
                    CarClass  = carClass,
                    LapTime   = _allTimeBestTrace.LapTime,
                    Points    = _allTimeBestTrace.Points.Select(p => new[]
                    {
                        p.TrackPos, p.Speed, p.Throttle, p.Brake,
                        (double)p.Gear, p.RPM, p.Steering, p.Elapsed
                    }).ToList()
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(dto);
                System.IO.File.WriteAllText(BestLapFilePath(track, carClass), json);
            }
            catch { /* silencieux */ }
        }

        private static string OpponentBestFilePath(string track, string carClass)
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DouzeAssistance", "bestlaps");
            System.IO.Directory.CreateDirectory(dir);
            string key  = $"opp_{track}_{carClass}";
            string safe = new string(key.Select(c =>
                System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
            return System.IO.Path.Combine(dir, safe + ".json");
        }

        private void TryLoadOpponentBest(string track, string carClass)
        {
            string path = OpponentBestFilePath(track, carClass);
            if (!System.IO.File.Exists(path)) return;
            try
            {
                var json = System.IO.File.ReadAllText(path);
                var dto  = Newtonsoft.Json.JsonConvert.DeserializeObject<BestLapDto>(json);
                if (dto?.Points == null || dto.Points.Count == 0) return;
                if (dto.LapTime >= _opponentBestTime) return; // session déjà meilleure

                _opponentBestTime  = dto.LapTime;
                _opponentBestTrace = new LapTrace
                {
                    LapNumber = -2,
                    LapTime   = dto.LapTime,
                    Compound  = string.IsNullOrEmpty(dto.DriverName) ? "ADVERSAIRE" : dto.DriverName,
                    Points    = dto.Points.Select(p => new TelemetryPoint
                    {
                        TrackPos = p[0], Speed = p[1]
                    }).ToList()
                };
            }
            catch { /* fichier corrompu : on ignore */ }
        }

        private void SaveOpponentBest(string track, string carClass)
        {
            if (_opponentBestTrace == null) return;
            try
            {
                var dto = new BestLapDto
                {
                    TrackName  = track,
                    CarClass   = carClass,
                    DriverName = _opponentBestTrace.Compound,
                    LapTime    = _opponentBestTrace.LapTime,
                    Points     = _opponentBestTrace.Points.Select(p =>
                        new[] { p.TrackPos, p.Speed }).ToList()
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(dto);
                System.IO.File.WriteAllText(OpponentBestFilePath(track, carClass), json);
            }
            catch { /* silencieux */ }
        }

        private class BestLapDto
        {
            public string        TrackName  { get; set; } = "";
            public string        CarClass   { get; set; } = "";
            public string        DriverName { get; set; } = "";
            public double        LapTime    { get; set; }
            public List<double[]> Points    { get; set; } = new();
        }

        // ====================================================================
        // DAMAGE DATA
        // ====================================================================

        public DamageData GetDamageData()
        {
            if (!_reader.IsConnected) return new DamageData();
            var pt = _reader.GetPlayerTelemetry();
            if (pt == null) return new DamageData();

            var tel = pt.Value;
            var ext = _reader.Extended;

            double[] dents = new double[8];
            if (tel.mDentSeverity != null)
                for (int i = 0; i < Math.Min(8, tel.mDentSeverity.Length); i++)
                    dents[i] = tel.mDentSeverity[i];

            // Get tracked damage from extended buffer
            double maxImpact = 0, accumImpact = 0;
            if (ext.mTrackedDamages != null)
            {
                int idx = tel.mID % rF2SharedMemory.rFactor2Constants.MAX_MAPPED_IDS;
                if (idx >= 0 && idx < ext.mTrackedDamages.Length)
                {
                    maxImpact = ext.mTrackedDamages[idx].mMaxImpactMagnitude;
                    accumImpact = ext.mTrackedDamages[idx].mAccumulatedImpactMagnitude;
                }
            }

            // Estimate repair time from dent severity
            // Each dent point ≈ 15-30s of repair in LMU
            // Detached parts add significant time
            double totalDentScore = 0;
            for (int i = 0; i < dents.Length; i++)
                totalDentScore += dents[i];
            double repairTime = totalDentScore * 20; // ~20s per dent severity point
            if (tel.mDetached != 0) repairTime += 60; // detached parts = +60s
            if (tel.mOverheating != 0) repairTime += 15;

            return new DamageData
            {
                DentSeverity = dents,
                MaxImpactMagnitude = maxImpact,
                AccumulatedImpact = accumImpact,
                LastImpactMagnitude = tel.mLastImpactMagnitude,
                AnyDetached = tel.mDetached != 0,
                Overheating = tel.mOverheating != 0,
                EstimatedRepairTime = repairTime
            };
        }

        // ====================================================================
        // G-FORCE DATA
        // ====================================================================

        public GForceData GetGForceData()
        {
            if (!_reader.IsConnected) return new GForceData();
            var pt = _reader.GetPlayerTelemetry();
            if (pt == null) return new GForceData();

            var tel = pt.Value;
            // mLocalAccel is in m/s², divide by 9.81 to get G
            const double G = 9.81;
            double lat = tel.mLocalAccel.x / G;
            double lon = tel.mLocalAccel.z / G;
            double vert = tel.mLocalAccel.y / G;

            return new GForceData
            {
                Lateral = lat,
                Longitudinal = lon,
                Vertical = vert,
                Combined = Math.Sqrt(lat * lat + lon * lon)
            };
        }

        // ====================================================================
        // PIT STRATEGY DATA
        // ====================================================================

        private int _tireChangeLap;

        public PitStrategyData GetPitStrategyData()
        {
            if (!_reader.IsConnected) return new PitStrategyData();
            var fuel = GetFuelData();
            var tires = GetTireData();
            var ps = _reader.GetPlayerScoring();
            if (ps == null) return new PitStrategyData();

            var scr = ps.Value;

            // Tire wear
            double avgWear = 0;
            if (tires.Length == 4)
                avgWear = (tires[0].Wear + tires[1].Wear + tires[2].Wear + tires[3].Wear) / 4.0;

            int lapsOnTires = scr.mTotalLaps - _tireChangeLap;
            double wearPerLap = lapsOnTires > 0 ? (1.0 - avgWear) / lapsOnTires : 0;
            int tireLapsLeft = wearPerLap > 0.001 ? Math.Min(999, (int)(avgWear / wearPerLap)) : 999;

            return new PitStrategyData
            {
                CurrentFuel = fuel.CurrentFuel,
                FuelPerLap = fuel.FuelPerLap,
                FuelToAdd = fuel.FuelToAdd,
                FuelAutonomy = fuel.FuelAutonomy,
                CurrentEnergy = fuel.CurrentEnergy,
                EnergyPerLap = fuel.EnergyPerLap,
                EnergyAutonomy = fuel.EnergyAutonomy,
                RealAutonomy = fuel.RealAutonomy,
                Limiter = fuel.Limiter,
                RaceLapsRemaining = fuel.RaceLapsRemaining,
                MaxStintLaps = fuel.MaxStintLaps,
                WindowState = fuel.WindowState,
                WindowClose = fuel.WindowClose,
                WindowOpen = fuel.WindowOpen,
                LapsOnTires = lapsOnTires,
                TireWearAvg = avgWear,
                TireLapsLeft = tireLapsLeft,
                TireCompound = tires.Length > 0 ? tires[0].Compound : "",
                StopsRemaining = fuel.StopsRequired,
                ValidFuelSamples = fuel.ValidFuelSamples,
                ValidEnergySamples = fuel.ValidEnergySamples
            };
        }

        // ====================================================================
        // TRACK NAME (for profiles)
        // ====================================================================

        public string GetTrackName()
        {
            if (!_reader.IsConnected) return "";
            return rF2Helper.Str(_reader.ScoringInfo.mTrackName);
        }

        // ====================================================================
        // TRACK MAP — enregistrement du tracé via tous les véhicules
        // ====================================================================

        private string  _trackRecordingName = "";
        private bool    _trackRecorded;
        // Clé = bucket lapDist (tous les 10 m) → position monde ordonnée
        private readonly SortedDictionary<int, (float X, float Z)> _trackBuckets = new();

        public TrackMapData GetTrackMapData()
        {
            var vehicles = GetAllVehicles();
            var orderedPoints = _trackBuckets.Values.ToList();

            var result = new TrackMapData
            {
                TrackPoints   = orderedPoints,
                Vehicles      = vehicles,
                TrackRecorded = _trackRecorded,
                PointCount    = _trackBuckets.Count
            };

            if (!_reader.IsConnected) return result;

            string trackName = rF2Helper.Str(_reader.ScoringInfo.mTrackName);
            result.TrackName = trackName;

            // ── Changement de circuit → réinitialiser et tenter de charger ──
            if (trackName != _trackRecordingName && !string.IsNullOrEmpty(trackName))
            {
                _trackRecordingName = trackName;
                _trackBuckets.Clear();
                _trackRecorded = false;

                TryLoadTrack(trackName);
                result.TrackRecorded = _trackRecorded;
                result.PointCount    = _trackBuckets.Count;
                result.TrackPoints   = _trackBuckets.Values.ToList();
                return result;
            }

            // ── Tracé déjà enregistré : pas d'échantillonnage ───────────────
            if (_trackRecorded) return result;

            // ── Échantillonnage de TOUS les véhicules ────────────────────────
            double trackLen = _reader.ScoringInfo.mLapDist;
            if (trackLen <= 0) return result;

            // Nombre de buckets attendus pour couvrir le circuit entier (1 bucket = 10 m)
            int expectedBuckets = Math.Max(50, (int)(trackLen / 10));

            foreach (var v in vehicles)
            {
                if (v.InPits) continue;
                double lapDist = v.LapDistance;
                if (lapDist < 0) continue;

                int bucket = (int)(lapDist / 10.0);
                if (!_trackBuckets.ContainsKey(bucket))
                    _trackBuckets[bucket] = ((float)v.PosX, (float)v.PosZ);
            }

            // ── Sauvegarde quand ≥ 80 % du circuit est couvert ──────────────
            if (_trackBuckets.Count >= expectedBuckets * 0.8)
            {
                SaveTrack(trackName);
                _trackRecorded = true;
            }

            result.PointCount    = _trackBuckets.Count;
            result.TrackRecorded = _trackRecorded;
            result.TrackPoints   = _trackBuckets.Values.ToList();
            return result;
        }

        private static string TrackFilePath(string trackName)
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DouzeAssistance", "tracks");
            System.IO.Directory.CreateDirectory(dir);
            string safe = new string(trackName.Select(c =>
                System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
            return System.IO.Path.Combine(dir, safe + ".json");
        }

        private void TryLoadTrack(string trackName)
        {
            string path = TrackFilePath(trackName);
            if (!System.IO.File.Exists(path)) return;
            try
            {
                var json = System.IO.File.ReadAllText(path);
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<TrackFileDto>(json);
                if (data?.Points != null && data.Points.Count > 0)
                {
                    // Les points chargés sont déjà ordonnés — on les réinsère avec des clés séquentielles
                    for (int i = 0; i < data.Points.Count; i++)
                        _trackBuckets[i] = ((float)data.Points[i][0], (float)data.Points[i][1]);
                    _trackRecorded = true;
                }
            }
            catch { /* fichier corrompu : on ignore */ }
        }

        private void SaveTrack(string trackName)
        {
            try
            {
                var dto = new TrackFileDto
                {
                    TrackName = trackName,
                    // Les valeurs sont déjà triées par clé (lapDist bucket)
                    Points    = _trackBuckets.Values.Select(p => new[] { (double)p.X, (double)p.Z }).ToList()
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(dto);
                System.IO.File.WriteAllText(TrackFilePath(trackName), json);
            }
            catch { /* silencieux */ }
        }

        private class TrackFileDto
        {
            public string TrackName { get; set; } = "";
            public List<double[]> Points { get; set; } = new();
        }
    }
}
