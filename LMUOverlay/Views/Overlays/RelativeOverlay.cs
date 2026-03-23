using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Relative standings sorted by on-track time gap to the player.
    /// All classes mixed. Cars ahead have negative gap, cars behind positive.
    /// Configurable number of entries ahead and behind.
    /// </summary>
    public class RelativeOverlay : BaseOverlayWindow
    {
        private readonly StackPanel _listPanel;
        private readonly RelativeConfig _cfg;

        private const double FS = 11;
        private const double FS_S = 9;

        public RelativeOverlay(DataService ds, OverlaySettings s, RelativeConfig cfg) : base(ds, s)
        {
            _cfg = cfg;

            var border = OverlayHelper.MakeBorder();
            var sp = new StackPanel();

            // Header row
            var hdr = MakeGrid();
            AddTb(hdr, "P", 0, FS_S, Color.FromRgb(80, 110, 110), HorizontalAlignment.Center);
            AddTb(hdr, "DRIVER", 2, FS_S, Color.FromRgb(80, 110, 110), HorizontalAlignment.Left);
            AddTb(hdr, "GAP", 4, FS_S, Color.FromRgb(80, 110, 110), HorizontalAlignment.Right);
            sp.Children.Add(hdr);
            sp.Children.Add(new Border { Height = 1, Background = Br(36, 68, 68), Margin = new Thickness(0, 2, 0, 2) });

            _listPanel = new StackPanel();
            sp.Children.Add(_listPanel);

            border.Child = sp;
            Content = border;
        }

        public override void UpdateData()
        {
            _listPanel.Children.Clear();

            var relative = DataService.GetRelativeByTime(_cfg.AheadCount, _cfg.BehindCount);
            if (relative.Count == 0) return;

            foreach (var (v, gap) in relative)
            {
                bool isPlayer = v.IsPlayer;
                Color classColor = OverlayHelper.GetClassColor(v.VehicleClass);

                // Row background
                Color rowBg;
                if (isPlayer)
                    rowBg = Color.FromArgb(50, 0, 200, 170);
                else
                    rowBg = Colors.Transparent;

                var row = new Border
                {
                    Padding = new Thickness(0, 2, 0, 2),
                    Background = new SolidColorBrush(rowBg)
                };

                if (isPlayer)
                {
                    row.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 170));
                    row.BorderThickness = new Thickness(1, 0, 0, 0);
                }

                var g = MakeGrid();

                // Position
                var posTb = new TextBlock
                {
                    Text = $"{v.Position}", FontSize = FS, FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(classColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(posTb, 0);
                g.Children.Add(posTb);

                // Class bar
                var bar = new Border
                {
                    Width = 3, CornerRadius = new CornerRadius(1),
                    Background = new SolidColorBrush(classColor),
                    Margin = new Thickness(0, 2, 2, 2)
                };
                Grid.SetColumn(bar, 1);
                g.Children.Add(bar);

                // Driver name
                var nameTb = new TextBlock
                {
                    Text = OverlayHelper.FormatName(v.DriverName),
                    FontSize = FS,
                    FontWeight = isPlayer ? FontWeights.Bold : FontWeights.Normal,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(isPlayer ? Color.FromRgb(100, 240, 220) : Color.FromRgb(220, 230, 230)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameTb, 2);
                g.Children.Add(nameTb);

                // Class abbreviation
                var classTb = new TextBlock
                {
                    Text = ClassShort(v.VehicleClass),
                    FontSize = 8, FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromArgb(180, classColor.R, classColor.G, classColor.B)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(classTb, 3);
                g.Children.Add(classTb);

                // Gap
                string gapStr;
                Color gapColor;
                if (isPlayer)
                {
                    gapStr = "---";
                    gapColor = Color.FromRgb(100, 240, 220);
                }
                else if (gap < 0)
                {
                    // Ahead (negative = on top)
                    double absGap = Math.Abs(gap);
                    gapStr = absGap >= 10 ? $"-{absGap:F1}" : $"-{absGap:F3}";
                    gapColor = Color.FromRgb(76, 217, 100);
                }
                else
                {
                    // Behind (positive = on bottom)
                    gapStr = gap >= 10 ? $"+{gap:F1}" : $"+{gap:F3}";
                    gapColor = Color.FromRgb(255, 80, 80);
                }

                // If gap is huge (> half a lap time), probably lapped — show in laps
                if (Math.Abs(gap) > 60)
                {
                    gapStr = gap < 0 ? $"-{Math.Abs(gap):F0}s" : $"+{gap:F0}s";
                }

                var gapTb = new TextBlock
                {
                    Text = gapStr, FontSize = FS,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(gapColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(gapTb, 4);
                g.Children.Add(gapTb);

                row.Child = g;
                _listPanel.Children.Add(row);
            }
        }

        // ================================================================
        // GRID
        // ================================================================

        private static Grid MakeGrid()
        {
            var g = new Grid { Margin = new Thickness(2, 0, 2, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });   // pos
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });    // class bar
            g.ColumnDefinitions.Add(new ColumnDefinition());                                 // name (star)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });   // class short
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });   // gap
            return g;
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private static void AddTb(Grid g, string text, int col, double size, Color color, HorizontalAlignment ha)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = size,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = ha,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        private static string ClassShort(string vc)
        {
            string c = (vc ?? "").ToUpperInvariant();
            if (c.Contains("HYPERCAR") || c.Contains("LMH") || c.Contains("LMDH")) return "HYP";
            if (c.Contains("LMP2")) return "LP2";
            if (c.Contains("LMP3")) return "LP3";
            if (c.Contains("GTE") || c.Contains("LMGT")) return "GTE";
            if (c.Contains("GT3")) return "GT3";
            if (c.Contains("GT4")) return "GT4";
            return vc?.Length > 3 ? vc[..3].ToUpperInvariant() : (vc ?? "?").ToUpperInvariant();
        }

        private static SolidColorBrush Br(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
