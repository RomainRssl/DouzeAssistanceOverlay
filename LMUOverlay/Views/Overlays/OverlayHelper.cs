using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Shared UI helpers and theme colors for all overlays.
    /// Dark teal racing theme.
    /// </summary>
    public static class OverlayHelper
    {
        // ================================================================
        // THEME COLORS
        // ================================================================

        // Backgrounds
        public static readonly Color BgDark = Color.FromRgb(18, 32, 32);
        public static readonly Color BgCell = Color.FromRgb(24, 52, 55);
        public static readonly Color BgCellAlt = Color.FromRgb(30, 60, 65);
        public static readonly Color BgAccent = Color.FromRgb(0, 105, 92);
        public static readonly Color BgGreen = Color.FromRgb(27, 94, 32);
        public static readonly Color BgRed = Color.FromRgb(140, 25, 25);
        public static readonly Color BgBlue = Color.FromRgb(20, 50, 80);
        public static readonly Color BgOrange = Color.FromRgb(120, 70, 10);

        // Text
        public static readonly Color TextPrimary = Color.FromRgb(230, 240, 240);
        public static readonly Color TextSecondary = Color.FromRgb(150, 180, 180);
        public static readonly Color TextMuted = Color.FromRgb(80, 110, 110);
        public static readonly Color TextValue = Colors.White;
        public static readonly Color TextGear = Color.FromRgb(255, 235, 59);

        // Accents
        public static readonly Color AccGreen = Color.FromRgb(76, 217, 100);
        public static readonly Color AccRed = Color.FromRgb(255, 59, 48);
        public static readonly Color AccYellow = Color.FromRgb(255, 204, 0);
        public static readonly Color AccBlue = Color.FromRgb(88, 166, 255);
        public static readonly Color AccPurple = Color.FromRgb(168, 85, 247);

        // Border
        public static readonly Color BorderColor = Color.FromRgb(36, 68, 68);

        // ================================================================
        // BORDER / CONTAINERS
        // ================================================================

        public static Border MakeBorder() => new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Background = new SolidColorBrush(Color.FromArgb(235, BgDark.R, BgDark.G, BgDark.B)),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(1)
        };

        public static Border MakeCell(Color? bg = null) => new Border
        {
            Background = new SolidColorBrush(bg ?? BgCell),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(1)
        };

        // ================================================================
        // TEXT HELPERS
        // ================================================================

        public static TextBlock MakeTitle(string text) => new TextBlock
        {
            Text = text, FontSize = 10, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(TextSecondary),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };

        public static TextBlock MakeLabel(string text) => new TextBlock
        {
            Text = text, FontSize = 7, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(TextSecondary),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        public static TextBlock MakeValue(double fontSize = 16) => new TextBlock
        {
            Text = "0", FontSize = fontSize, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(TextValue),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // ================================================================
        // NAME / TIME / CLASS HELPERS
        // ================================================================

        public static string FormatName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";
            var parts = name.Split(' ');
            return parts.Length >= 2 ? $"{parts[0][0]}. {parts[^1]}" : name;
        }

        public static string FormatTime(double t)
        {
            if (t <= 0) return "--:--.---";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        public static Color GetClassColor(string vehicleClass)
        {
            string c = (vehicleClass ?? "").ToUpperInvariant();
            if (c.Contains("HYPERCAR") || c.Contains("LMH") || c.Contains("LMDH"))
                return Color.FromRgb(255, 24, 1);
            if (c.Contains("LMP2")) return Color.FromRgb(0, 144, 255);
            if (c.Contains("GTE") || c.Contains("LMGT")) return Color.FromRgb(0, 179, 65);
            if (c.Contains("GT3")) return Color.FromRgb(255, 183, 0);
            return Color.FromRgb(180, 180, 180);
        }

        // ================================================================
        // BRUSH SHORTHAND
        // ================================================================

        public static SolidColorBrush Br(Color c) => new(c);
        public static SolidColorBrush Br(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
