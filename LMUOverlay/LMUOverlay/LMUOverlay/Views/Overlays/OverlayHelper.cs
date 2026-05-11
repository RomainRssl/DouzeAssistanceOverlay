using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Helpers;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Helpers de rendu partagés par tous les overlays.
    /// Toutes les valeurs visuelles sont lues depuis ThemeManager.Current.
    /// </summary>
    public static class OverlayHelper
    {
        // ================================================================
        // COULEURS — propriétés calculées depuis ThemeManager
        // ================================================================

        // Fonds
        public static Color BgDark    => ThemeManager.Current.PanelBackground;
        public static Color BgCell    => Darken(ThemeManager.Current.PanelBackground, 0.85f);
        public static Color BgCellAlt => Darken(ThemeManager.Current.PanelBackground, 0.92f);

        // Fonds sémantiques (subtils, pour les backgrounds de cellules d'état)
        public static Color BgGreen  => WithAlpha(ThemeManager.Current.StateGood,    40);
        public static Color BgRed    => WithAlpha(ThemeManager.Current.StateDanger,  40);
        public static Color BgBlue   => WithAlpha(ThemeManager.Current.ClassLmp2,    40);
        public static Color BgOrange => WithAlpha(ThemeManager.Current.ClassLmgt,    40);
        public static Color BgAccent => WithAlpha(ThemeManager.Current.AccentPrimary, 60);

        // Textes
        public static Color TextPrimary   => ThemeManager.Current.TextPrimary;
        public static Color TextSecondary => ThemeManager.Current.TextSecondary;
        public static Color TextMuted     => ThemeManager.Current.TextMuted;
        public static Color TextValue     => ThemeManager.Current.TextPrimary;
        public static Color TextGear      => ThemeManager.Current.AccentSecondary;

        // Accents fonctionnels
        public static Color AccGreen  => ThemeManager.Current.StateGood;
        public static Color AccRed    => ThemeManager.Current.StateDanger;
        public static Color AccYellow => ThemeManager.Current.StateWarn;
        public static Color AccBlue   => ThemeManager.Current.ClassLmp2;
        public static Color AccPurple => ThemeManager.Current.StateBestLap;

        // Bordure
        public static Color BorderColor => ThemeManager.Current.Border;

        // Polices
        public static FontFamily FontConsolas  => ThemeManager.Current.MonoFont;
        public static FontFamily FontSegoeUISB => ThemeManager.Current.DisplayFont;

        // ================================================================
        // NOUVEAUX HELPERS "Draw*" — API Endurance Noir
        // ================================================================

        /// <summary>Ligne d'accent colorée fine (haut de panel).</summary>
        public static Border DrawAccentLine(Color? color = null) => new()
        {
            Height          = 2,
            Background      = BrushCache.Get(color ?? ThemeManager.Current.AccentPrimary),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        /// <summary>Séparateur horizontal discret.</summary>
        public static Border DrawSeparator() => new()
        {
            Height          = 1,
            Background      = BrushCache.Get(ThemeManager.Current.Border),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin          = new Thickness(0, 2, 0, 2)
        };

        /// <summary>
        /// Panel conteneur principal : fond semi-transparent + bordure + coins + ligne accent optionnelle.
        /// </summary>
        public static Border DrawPanel(UIElement content)
        {
            var tm = ThemeManager.Current;
            var bg = Color.FromArgb(tm.PanelAlpha, tm.PanelBackground.R, tm.PanelBackground.G, tm.PanelBackground.B);

            UIElement inner;
            if (tm.AccentLine)
            {
                var dp = new DockPanel();
                var line = DrawAccentLine();
                DockPanel.SetDock(line, Dock.Top);
                dp.Children.Add(line);
                dp.Children.Add(content);
                inner = dp;
            }
            else
            {
                inner = content;
            }

            return new Border
            {
                CornerRadius    = tm.CornerRadius,
                Padding         = tm.PanelPadding,
                Background      = BrushCache.Get(bg),
                BorderBrush     = BrushCache.Get(tm.Border),
                BorderThickness = tm.BorderThickness,
                Child           = inner
            };
        }

        /// <summary>Header de panel avec titre centré.</summary>
        public static Border DrawPanelHeader(string text)
        {
            var tm = ThemeManager.Current;
            return new Border
            {
                Background      = BrushCache.Get(BgCell),
                BorderBrush     = BrushCache.Get(tm.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(tm.CellPaddingH, tm.CellPaddingV, tm.CellPaddingH, tm.CellPaddingV),
                Child           = new TextBlock
                {
                    Text                = text.ToUpperInvariant(),
                    FontSize            = tm.SizeLabel,
                    FontWeight          = FontWeights.SemiBold,
                    FontFamily          = tm.DisplayFont,
                    Foreground          = BrushCache.Get(tm.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
        }

        /// <summary>Label discret (clé / catégorie).</summary>
        public static TextBlock DrawLabel(string text) => new()
        {
            Text                = text,
            FontSize            = ThemeManager.Current.SizeLabel,
            FontWeight          = FontWeights.Normal,
            FontFamily          = ThemeManager.Current.DisplayFont,
            Foreground          = BrushCache.Get(ThemeManager.Current.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        /// <summary>Valeur numérique en police mono.</summary>
        public static TextBlock DrawMonoValue(string value = "—", double? fontSize = null) => new()
        {
            Text                = value,
            FontSize            = fontSize ?? ThemeManager.Current.SizeLarge,
            FontWeight          = FontWeights.Bold,
            FontFamily          = ThemeManager.Current.MonoFont,
            Foreground          = BrushCache.Get(ThemeManager.Current.TextPrimary),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        /// <summary>Barre de progression avec gradient optionnel (lu depuis ThemeManager).</summary>
        public static Border DrawBar(double pct, Color fillColor, double height = 6)
        {
            pct = Math.Clamp(pct, 0, 1);
            var tm = ThemeManager.Current;

            Brush fill;
            if (tm.BarGradient)
            {
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint   = new Point(1, 0)
                };
                grad.GradientStops.Add(new GradientStop(fillColor, 0));
                grad.GradientStops.Add(new GradientStop(
                    Color.FromArgb(fillColor.A, (byte)(fillColor.R * 0.6), (byte)(fillColor.G * 0.6), (byte)(fillColor.B * 0.6)),
                    1));
                fill = grad;
            }
            else
            {
                fill = BrushCache.Get(fillColor);
            }

            double cr = tm.RoundedBars ? height / 2 : 0;

            var bar = new Border
            {
                Height          = height,
                CornerRadius    = new CornerRadius(cr),
                Background      = fill,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width           = double.NaN // sera fixé dynamiquement
            };

            return new Border
            {
                Height          = height,
                CornerRadius    = new CornerRadius(cr),
                Background      = BrushCache.Get(WithAlpha(fillColor, 30)),
                Child           = new Grid
                {
                    Children =
                    {
                        new Border
                        {
                            CornerRadius = new CornerRadius(cr),
                            Background   = fill,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Width        = double.NaN // l'appelant doit binder la largeur
                        }
                    }
                }
            };
        }

        // ================================================================
        // FACTORIES EXISTANTES — migrent vers ThemeManager
        // ================================================================

        public static Border MakeBorder() => new()
        {
            CornerRadius    = ThemeManager.Current.CornerRadius,
            Padding         = ThemeManager.Current.PanelPadding,
            Background      = BrushCache.Get(Color.FromArgb(
                                  ThemeManager.Current.PanelAlpha,
                                  ThemeManager.Current.PanelBackground.R,
                                  ThemeManager.Current.PanelBackground.G,
                                  ThemeManager.Current.PanelBackground.B)),
            BorderBrush     = BrushCache.Get(ThemeManager.Current.Border),
            BorderThickness = ThemeManager.Current.BorderThickness
        };

        public static Border MakeCell(Color? bg = null) => new()
        {
            Background      = BrushCache.Get(bg ?? BgCell),
            CornerRadius    = ThemeManager.Current.CornerRadius,
            Padding         = new Thickness(
                                  ThemeManager.Current.CellPaddingH,
                                  ThemeManager.Current.CellPaddingV,
                                  ThemeManager.Current.CellPaddingH,
                                  ThemeManager.Current.CellPaddingV),
            Margin          = new Thickness(1)
        };

        public static Border MakeSectionHeader(string text) => new()
        {
            Background      = BrushCache.Get(BgCell),
            BorderBrush     = BrushCache.Get(ThemeManager.Current.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(6, 3, 6, 3),
            Child           = new TextBlock
            {
                Text       = text,
                FontSize   = ThemeManager.Current.SizeLabel,
                FontWeight = FontWeights.SemiBold,
                FontFamily = ThemeManager.Current.DisplayFont,
                Foreground = BrushCache.Get(ThemeManager.Current.TextSecondary)
            }
        };

        public static TextBlock MakeTitle(string text) => new()
        {
            Text                = text,
            FontSize            = ThemeManager.Current.SizeLabel,
            FontWeight          = FontWeights.SemiBold,
            FontFamily          = ThemeManager.Current.DisplayFont,
            Foreground          = BrushCache.Get(ThemeManager.Current.TextSecondary),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 0, 0, 3)
        };

        public static TextBlock MakeLabel(string text) => new()
        {
            Text                = text,
            FontSize            = ThemeManager.Current.SizeLabel,
            FontWeight          = FontWeights.Normal,
            FontFamily          = ThemeManager.Current.MonoFont,
            Foreground          = BrushCache.Get(ThemeManager.Current.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        public static TextBlock MakeValue(double fontSize = 0) => new()
        {
            Text                = "0",
            FontSize            = fontSize > 0 ? fontSize : ThemeManager.Current.SizeLarge,
            FontWeight          = FontWeights.Bold,
            FontFamily          = ThemeManager.Current.MonoFont,
            Foreground          = BrushCache.Get(ThemeManager.Current.TextPrimary),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // ================================================================
        // HELPERS NOM / TEMPS / CLASSE
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
            var tm = ThemeManager.Current;
            string c = (vehicleClass ?? "").ToUpperInvariant();
            if (c.Contains("HYPERCAR") || c.Contains("LMH") || c.Contains("LMDH")) return tm.ClassHypercar;
            if (c.Contains("LMP2"))                                                  return tm.ClassLmp2;
            if (c.Contains("GTE") || c.Contains("LMGT"))                            return tm.ClassLmgt;
            if (c.Contains("GT3"))                                                   return tm.ClassGt3;
            return ThemeManager.Current.TextMuted;
        }

        // ================================================================
        // RACCOURCIS BRUSHES
        // ================================================================

        public static SolidColorBrush Br(Color c)                  => new(c);
        public static SolidColorBrush Br(byte r, byte g, byte b)   => new(Color.FromRgb(r, g, b));

        // ================================================================
        // UTILITAIRES COULEUR
        // ================================================================

        private static Color Darken(Color c, float factor) =>
            Color.FromRgb(
                (byte)(c.R * factor),
                (byte)(c.G * factor),
                (byte)(c.B * factor));

        private static Color WithAlpha(Color c, byte alpha) =>
            Color.FromArgb(alpha, c.R, c.G, c.B);
    }
}
