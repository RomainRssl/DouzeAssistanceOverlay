using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Shared UI helpers and theme colors for all overlays.
    /// Dark grey theme — matches the main window palette.
    /// </summary>
    public static class OverlayHelper
    {
        // ================================================================
        // THEME COLORS  (mirror MainWindow palette)
        // ================================================================

        // Backgrounds
        public static readonly Color BgDark    = Color.FromRgb(0x1A, 0x1A, 0x1A); // main bg
        public static readonly Color BgCell    = Color.FromRgb(0x22, 0x22, 0x22); // header / card
        public static readonly Color BgCellAlt = Color.FromRgb(0x1E, 0x1E, 0x1E); // sidebar / alt row
        public static readonly Color BgAccent  = Color.FromRgb(0x2E, 0x5A, 0x2E); // active green btn
        public static readonly Color BgGreen   = Color.FromRgb(0x1B, 0x3A, 0x1B); // subtle green bg
        public static readonly Color BgRed     = Color.FromRgb(0x3A, 0x12, 0x12); // subtle red bg
        public static readonly Color BgBlue    = Color.FromRgb(0x12, 0x22, 0x38); // subtle blue bg
        public static readonly Color BgOrange  = Color.FromRgb(0x38, 0x22, 0x08); // subtle orange bg

        // Text
        public static readonly Color TextPrimary   = Color.FromRgb(0xDD, 0xDD, 0xDD); // #DDDDDD
        public static readonly Color TextSecondary = Color.FromRgb(0x88, 0x88, 0x88); // #888888
        public static readonly Color TextMuted     = Color.FromRgb(0x55, 0x55, 0x55); // #555555
        public static readonly Color TextValue     = Colors.White;
        public static readonly Color TextGear      = Color.FromRgb(0xFF, 0xEB, 0x3B); // yellow

        // Accents (unchanged — functional colors)
        public static readonly Color AccGreen  = Color.FromRgb(0x44, 0xD9, 0x64); // #44D964
        public static readonly Color AccRed    = Color.FromRgb(0xFF, 0x3B, 0x30); // #FF3B30
        public static readonly Color AccYellow = Color.FromRgb(0xFF, 0xCC, 0x00); // #FFCC00
        public static readonly Color AccBlue   = Color.FromRgb(0x58, 0xA6, 0xFF); // #58A6FF
        public static readonly Color AccPurple = Color.FromRgb(0xA8, 0x55, 0xF7); // #A855F7

        // Border
        public static readonly Color BorderColor = Color.FromRgb(0x3A, 0x3A, 0x3A); // #3A3A3A

        // Font families
        public static readonly FontFamily FontConsolas  = new("Consolas");
        public static readonly FontFamily FontSegoeUISB = new("Segoe UI Semibold");

        // ================================================================
        // BORDER / CONTAINERS
        // ================================================================

        public static Border MakeBorder() => new Border
        {
            CornerRadius    = new CornerRadius(6),
            Padding         = new Thickness(6),
            Background      = new SolidColorBrush(Color.FromArgb(240, BgDark.R, BgDark.G, BgDark.B)),
            BorderBrush     = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(1)
        };

        public static Border MakeCell(Color? bg = null) => new Border
        {
            Background      = new SolidColorBrush(bg ?? BgCell),
            CornerRadius    = new CornerRadius(3),
            Padding         = new Thickness(4, 2, 4, 2),
            Margin          = new Thickness(1)
        };

        /// <summary>Section header bar inside an overlay (mimics sidebar header).</summary>
        public static Border MakeSectionHeader(string text) => new Border
        {
            Background      = new SolidColorBrush(BgCell),
            BorderBrush     = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(6, 3, 6, 3),
            Child           = new TextBlock
            {
                Text        = text,
                FontSize    = 8,
                FontWeight  = FontWeights.SemiBold,
                FontFamily  = FontSegoeUISB,
                Foreground  = new SolidColorBrush(TextSecondary)
            }
        };

        // ================================================================
        // TEXT HELPERS
        // ================================================================

        public static TextBlock MakeTitle(string text) => new TextBlock
        {
            Text                = text,
            FontSize            = 9,
            FontWeight          = FontWeights.SemiBold,
            FontFamily          = FontSegoeUISB,
            Foreground          = new SolidColorBrush(TextSecondary),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 0, 0, 3)
        };

        public static TextBlock MakeLabel(string text) => new TextBlock
        {
            Text                = text,
            FontSize            = 7,
            FontWeight          = FontWeights.SemiBold,
            FontFamily          = FontConsolas,
            Foreground          = new SolidColorBrush(TextMuted),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        public static TextBlock MakeValue(double fontSize = 16) => new TextBlock
        {
            Text                = "0",
            FontSize            = fontSize,
            FontWeight          = FontWeights.Bold,
            FontFamily          = FontConsolas,
            Foreground          = new SolidColorBrush(TextPrimary),
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
            if (c.Contains("LMP2"))  return Color.FromRgb(0, 144, 255);
            if (c.Contains("GTE") || c.Contains("LMGT")) return Color.FromRgb(255, 140, 0);
            if (c.Contains("GT3"))  return Color.FromRgb(0, 200, 80);
            return Color.FromRgb(180, 180, 180);
        }

        // ================================================================
        // BRUSH SHORTHAND
        // ================================================================

        public static SolidColorBrush Br(Color c)             => new(c);
        public static SolidColorBrush Br(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
