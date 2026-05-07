using System.Windows.Media;

namespace LMUOverlay.Helpers
{
    /// <summary>
    /// Cache de SolidColorBrush par couleur.
    /// Évite de recréer des brushes à chaque tick dans UpdateData().
    /// Tous les brushes sont Frozen → thread-safe et optimisés par WPF.
    /// </summary>
    internal static class BrushCache
    {
        private static readonly Dictionary<uint, SolidColorBrush> _cache = new();

        public static SolidColorBrush Get(Color c)
        {
            uint key = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            if (!_cache.TryGetValue(key, out var brush))
            {
                brush = new SolidColorBrush(c);
                brush.Freeze();
                _cache[key] = brush;
            }
            return brush;
        }

        public static SolidColorBrush Get(byte r, byte g, byte b) =>
            Get(Color.FromRgb(r, g, b));

        public static SolidColorBrush Get(byte a, byte r, byte g, byte b) =>
            Get(Color.FromArgb(a, r, g, b));
    }
}
