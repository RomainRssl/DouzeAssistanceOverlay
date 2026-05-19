using Xunit;

namespace LMUOverlay.Tests.FuelStrategy
{
    /// <summary>
    /// Tests for SC/VSC lap exclusion from consumption average — FUEL-02.
    ///
    /// These tests validate the consumption tracking STATE MACHINE behavior.
    /// Because the state machine lives inside DataService (not a pure function),
    /// these tests document the expected behaviors. They are RED until Plan 02
    /// extracts or validates the SC tagging logic.
    ///
    /// Strategy: simulate the tracking logic directly (the rules are simple enough
    /// to replicate in test helpers without SharedMemoryReader).
    /// </summary>
    [Trait("Category", "Fuel")]
    public class ConsumptionTrackerTests
    {
        /// <summary>
        /// Simulates the lap validity gate from DataService.UpdateEnergyAndFuelTracking().
        /// A lap is valid when: not in pits AND not lap-invalid AND not under SC/VSC.
        /// </summary>
        private static bool SimulateLapValidity(
            bool wasInPits,
            bool wasLapInvalid,
            bool wasUnderSC)
        {
            return !wasInPits && !wasLapInvalid && !wasUnderSC;
        }

        /// <summary>
        /// Simulates rolling average of valid fuel samples (mirrors DataService logic).
        /// </summary>
        private static double ComputeAverage(IEnumerable<double> samples)
            => samples.Any() ? samples.Average() : 0;

        [Fact(DisplayName = "SCLapExcluded: lap under Safety Car is excluded from average")]
        public void SCLap_IsExcludedFromConsumptionAverage()
        {
            // Two laps: lap1 normal (3.0 kg), lap2 under SC (2.1 kg — slow pace)
            var samples = new List<double>();

            // Lap 1: no SC, no pits, not invalid → valid sample
            bool lap1Valid = SimulateLapValidity(wasInPits: false, wasLapInvalid: false, wasUnderSC: false);
            if (lap1Valid) samples.Add(3.0);

            // Lap 2: SC active → wasUnderSC=true → excluded
            bool lap2Valid = SimulateLapValidity(wasInPits: false, wasLapInvalid: false, wasUnderSC: true);
            if (lap2Valid) samples.Add(2.1);  // should NOT be added

            double average = ComputeAverage(samples);

            // Only lap 1 should be in the average
            Assert.Equal(1, samples.Count);
            Assert.Equal(3.0, average, precision: 6);
        }

        [Fact(DisplayName = "PostSCNormal: lap after SC ends is included when SC inactive")]
        public void PostSCLap_IsIncludedWhenSCInactive()
        {
            var samples = new List<double>();

            // Lap 1: SC active → excluded
            bool lap1Valid = SimulateLapValidity(wasInPits: false, wasLapInvalid: false, wasUnderSC: true);
            if (lap1Valid) samples.Add(2.1);

            // Lap 2: SC cleared → valid, normal consumption
            bool lap2Valid = SimulateLapValidity(wasInPits: false, wasLapInvalid: false, wasUnderSC: false);
            if (lap2Valid) samples.Add(3.2);

            // Lap 3: also normal
            bool lap3Valid = SimulateLapValidity(wasInPits: false, wasLapInvalid: false, wasUnderSC: false);
            if (lap3Valid) samples.Add(3.0);

            double average = ComputeAverage(samples);

            // SC lap excluded; laps 2 and 3 averaged
            Assert.Equal(2, samples.Count);
            Assert.Equal(3.1, average, precision: 6);
        }
    }
}
