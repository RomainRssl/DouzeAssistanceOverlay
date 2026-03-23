using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class StandingsOverlay : BaseOverlayWindow
    {
        private readonly StackPanel _mainPanel;
        private readonly StandingsColumnConfig _colCfg;

        private const double F = 10;
        private const double FS = 8;

        public StandingsOverlay(DataService ds, OverlaySettings s, StandingsColumnConfig colCfg) : base(ds, s)
        {
            _colCfg = colCfg;

            var border = new Border
            {
                CornerRadius = new CornerRadius(4), Padding = new Thickness(3),
                Background = new SolidColorBrush(Color.FromArgb(240, 14, 24, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 55, 55)),
                BorderThickness = new Thickness(1)
            };

            _mainPanel = new StackPanel();
            border.Child = _mainPanel;
            Content = border;
        }

        public override void UpdateData()
        {
            _mainPanel.Children.Clear();
            var all = DataService.GetAllVehicles();
            if (all.Count == 0) return;

            // Session bar
            if (_colCfg.ShowSessionInfo)
                _mainPanel.Children.Add(BuildSessionBar());

            // Group by class
            var classes = all
                .GroupBy(v => Classify(v.VehicleClass))
                .OrderBy(g => ClassOrd(g.Key))
                .ToList();

            // Find player's class
            var playerVehicle = all.FirstOrDefault(v => v.IsPlayer);
            string playerClass = playerVehicle != null ? Classify(playerVehicle.VehicleClass) : "";

            foreach (var grp in classes)
            {
                string cls = grp.Key;
                var cars = grp.OrderBy(v => v.Position).ToList();
                Color cc = ClassCol(cls);
                bool isPlayerClass = cls == playerClass;

                // Class header
                _mainPanel.Children.Add(ClassHeader(cls, cars, cc));

                // Column header
                _mainPanel.Children.Add(ColHeader());

                // Player class: show MaxEntriesPerClass, other classes: show OtherClassCount
                int max = isPlayerClass ? _colCfg.MaxEntriesPerClass : _colCfg.OtherClassCount;
                var player = cars.FirstOrDefault(v => v.IsPlayer);
                int pIdx = player != null ? cars.IndexOf(player) : -1;

                for (int i = 0; i < cars.Count && i < max; i++)
                    _mainPanel.Children.Add(Row(cars[i], i + 1, cc, i));

                // Player outside range (should only happen in player's own class)
                if (pIdx >= max && player != null)
                {
                    _mainPanel.Children.Add(new TextBlock { Text = $"··· {pIdx - max} ···", FontSize = 7, Foreground = B(50, 70, 70), HorizontalAlignment = HorizontalAlignment.Center });
                    // Show car before + player + car after
                    if (pIdx > max)
                        _mainPanel.Children.Add(Row(cars[pIdx - 1], pIdx, cc, max));
                    _mainPanel.Children.Add(Row(player, pIdx + 1, cc, max + 1));
                    if (pIdx + 1 < cars.Count)
                        _mainPanel.Children.Add(Row(cars[pIdx + 1], pIdx + 2, cc, max + 2));
                }

                // Show remaining count
                int shown = Math.Min(cars.Count, max) + (pIdx >= max ? 3 : 0);
                if (cars.Count > shown)
                    _mainPanel.Children.Add(new TextBlock { Text = $"+ {cars.Count - shown}", FontSize = 7, Foreground = B(50, 70, 70), HorizontalAlignment = HorizontalAlignment.Center });

                _mainPanel.Children.Add(new Border { Height = 2 });
            }
        }

        // ================================================================
        // SESSION BAR
        // ================================================================

        private UIElement BuildSessionBar()
        {
            var weather = DataService.GetWeatherData();
            var b = new Border { Background = new SolidColorBrush(Color.FromRgb(16, 28, 32)), Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 0, 0, 1) };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            if (weather != null)
            {
                string wet = weather.Raining > 0.1 ? "Wet" : "Dry";
                sp.Children.Add(T($"☁ {wet}  {weather.AmbientTemp:F0}/{weather.TrackTemp:F0}°", FS, B(120, 150, 150)));
            }
            b.Child = sp;
            return b;
        }

        // ================================================================
        // CLASS HEADER
        // ================================================================

        private static UIElement ClassHeader(string cls, List<VehicleData> cars, Color cc)
        {
            var hdr = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, cc.R, cc.G, cc.B)),
                Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(0, 1, 0, 0)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = cls, FontSize = 11, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = Brushes.White, Margin = new Thickness(0, 0, 8, 0) });

            double best = cars.Where(v => v.BestLapTime > 0).Select(v => v.BestLapTime).DefaultIfEmpty(0).Min();
            if (best > 0)
                sp.Children.Add(new TextBlock { Text = $"~{FT(best)}", FontSize = 9, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(220, 240, 240)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });

            sp.Children.Add(new TextBlock { Text = $"{cars.Count}🚗", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(220, 235, 235)), VerticalAlignment = VerticalAlignment.Center });

            hdr.Child = sp;
            return hdr;
        }

        // ================================================================
        // COLUMN HEADER
        // ================================================================

        private UIElement ColHeader()
        {
            var g = MakeGrid();
            int c = 0;
            P(g, c++, T("P", 7, B(55, 80, 80), HorizontalAlignment.Center)); // pos
            c++; // class bar
            P(g, c++, T("DRIVER", 7, B(55, 80, 80))); // driver
            c++; // car #
            P(g, c++, T("P", 7, B(55, 80, 80), HorizontalAlignment.Center)); // pit
            P(g, c++, T("GAP/BEST", 7, B(55, 80, 80), HorizontalAlignment.Right));
            P(g, c++, T("LAST", 7, B(55, 80, 80), HorizontalAlignment.Right));
            if (_colCfg.TireCompound) P(g, c++, T("T", 7, B(55, 80, 80), HorizontalAlignment.Center));
            if (_colCfg.SectorStatus) P(g, c++, T("SEC", 7, B(55, 80, 80), HorizontalAlignment.Center));
            if (_colCfg.Speed) P(g, c++, T("SPD", 7, B(55, 80, 80), HorizontalAlignment.Right));
            return g;
        }

        // ================================================================
        // DRIVER ROW
        // ================================================================

        private UIElement Row(VehicleData v, int classPos, Color cc, int idx)
        {
            bool isP = v.IsPlayer;
            Color bg = isP ? Color.FromArgb(45, 0, 190, 230) : idx % 2 == 0 ? Color.FromArgb(10, 200, 255, 255) : Colors.Transparent;

            var row = new Border { Background = new SolidColorBrush(bg), Padding = new Thickness(1, 1, 1, 1), MinHeight = 17 };
            if (isP) { row.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 210, 230)); row.BorderThickness = new Thickness(2, 0, 0, 0); }

            var g = MakeGrid();
            int c = 0;

            // Class position only
            var posTb = new TextBlock
            {
                Text = $"{classPos}", FontSize = F, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(cc),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            P(g, c++, posTb);

            // Class bar
            P(g, c++, new Border { Width = 3, CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(cc), Margin = new Thickness(0, 1, 0, 1) });

            // Driver
            P(g, c++, new TextBlock { Text = OverlayHelper.FormatName(v.DriverName), FontSize = F, FontWeight = isP ? FontWeights.Bold : FontWeights.Normal, FontFamily = new FontFamily("Consolas"), Foreground = isP ? B(0, 230, 250) : B(210, 225, 225), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });

            // Car number
            var nb = new Border { Background = new SolidColorBrush(Color.FromArgb(50, cc.R, cc.G, cc.B)), CornerRadius = new CornerRadius(2), Padding = new Thickness(2, 0, 2, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            nb.Child = new TextBlock { Text = v.CarNumber, FontSize = F, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
            P(g, c++, nb);

            // Pit
            P(g, c++, T(v.InPits ? "P" : v.NumPitstops > 0 ? $"{v.NumPitstops}" : "", F, v.InPits ? B(255, 204, 0) : B(80, 105, 105), HorizontalAlignment.Center));

            // Gap / Best
            string gap; SolidColorBrush gc;
            if (classPos == 1) { gap = v.BestLapTime > 0 ? FT(v.BestLapTime) : "-:--.---"; gc = B(76, 217, 100); }
            else { gap = $"+{v.TimeBehindNext:F3}"; gc = B(170, 185, 190); }
            P(g, c++, T(gap, F, gc, HorizontalAlignment.Right));

            // Last lap
            string lt = v.LastLapTime > 0 ? FT(v.LastLapTime) : "00:00.000";
            Color lc = v.LastLapTime > 0 && Math.Abs(v.LastLapTime - v.BestLapTime) < 0.001 ? Color.FromRgb(76, 217, 100) : v.LastLapTime > 0 ? Color.FromRgb(200, 212, 216) : Color.FromRgb(70, 90, 90);
            P(g, c++, T(lt, F, new SolidColorBrush(lc), HorizontalAlignment.Right));

            // Tire
            if (_colCfg.TireCompound)
            {
                string tc = string.IsNullOrEmpty(v.TireCompound) ? "?" : v.TireCompound[..1];
                P(g, c++, T(tc, FS, B(88, 166, 255), HorizontalAlignment.Center));
            }

            // Sectors
            if (_colCfg.SectorStatus)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(SD(v.S1Status, v.CurrentSector == 1));
                sp.Children.Add(SD(v.S2Status, v.CurrentSector == 2));
                sp.Children.Add(SD(v.S3Status, v.CurrentSector == 0));
                P(g, c++, sp);
            }

            // Speed
            if (_colCfg.Speed)
                P(g, c++, T($"{v.Speed * 3.6:F0}", F, B(130, 150, 155), HorizontalAlignment.Right));

            row.Child = g;
            return row;
        }

        // ================================================================
        // GRID LAYOUT
        // ================================================================

        private Grid MakeGrid()
        {
            var g = new Grid { Margin = new Thickness(1, 0, 1, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });  // pos
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });   // class bar
            g.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 80 });               // driver (star)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // car #
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });  // pit
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });  // gap
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });  // last
            if (_colCfg.TireCompound) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            if (_colCfg.SectorStatus) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            if (_colCfg.Speed) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            return g;
        }

        private static void P(Grid g, int col, UIElement el) { if (col < g.ColumnDefinitions.Count) { Grid.SetColumn(el, col); g.Children.Add(el); } }

        private static Border SD(SectorStatus s, bool cur)
        {
            Color bg = s switch { SectorStatus.SessionBest => Color.FromRgb(168, 85, 247), SectorStatus.PersonalBest => Color.FromRgb(76, 217, 100), SectorStatus.Slower => Color.FromRgb(255, 204, 0), _ => Color.FromArgb(30, 90, 120, 120) };
            return new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(bg), Margin = new Thickness(1, 0, 1, 0), BorderThickness = cur ? new Thickness(1) : new Thickness(0), BorderBrush = cur ? Brushes.White : null };
        }

        // Helpers
        private static string Classify(string vc) { string c = (vc ?? "").ToUpperInvariant(); if (c.Contains("HYPERCAR") || c.Contains("LMH") || c.Contains("LMDH")) return "HYPERCAR"; if (c.Contains("LMP2")) return "LMP2"; if (c.Contains("LMP3")) return "LMP3"; if (c.Contains("GTE") || c.Contains("LMGT")) return "LMGTE"; if (c.Contains("GT3")) return "GT3"; if (c.Contains("GT4")) return "GT4"; return string.IsNullOrEmpty(vc) ? "OTHER" : vc.ToUpperInvariant(); }
        private static int ClassOrd(string c) => c switch { "HYPERCAR" => 0, "LMP2" => 1, "LMP3" => 2, "LMGTE" => 3, "GT3" => 4, "GT4" => 5, _ => 9 };
        private static Color ClassCol(string c) => c switch { "HYPERCAR" => Color.FromRgb(180, 20, 0), "LMP2" => Color.FromRgb(0, 100, 200), "LMP3" => Color.FromRgb(140, 140, 0), "LMGTE" => Color.FromRgb(0, 140, 50), "GT3" => Color.FromRgb(180, 120, 0), "GT4" => Color.FromRgb(100, 60, 140), _ => Color.FromRgb(80, 80, 80) };
        private static string FT(double t) { if (t <= 0) return "-:--.---"; var s = TimeSpan.FromSeconds(t); return $"{(int)s.TotalMinutes}:{s.Seconds:D2}.{s.Milliseconds:D3}"; }
        private static TextBlock T(string text, double sz, SolidColorBrush fg, HorizontalAlignment ha = HorizontalAlignment.Left) => new() { Text = text, FontSize = sz, FontFamily = new FontFamily("Consolas"), Foreground = fg, HorizontalAlignment = ha, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        private static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
