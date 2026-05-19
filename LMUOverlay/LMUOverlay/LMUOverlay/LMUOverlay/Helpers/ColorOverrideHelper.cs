using LMUOverlay.Models;

namespace LMUOverlay.Helpers
{
    /// <summary>
    /// Static helper for reading and writing per-overlay color overrides stored
    /// in <see cref="OverlaySettings.CustomOptions"/>.
    /// Keys: "ColorBg", "ColorText", "ColorAccent" — values are hex strings (#RRGGBB or #AARRGGBB).
    /// </summary>
    public static class ColorOverrideHelper
    {
        /// <summary>
        /// Returns the stored hex color string for <paramref name="key"/>, or <c>null</c> if absent.
        /// </summary>
        public static string? Get(OverlaySettings settings, string key)
        {
            if (settings.CustomOptions.TryGetValue(key, out var v) && v is string hex)
                return hex;
            return null;
        }

        /// <summary>
        /// Stores <paramref name="hexValue"/> under <paramref name="key"/> in CustomOptions,
        /// overwriting any previous value.
        /// </summary>
        public static void Set(OverlaySettings settings, string key, string hexValue)
            => settings.CustomOptions[key] = hexValue;
    }
}
