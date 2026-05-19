using LMUOverlay.Models;

namespace LMUOverlay.Services
{
    /// <summary>
    /// Pure calculation helpers extracted from DataService so they can be unit-tested
    /// without a SharedMemoryReader (Windows shared memory handle).
    ///
    /// DataService calls these static methods and passes in the values it reads from
    /// shared memory. No I/O, no side effects, no state.
    /// </summary>
    public static class FuelStrategyCalculator
    {
        /// <summary>
        /// Computes the number of race laps remaining based on the global race leader,
        /// not the player's own laps.
        /// </summary>
        /// <param name="maxLaps">info.mMaxLaps — 10000 means time-based race</param>
        /// <param name="leaderTotalLaps">mTotalLaps of the car with mPlace==1 (not DNF)</param>
        /// <param name="playerTotalLaps">mTotalLaps of the player (fallback when no leader found)</param>
        /// <param name="sessionLeft">info.mEndET - info.mCurrentET (seconds)</param>
        /// <param name="lastLapTime">scr.mLastLapTime (seconds); 0 when unavailable</param>
        /// <returns>Estimated race laps remaining; 0 when calculation is not possible</returns>
        public static int ComputeRaceLapsLeft(
            int maxLaps,
            int leaderTotalLaps,
            int playerTotalLaps,
            double sessionLeft,
            double lastLapTime)
        {
            // Lap-count race: use leader's completed laps (FUEL-01 fix)
            if (maxLaps > 0 && maxLaps < 10000)
                return Math.Max(0, maxLaps - leaderTotalLaps);

            // Time-based race: estimate from session time left and last lap time
            if (lastLapTime > 10 && sessionLeft > 0)
                return (int)Math.Ceiling(sessionLeft / lastLapTime);

            return 0;
        }

        /// <summary>
        /// Computes the fuel to add at the next pit stop and the pit window close lap.
        /// </summary>
        /// <param name="raceLapsLeft">From ComputeRaceLapsLeft()</param>
        /// <param name="currentFuel">tel.mFuel</param>
        /// <param name="fuelPerLap">Rolling average of valid lap consumptions (C_fuel)</param>
        /// <param name="fuelCapacity">tel.mFuelCapacity</param>
        /// <param name="safetyMarginLaps">From FuelStrategyConfig.SafetyMarginLaps (FUEL-03 fix)</param>
        /// <param name="validSamples">Number of valid lap samples in the rolling average</param>
        /// <returns>Tuple of (fuelToAdd, windowClose); both 0 when data is not ready</returns>
        public static (double FuelToAdd, double WindowClose) ComputeFuelToAdd(
            int raceLapsLeft,
            double currentFuel,
            double fuelPerLap,
            double fuelCapacity,
            double safetyMarginLaps,
            int validSamples)
        {
            bool dataReady = fuelPerLap > 0.1 && validSamples >= 2 && raceLapsLeft > 0;
            if (!dataReady) return (0, 0);

            double margin = safetyMarginLaps * fuelPerLap;
            double fuelToAdd = Math.Max(0, (raceLapsLeft * fuelPerLap) - currentFuel + margin);
            double realAutonomy = currentFuel / fuelPerLap;
            double windowClose = realAutonomy > 0 ? realAutonomy - safetyMarginLaps : 0;

            return (Math.Min(fuelToAdd, fuelCapacity), windowClose);
        }
    }
}
