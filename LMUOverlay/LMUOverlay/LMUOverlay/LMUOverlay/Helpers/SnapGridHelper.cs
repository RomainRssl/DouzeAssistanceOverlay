namespace LMUOverlay.Helpers
{
    /// <summary>
    /// Pure static snap-to-grid math helper for overlay drag positioning.
    /// SnapGridHelper.Snap(rawValue, grid) rounds to the nearest grid boundary.
    /// </summary>
    public static class SnapGridHelper
    {
        public const double DefaultGrid = 10.0;

        /// <summary>
        /// Snaps <paramref name="rawValue"/> to the nearest multiple of <paramref name="grid"/>.
        /// Uses Math.Round (midpoint rounds away from zero), so 15.0 → 20 with grid=10.
        /// </summary>
        public static double Snap(double rawValue, double grid = DefaultGrid)
            => Math.Round(rawValue / grid) * grid;
    }
}
