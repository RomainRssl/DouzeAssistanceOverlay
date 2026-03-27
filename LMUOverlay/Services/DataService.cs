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

        // Energy tracking
        private double _energyAtStartOfLap = -1;

        // Lap detection
        private int _trackedLap = -1;
        private bool _wasInPitsThisLap;
        private bool _wasLapInvalidThisLap;

        // Session best sector tracking (for purple indicators)
        private double _sessionBestS1 = double.MaxValue;
        private double _sessionBestS2 = double.MaxValue; // S2 alone, not cumulative
        private double _sessionBestS3 = double.MaxValue;
        // Per-vehicle personal best sectors (key = vehicle ID)
        private readonly Dictionary<int, (double s1, double s2, double s3)> _personalBestSectors = new();

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
                string tireCompound = "";
                double fuel = 0;
                if (telemetry.mVehicles != null)
                {
                    int tCount = Math.Min(telemetry.mNumVehicles, telemetry.mVehicles.Length);
                    for (int t = 0; t < tCount; t++)
                    {
                        if (telemetry.mVehicles[t].mID == v.mID)
                        {
                            tireCompound = rF2Helper.Str(telemetry.mVehicles[t].mFrontTireCompoundName);
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
                    PitState = v.mPitState,
                    LapsBehindLeader = v.mLapsBehindLeader,
                    TireCompound = tireCompound,
                    Fuel = fuel,

                    // Sector status (computed below)
                    CurrentSector = v.mSector,
                    BestSector1 = v.mBestSector1,
                    BestSector2 = v.mBestSector2,
                });
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

            var result = new List<(VehicleData, double, double)>();
            foreach (var v in all)
            {
                if (v.IsPlayer) continue;
                double dx = v.PosX - player.PosX, dz = v.PosZ - player.PosZ;
                double dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist > range) continue;

                double cos = Math.Cos(-player.YawAngle), sin = Math.Sin(-player.YawAngle);
                double relX = -(dx * cos - dz * sin); // negate: rF2 X axis is inverted
                double relZ = -(dx * sin + dz * cos); // negate: rF2 Z axis is inverted
                result.Add((v, relX, relZ));
            }
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
            double E_actuel = tel.mBatteryChargeFraction * 100;
            double E_max = 100.0;
            double sessionLeft = info.mEndET - info.mCurrentET;

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

                // ENERGY sample
                if (_energyAtStartOfLap >= 0 && isValid)
                {
                    double energyUsed = _energyAtStartOfLap - E_actuel;
                    if (energyUsed > 0.1 && energyUsed < 80.0)
                    {
                        _energySamples.Add(energyUsed);
                        if (_energySamples.Count > MAX_SAMPLES) _energySamples.RemoveAt(0);
                    }
                }

                _fuelAtStartOfLap = V_actuel;
                _energyAtStartOfLap = E_actuel;
                _wasInPitsThisLap = scr.mInPits != 0;
                _wasLapInvalidThisLap = false;
            }

            if (_trackedLap < 0)
            {
                _fuelAtStartOfLap = V_actuel;
                _energyAtStartOfLap = E_actuel;
            }
            _trackedLap = currentLap;

            // Averages
            double C_fuel = _fuelSamples.Count > 0 ? _fuelSamples.Average() : 0;
            double C_energy = _energySamples.Count > 0 ? _energySamples.Average() : 0;

            // Autonomies
            double L_fuel = C_fuel > 0.1 ? V_actuel / C_fuel : 0;
            double L_energy = C_energy > 0.1 ? E_actuel / C_energy : 0;

            // Real autonomy = GOULOT
            double L_real;
            LimitingFactor limiter;
            bool hasFuel = _fuelSamples.Count >= 2;
            bool hasEnergy = _energySamples.Count >= 2;

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

            // Fuel to add
            double V_marge = SAFETY_MARGIN_LAPS * C_fuel;
            double fuelToEnd = C_fuel > 0.1 ? raceLapsLeft * C_fuel : 0;
            double fuelToAdd = C_fuel > 0.1
                ? Math.Max(0, (raceLapsLeft * C_fuel) - V_actuel + V_marge) : 0;
            double deficit = fuelToEnd > 0 ? Math.Max(0, fuelToEnd - V_actuel) : 0;
            int stopsRequired = (V_max > 0 && deficit > 0)
                ? Math.Min(99, (int)Math.Ceiling(deficit / V_max)) : 0;

            // Pit window (based on REAL autonomy)
            int maxStintFuel = (C_fuel > 0.1 && V_max > 0) ? (int)Math.Floor(V_max / C_fuel) : 999;
            int maxStintEnergy = (C_energy > 0.1) ? (int)Math.Floor(E_max / C_energy) : 999;
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
                CurrentEnergy = E_actuel, EnergyCapacity = E_max,
                EnergyPerLap = C_energy, EnergyAutonomy = L_energy,
                RealAutonomy = L_real, Limiter = limiter,
                RaceLapsRemaining = raceLapsLeft,
                FuelToAdd = Math.Min(fuelToAdd, V_max),
                FuelToEnd = fuelToEnd, FuelDeficit = deficit,
                StopsRequired = stopsRequired, TimeRemaining = sessionLeft,
                MaxStintLaps = maxStintLaps,
                WindowClose = windowClose, WindowOpen = windowOpen,
                WindowState = windowState,
                ValidFuelSamples = _fuelSamples.Count,
                ValidEnergySamples = _energySamples.Count
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
            if (pt == null) return new InputData();

            var tel = pt.Value;

            // Speed from local velocity (mLocalVel.z is forward in vehicle frame)
            double speed = Math.Sqrt(
                tel.mLocalVel.x * tel.mLocalVel.x +
                tel.mLocalVel.y * tel.mLocalVel.y +
                tel.mLocalVel.z * tel.mLocalVel.z) * 3.6; // m/s → km/h

            return new InputData
            {
                Throttle = tel.mUnfilteredThrottle,
                Brake = tel.mUnfilteredBrake,
                Steering = tel.mUnfilteredSteering,
                Clutch = tel.mUnfilteredClutch,
                Gear = tel.mGear,
                RPM = tel.mEngineRPM,
                MaxRPM = tel.mEngineMaxRPM,
                Speed = speed
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
                PitLimiter = scr.mInPits != 0 || scr.mPitState >= 2,
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

                    _lapHistory.Add(new LapRecord
                    {
                        LapNumber = _deltaLastLap,
                        LapTime = scr.mLastLapTime,
                        Sector1 = scr.mLastSector1,
                        Sector2 = scr.mLastSector2 > 0 ? scr.mLastSector2 - scr.mLastSector1 : 0,
                        Sector3 = scr.mLastLapTime - scr.mLastSector2,
                        FuelUsed = _fuelSamples.Count > 0 ? _fuelSamples[^1] : 0,
                        FuelRemaining = tel.mFuel,
                        TireCompound = rF2Helper.Str(tel.mFrontTireCompoundName),
                        TrackTemp = _reader.ScoringInfo.mTrackTemp
                    });
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

        // ====================================================================
        // TELEMETRY TRACES
        // ====================================================================

        private readonly List<LapTrace>       _lapTraces    = new();
        private readonly List<TelemetryPoint> _currentTrace = new();
        private double _lastTraceDist = -1;
        private const int MAX_TRACES = 30;
        private const double TRACE_INTERVAL_M = 5.0; // sample every 5m

        public void UpdateTelemetryTrace()
        {
            if (!_reader.IsConnected) return;
            var ps = _reader.GetPlayerScoring();
            var pt = _reader.GetPlayerTelemetry();
            if (ps == null || pt == null) return;

            var scr = ps.Value;
            var tel = pt.Value;
            var info = _reader.ScoringInfo;

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

        public List<LapTrace> GetLapTraces() => new(_lapTraces);

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
        // TRACK LIMITS
        // ====================================================================

        private int _lastPenaltyCount;
        private bool _wasOffTrack;
        private int _offTrackCount;
        private double _lastOffTrackET;
        private string _prevLSIRules = "";
        private string _prevStatusMsg = "";
        private readonly List<TrackLimitEvent> _trackLimitEvents = new();
        private int _trackLimitWarnings;

        private static readonly string[] OffTrackSurfaces = { "grass", "dirt", "gravel", "sand", "wall" };

        public TrackLimitsData GetTrackLimitsData()
        {
            if (!_reader.IsConnected) return new TrackLimitsData();
            var pt = _reader.GetPlayerTelemetry();
            var ps = _reader.GetPlayerScoring();
            if (pt == null || ps == null) return new TrackLimitsData();

            var tel = pt.Value;
            var scr = ps.Value;
            var ext = _reader.Extended;
            var info = _reader.ScoringInfo;

            // Wheel surface detection
            bool[] wheelOff = new bool[4];
            string[] wheelSurf = new string[4];
            bool anyOff = false;

            if (tel.mWheels != null && tel.mWheels.Length >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    var w = tel.mWheels[i];
                    string terrain = rF2Helper.Str(w.mTerrainName).ToLowerInvariant();
                    byte surfType = w.mSurfaceType;

                    // Off-track: surface type 2=grass, 3=dirt, 4=gravel, or terrain name
                    bool isOff = surfType >= 2 && surfType <= 4;
                    if (!isOff && !string.IsNullOrEmpty(terrain))
                        isOff = OffTrackSurfaces.Any(s => terrain.Contains(s));

                    wheelOff[i] = isOff;
                    wheelSurf[i] = surfType switch
                    {
                        0 => "Sec",
                        1 => "Mouillé",
                        2 => "Herbe",
                        3 => "Terre",
                        4 => "Gravier",
                        5 => "Vibreur",
                        6 => "Spécial",
                        _ => $"?{surfType}"
                    };
                    if (isOff) anyOff = true;
                }
            }

            // Detect new off-track event
            if (anyOff && !_wasOffTrack)
            {
                _offTrackCount++;
                _lastOffTrackET = info.mCurrentET;
                _trackLimitWarnings++;

                _trackLimitEvents.Add(new TrackLimitEvent
                {
                    LapNumber = scr.mTotalLaps,
                    ElapsedTime = info.mCurrentET,
                    Type = "Hors-piste",
                    Detail = string.Join("+", wheelOff.Select((o, i) => o ? new[] { "AG", "AD", "RG", "RD" }[i] : null).Where(s => s != null))
                });
                if (_trackLimitEvents.Count > 20) _trackLimitEvents.RemoveAt(0);
            }
            _wasOffTrack = anyOff;

            // Detect penalty changes
            int penalties = scr.mNumPenalties;
            if (penalties > _lastPenaltyCount && _lastPenaltyCount >= 0)
            {
                _trackLimitEvents.Add(new TrackLimitEvent
                {
                    LapNumber = scr.mTotalLaps,
                    ElapsedTime = info.mCurrentET,
                    Type = "Pénalité",
                    Detail = $"Total: {penalties}"
                });
                if (_trackLimitEvents.Count > 20) _trackLimitEvents.RemoveAt(0);
            }
            _lastPenaltyCount = penalties;

            // Read LSI rules message (may contain track limit info)
            string lsiMsg = ext.mLSIRulesInstructionMessage != null
                ? rF2Helper.Str(ext.mLSIRulesInstructionMessage) : "";
            if (!string.IsNullOrEmpty(lsiMsg) && lsiMsg != _prevLSIRules)
            {
                _prevLSIRules = lsiMsg;
                _trackLimitEvents.Add(new TrackLimitEvent
                {
                    LapNumber = scr.mTotalLaps,
                    ElapsedTime = info.mCurrentET,
                    Type = "Message",
                    Detail = lsiMsg
                });
                if (_trackLimitEvents.Count > 20) _trackLimitEvents.RemoveAt(0);
            }

            // Status message
            string statusMsg = ext.mStatusMessage != null
                ? rF2Helper.Str(ext.mStatusMessage) : "";

            return new TrackLimitsData
            {
                PenaltyCount = penalties,
                TrackLimitWarnings = _trackLimitWarnings,
                WheelOffTrack = wheelOff,
                WheelSurface = wheelSurf,
                OffTrackCount = _offTrackCount,
                LastOffTrackTime = _lastOffTrackET,
                LastLSIMessage = lsiMsg,
                StatusMessage = statusMsg,
                IsOffTrackNow = anyOff,
                RecentEvents = new List<TrackLimitEvent>(_trackLimitEvents)
            };
        }
    }
}
