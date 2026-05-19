using LMUOverlay.Services;
using Xunit;

namespace LMUOverlay.Tests.FuelStrategy
{
    /// <summary>
    /// Tests for FuelStrategyCalculator — covers FUEL-01 (leader laps) and FUEL-03 (safety margin).
    /// These tests are RED until Plan 02 wires DataService to delegate to FuelStrategyCalculator.
    /// </summary>
    [Trait("Category", "Fuel")]
    public class FuelStrategyCalculatorTests
    {
        // ── FUEL-01: Multi-class leader laps ─────────────────────────────────

        [Fact(DisplayName = "MultiClass: GT3 player lap 3, Hypercar leader lap 5 → lapsLeft uses leader")]
        public void MultiClass_LeaderAheadOfPlayer_UsesLeaderLaps()
        {
            // Arrange: 10-lap race; global P1 (Hypercar) is on lap 5; GT3 player is on lap 3
            int maxLaps = 10;
            int leaderTotalLaps = 5;
            int playerTotalLaps = 3;

            // Act
            int lapsLeft = FuelStrategyCalculator.ComputeRaceLapsLeft(
                maxLaps, leaderTotalLaps, playerTotalLaps,
                sessionLeft: 0, lastLapTime: 0);

            // Assert: remaining = 10 - 5 = 5 (leader's view), NOT 10 - 3 = 7 (player's view)
            Assert.Equal(5, lapsLeft);
        }

        [Fact(DisplayName = "SingleClass: player IS leader → same result as old calculation")]
        public void SingleClass_PlayerIsLeader_SameResult()
        {
            // In single-class, player and leader have the same total laps
            int maxLaps = 20;
            int leaderTotalLaps = 12;
            int playerTotalLaps = 12;  // player is the leader

            int lapsLeft = FuelStrategyCalculator.ComputeRaceLapsLeft(
                maxLaps, leaderTotalLaps, playerTotalLaps,
                sessionLeft: 0, lastLapTime: 0);

            Assert.Equal(8, lapsLeft);  // 20 - 12
        }

        [Fact(DisplayName = "TimeBased: mMaxLaps==10000 falls back to sessionLeft/lapTime")]
        public void TimeBased_FallsBackToSessionTime()
        {
            // mMaxLaps=10000 signals a time-based race
            int maxLaps = 10000;
            int lapsLeft = FuelStrategyCalculator.ComputeRaceLapsLeft(
                maxLaps,
                leaderTotalLaps: 0,
                playerTotalLaps: 0,
                sessionLeft: 360,   // 6 minutes left
                lastLapTime: 120);  // 2-minute laps

            // 360 / 120 = 3 laps, ceiling(3.0) = 3
            Assert.Equal(3, lapsLeft);
        }

        // ── FUEL-03: Configurable safety margin ──────────────────────────────

        [Fact(DisplayName = "SafetyMargin: margin=0.5 reduces fuelToAdd vs margin=1.0")]
        public void SafetyMargin_HalfMargin_LowerFuelToAdd()
        {
            // 5 laps left, 3 kg/lap consumption, currently 10 kg in tank
            var (fuelToAdd_05, _) = FuelStrategyCalculator.ComputeFuelToAdd(
                raceLapsLeft: 5, currentFuel: 10, fuelPerLap: 3,
                fuelCapacity: 100, safetyMarginLaps: 0.5, validSamples: 3);

            var (fuelToAdd_10, _) = FuelStrategyCalculator.ComputeFuelToAdd(
                raceLapsLeft: 5, currentFuel: 10, fuelPerLap: 3,
                fuelCapacity: 100, safetyMarginLaps: 1.0, validSamples: 3);

            // margin=0.5: (5×3) - 10 + (0.5×3) = 5 + 1.5 = 6.5
            Assert.Equal(6.5, fuelToAdd_05, precision: 6);
            // margin=1.0: (5×3) - 10 + (1.0×3) = 5 + 3.0 = 8.0
            Assert.Equal(8.0, fuelToAdd_10, precision: 6);
        }

        [Fact(DisplayName = "WindowClose: margin reflected in pit window close lap")]
        public void WindowClose_MarginReflectedInWindow()
        {
            // 15 kg in tank, 3 kg/lap → real autonomy = 5 laps; margin=1 → windowClose = 4
            var (_, windowClose) = FuelStrategyCalculator.ComputeFuelToAdd(
                raceLapsLeft: 10, currentFuel: 15, fuelPerLap: 3,
                fuelCapacity: 100, safetyMarginLaps: 1.0, validSamples: 3);

            // windowClose = autonomy(5.0) - safetyMarginLaps(1.0) = 4.0
            Assert.Equal(4.0, windowClose, precision: 6);
        }
    }
}
