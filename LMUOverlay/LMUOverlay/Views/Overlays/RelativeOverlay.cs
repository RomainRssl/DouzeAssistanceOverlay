using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Relative standings sorted by on-track time gap to the player.
    /// All classes mixed. Cars ahead have negative gap, cars behind positive.
    /// Rows are pre-allocated at construction — UpdateData() only updates text/colors.
    /// </summary>
    public class RelativeOverlay : BaseOverlayWindow
    {
        private readonly RelativeConfig _cfg;

        private const double FS   = 11;
        private const double FS_S = 9;

        private static readonly FontFamily _consolas = new("Consolas");

        // Pre-allocated row elements — zero allocation in UpdateData()
        private sealed class RelRow
        {
            public Border    Container  = null!;
            public TextBlock Pos        = null!;
            public Border    ClassBar   = null!;
            public TextBlock Name       = null!;
            public TextBlock ClassShort = null!;
            public TextBlock Gap        = null!;
        }

        private RelRow[] _rows = Array.Empty<RelRow>();

        public RelativeOverlay(DataService ds, OverlaySettings s, RelativeConfig cfg) : base(ds, s)
        {
            _cfg = cfg;

            var border = OverlayHelper.MakeBorder();
            var sp = new StackPanel();

            // Header row
            var hdr = MakeGrid();
            AddTb(hdr, "P",      0, FS_S, Color.FromRgb(80, 110, 110), HorizontalAlignment.Center);
            AddTb(hdr, "DRIVER", 2, FS_S, Color.FromRgb(80, 110, 110), HorizontalAlignment.Left);
            AddTb(hdr, "GAP",    4, FS_S, Color.FromRgb(80, 110, 110), HorizontalAlignment.Right);
            sp.Children.Add(hdr);
            sp.Children.Add(new Border { Height = 1, Background = BrushCache.Get(36, 68, 68), Margin = new Thickness(0, 2, 0, 2) });

            // Pre-allocate all rows
            int total = _cfg.AheadCount + _cfg.BehindCount + 1;
            _rows = new RelRow[total];
            var listPanel = new StackPanel();

            for (int i = 0; i < total; i++)
            {
                var row = BuildEmptyRow();
                _rows[i] = row;
                listPanel.Children.Add(row.Container);
            }

            sp.Children.Add(listPanel);
            border.Child = sp;
            Content = border;
        }

        private static RelRow BuildEmptyRow()
        {
            var cons = _consolas;

            var posTb = new TextBlock
            {
                FontSize = FS, FontWeight = FontWeights.Bold, FontFamily = cons,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            var classBar = new Border
            {
                Width = 3, CornerRadius = new CornerRadius(1),
                Margin = new Thickness(0, 2, 2, 2)
            };

            var nameTb = new TextBlock
            {
                FontSize = FS, FontFamily = cons,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var classTb = new TextBlock
            {
                FontSize = 8, FontFamily = cons,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var gapTb = new TextBlock
            {
                FontSize = FS, FontWeight = FontWeights.SemiBold, FontFamily = cons,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var g = MakeGrid();
            Grid.SetColumn(posTb,    0);
            Grid.SetColumn(classBar, 1);
            Grid.SetColumn(nameTb,   2);
            Grid.SetColumn(classTb,  3);
            Grid.SetColumn(gapTb,    4);
            g.Children.Add(posTb);
            g.Children.Add(classBar);
            g.Children.Add(nameTb);
            g.Children.Add(classTb);
            g.Children.Add(gapTb);

            // Pre-set player left border (always present, thickness controls visibility)
            var container = new Border
            {
                Padding         = new Thickness(0, 2, 0, 2),
                BorderBrush     = BrushCache.Get(0, 200, 170),
                BorderThickness = new Thickness(0),
                Child           = g,
                Visibility      = Visibility.Collapsed
            };

            return new RelRow
            {
                Container  = container,
                Pos        = posTb,
                ClassBar   = classBar,
                Name       = nameTb,
                ClassShort = classTb,
                Gap        = gapTb
            };
        }

        public override void UpdateData()
        {
            var relative = DataService.GetRelativeByTime(_cfg.AheadCount, _cfg.BehindCount);

            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];

                if (i >= relative.Count)
                {
                    row.Container.Visibility = Visibility.Collapsed;
                    continue;
                }

                var (v, gap) = relative[i];
                bool isPlayer  = v.IsPlayer;
                Color cc = OverlayHelper.GetClassColor(v.VehicleClass);

                // Container background + player left border
                row.Container.Background      = isPlayer ? BrushCache.Get(50, 0, 200, 170) : BrushCache.Get(Colors.Transparent);
                row.Container.BorderThickness = isPlayer ? new Thickness(1, 0, 0, 0) : new Thickness(0);
                row.Container.Visibility      = Visibility.Visible;

                // Position
                row.Pos.Text       = $"{v.Position}";
                row.Pos.Foreground = BrushCache.Get(cc);

                // Class bar
                row.ClassBar.Background = BrushCache.Get(cc);

                // Driver name
                row.Name.Text       = OverlayHelper.FormatName(v.DriverName);
                row.Name.FontWeight = isPlayer ? FontWeights.Bold : FontWeights.Normal;
                row.Name.Foreground = isPlayer ? BrushCache.Get(100, 240, 220) : BrushCache.Get(220, 230, 230);

                // Class abbreviation
                row.ClassShort.Text       = GetClassShort(v.VehicleClass);
                row.ClassShort.Foreground = BrushCache.Get(Color.FromArgb(180, cc.R, cc.G, cc.B));

                // Gap
                string gapStr;
                Color  gapColor;
                if (isPlayer)
                {
                    gapStr   = "---";
                    gapColor = Color.FromRgb(100, 240, 220);
                }
                else if (gap < 0)
                {
                    double abs = Math.Abs(gap);
                    gapStr   = abs >= 10 ? $"-{abs:F1}" : $"-{abs:F3}";
                    gapColor = Color.FromRgb(76, 217, 100);
                }
                else
                {
                    gapStr   = gap >= 10 ? $"+{gap:F1}" : $"+{gap:F3}";
                    gapColor = Color.FromRgb(255, 80, 80);
                }

                if (Math.Abs(gap) > 60)
                    gapStr = gap < 0 ? $"-{Math.Abs(gap):F0}s" : $"+{gap:F0}s";

                row.Gap.Text       = gapStr;
                row.Gap.Foreground = BrushCache.Get(gapColor);
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
                FontFamily = _consolas,
                Foreground = BrushCache.Get(color),
                HorizontalAlignment = ha,
                VerticalAlignment   = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        private static string GetClassShort(string vc)
        {
            string c = (vc ?? "").ToUpperInvariant();
            if (c.Contains("HYPERCAR") || c.Contains("LMH") || c.Contains("LMDH")) return "HYP";
            if (c.Contains("LMP2")) return "LMP2";
            if (c.Contains("LMP3")) return "LP3";
            if (c.Contains("GTE") || c.Contains("LMGT")) return "GTE";
            if (c.Contains("GT3")) return "GT3";
            if (c.Contains("GT4")) return "GT4";
            return vc?.Length > 3 ? vc[..3].ToUpperInvariant() : (vc ?? "?").ToUpperInvariant();
        }
    }
}
