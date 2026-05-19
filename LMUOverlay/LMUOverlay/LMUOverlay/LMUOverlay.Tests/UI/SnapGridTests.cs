using Xunit;
using LMUOverlay.Helpers;

namespace LMUOverlay.Tests.UI
{
    // TODO: implement SnapGridHelper.Snap in 02-02

    /// <summary>
    /// Tests for snap-to-grid math (UI-01).
    /// These tests are RED until Plan 02-02 creates SnapGridHelper.
    /// </summary>
    [Trait("Category", "UI")]
    public class SnapGridTests
    {
        [Fact(DisplayName = "Snap: value already on grid boundary returns unchanged")]
        public void Snap_ValueOnGrid_ReturnsUnchanged()
        {
            // 0 is exactly on the 10px grid
            double result = SnapGridHelper.Snap(0, 10);
            Assert.Equal(0, result);
        }

        [Fact(DisplayName = "Snap: 14.9 rounds down to nearest grid (10)")]
        public void Snap_ValueBelowMidpoint_RoundsDown()
        {
            // 14.9 is below the midpoint between 10 and 20, should snap to 10
            double result = SnapGridHelper.Snap(14.9, 10);
            Assert.Equal(10, result);
        }

        [Fact(DisplayName = "Snap: 15.0 rounds up to next grid (20) at exact midpoint")]
        public void Snap_ValueAtMidpoint_RoundsUp()
        {
            // 15.0 is at the midpoint — rounds up to 20
            double result = SnapGridHelper.Snap(15.0, 10);
            Assert.Equal(20, result);
        }

        [Fact(DisplayName = "Snap: 23.4 rounds down to nearest grid (20)")]
        public void Snap_ValueAboveGridBelowMidpoint_RoundsDown()
        {
            // 23.4 is between 20 and 30, below midpoint (25), should snap to 20
            double result = SnapGridHelper.Snap(23.4, 10);
            Assert.Equal(20, result);
        }

        [Fact(DisplayName = "Snap: negative value -7.0 snaps to -10")]
        public void Snap_NegativeValue_SnapsToCorrectGrid()
        {
            // -7.0 is between -10 and 0; rounds to -10 (farther from zero)
            double result = SnapGridHelper.Snap(-7.0, 10);
            Assert.Equal(-10, result);
        }

        [Fact(DisplayName = "Snap: large value 1920.0 already on grid returns unchanged")]
        public void Snap_LargeValueOnGrid_ReturnsUnchanged()
        {
            // 1920 is exactly on the 10px grid
            double result = SnapGridHelper.Snap(1920.0, 10);
            Assert.Equal(1920, result);
        }

        [Fact(DisplayName = "Snap: raw delta accumulation 1.2+1.8+2.5=5.5 snaps to 10")]
        public void Snap_RawDeltaAccumulation_SnapsToExpectedGrid()
        {
            // Simulate 3 MouseMove deltas: raw accumulates 1.2 + 1.8 + 2.5 = 5.5px
            // Snap(5.5, 10) should round to 10 (midpoint is 5.0, 5.5 > 5.0 so round up)
            double rawAccumulated = 1.2 + 1.8 + 2.5; // = 5.5
            double result = SnapGridHelper.Snap(rawAccumulated, 10);
            Assert.Equal(10, result);
        }
    }
}
