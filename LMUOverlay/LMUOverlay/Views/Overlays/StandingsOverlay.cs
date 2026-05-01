using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class StandingsOverlay : BaseOverlayWindow
    {
        private readonly StackPanel _mainPanel;
        private readonly StandingsColumnConfig _colCfg;

        private const double F  = 10;
        private const double FS = 8;

        private static readonly FontFamily _consolas = new("Consolas");
        private int _lastDataHash = 0;

        public StandingsOverlay(DataService ds, OverlaySettings s, StandingsColumnConfig colCfg) : base(ds, s)
        {
            _colCfg = colCfg;

            var border = new Border
            {
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(3),
                Background      = new SolidColorBrush(Color.FromArgb(240, 14, 24, 28)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 55, 55)),
                BorderThickness = new Thickness(1)
            };

            _mainPanel     = new StackPanel();
            border.Child   = _mainPanel;
            Content        = border;
        }

        public override void UpdateData()
        {
            var all = DataService.GetAllVehicles();
            if (all.Count == 0) return;

            int hash = ComputeStandingsHash(all);
            if (hash == _lastDataHash) return;
            _lastDataHash = hash;

            _mainPanel.Children.Clear();

            if (_colCfg.ShowSessionInfo)
                _mainPanel.Children.Add(BuildSessionBar());

            var playerVehicle = all.FirstOrDefault(v => v.IsPlayer);
            string playerClass = playerVehicle != null ? Classify(playerVehicle.VehicleClass) : "";

            var classes = all
                .GroupBy(v => Classify(v.VehicleClass))
                .OrderBy(g => ClassOrd(g.Key))
                .ToList();

            foreach (var grp in classes)
            {
                string cls         = grp.Key;
                var    cars        = grp.OrderBy(v => v.Position).ToList();
                Color  cc          = ClassCol(cls);
                bool   isPlayerCls = cls == playerClass;

                _mainPanel.Children.Add(ClassHeader(cls, cars, cc));
                _mainPanel.Children.Add(ColHeader());

                int max  = isPlayerCls ? _colCfg.MaxEntriesPerClass : _colCfg.OtherClassCount;
                var player = cars.FirstOrDefault(v => v.IsPlayer);
                int pIdx = player != null ? cars.IndexOf(player) : -1;

                for (int i = 0; i < cars.Count && i < max; i++)
                    _mainPanel.Children.Add(Row(cars[i], i + 1, cc, i));

                if (pIdx >= max && player != null)
                {
                    _mainPanel.Children.Add(new TextBlock { Text = $"··· {pIdx - max} ···", FontSize = 7, Foreground = B(50, 70, 70), HorizontalAlignment = HorizontalAlignment.Center });
                    if (pIdx > max)
                        _mainPanel.Children.Add(Row(cars[pIdx - 1], pIdx,     cc, max));
                    _mainPanel.Children.Add(Row(player,           pIdx + 1, cc, max + 1));
                    if (pIdx + 1 < cars.Count)
                        _mainPanel.Children.Add(Row(cars[pIdx + 1], pIdx + 2, cc, max + 2));
                }

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
            var b  = new Border { Background = new SolidColorBrush(Color.FromRgb(16, 28, 32)), Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 0, 0, 1) };
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
            var hdr = new Border { Background = BrushCache.Get(Color.FromArgb(200, cc.R, cc.G, cc.B)), Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(0, 1, 0, 0) };
            var sp  = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = cls, FontSize = 11, FontWeight = FontWeights.Bold, FontFamily = _consolas, Foreground = Brushes.White, Margin = new Thickness(0, 0, 8, 0) });
            double best = cars.Where(v => v.BestLapTime > 0).Select(v => v.BestLapTime).DefaultIfEmpty(0).Min();
            if (best > 0)
                sp.Children.Add(new TextBlock { Text = $"~{FT(best)}", FontSize = 9, FontFamily = _consolas, Foreground = BrushCache.Get(220, 240, 240), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            sp.Children.Add(new TextBlock { Text = $"{cars.Count}🚗", FontSize = 9, Foreground = BrushCache.Get(220, 235, 235), VerticalAlignment = VerticalAlignment.Center });
            hdr.Child = sp;
            return hdr;
        }

        // ================================================================
        // COLUMN HEADER — doit correspondre EXACTEMENT à MakeGrid() et Row()
        // ================================================================

        private UIElement ColHeader()
        {
            var g = MakeGrid();
            int c = 0;
            // Colonnes fixes
            P(g, c++, T("P",       7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.ClassBar) c++; // class bar (pas de texte)
            // Optionnelles structurelles
            if (_colCfg.Driver)      P(g, c++, T("PILOTE",  7, B(55,80,80)));
            if (_colCfg.CarName)     P(g, c++, T("VOITURE", 7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.CarNumber)   { c++; } // badge, pas de label texte
            if (_colCfg.PitStops)    P(g, c++, T("PIT",     7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.GapToNext)   P(g, c++, T("ÉCART",   7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.BestLap)     P(g, c++, T("BEST",    7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.LastLap)     P(g, c++, T("DERNIER", 7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.Delta)       P(g, c++, T("Δ",       7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.GapToLeader) P(g, c++, T("LEADER",  7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.TotalLaps)   P(g, c++, T("TOURS",   7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.LapProgress) P(g, c++, T("PROG%",   7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.StintLaps)   P(g, c++, T("SL",      7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.StintTime)   P(g, c++, T("ST",      7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.TireCompound)P(g, c++, T("T",       7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.Damage)      P(g, c++, T("DMG",     7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.Penalties)   P(g, c++, T("PEN",     7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.SectorStatus)P(g, c++, T("SEC",     7, B(55,80,80), HorizontalAlignment.Center));
            if (_colCfg.Speed)       P(g, c++, T("SPD",     7, B(55,80,80), HorizontalAlignment.Right));
            if (_colCfg.Indicator)   { c++; } // indicator (pas de label)
            return g;
        }

        // ================================================================
        // DRIVER ROW
        // ================================================================

        private UIElement Row(VehicleData v, int classPos, Color cc, int idx)
        {
            bool isP = v.IsPlayer;
            Color bg = isP ? Color.FromArgb(45, 0, 190, 230) : idx % 2 == 0 ? Color.FromArgb(10, 200, 255, 255) : Colors.Transparent;

            var row = new Border { Background = BrushCache.Get(bg), Padding = new Thickness(1, 1, 1, 1), MinHeight = 17 };
            if (isP) { row.BorderBrush = BrushCache.Get(0, 210, 230); row.BorderThickness = new Thickness(2, 0, 0, 0); }

            var g = MakeGrid();
            int c = 0;

            // Position (toujours)
            P(g, c++, new TextBlock { Text = $"{classPos}", FontSize = F, FontWeight = FontWeights.Bold, FontFamily = _consolas, Foreground = BrushCache.Get(cc), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

            // Class bar (optionnel)
            if (_colCfg.ClassBar)
                P(g, c++, new Border { Width = 3, CornerRadius = new CornerRadius(1), Background = BrushCache.Get(cc), Margin = new Thickness(0, 1, 0, 1) });

            // Driver
            if (_colCfg.Driver)
                P(g, c++, new TextBlock { Text = OverlayHelper.FormatName(v.DriverName), FontSize = F, FontWeight = isP ? FontWeights.Bold : FontWeights.Normal, FontFamily = _consolas, Foreground = isP ? B(0, 230, 250) : B(210, 225, 225), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });

            // Manufacturer badge (image or text fallback)
            if (_colCfg.CarName)
                P(g, c++, MakeBrandBadge(v.VehicleName, v.UpgradePack));

            // Car number badge
            if (_colCfg.CarNumber)
            {
                var nb = new Border { Background = BrushCache.Get(Color.FromArgb(50, cc.R, cc.G, cc.B)), CornerRadius = new CornerRadius(2), Padding = new Thickness(2, 0, 2, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                nb.Child = new TextBlock { Text = v.CarNumber, FontSize = FS, FontWeight = FontWeights.Bold, FontFamily = _consolas, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
                P(g, c++, nb);
            }

            // Pit stops
            if (_colCfg.PitStops)
                P(g, c++, T(v.InPits ? "P" : v.NumPitstops > 0 ? $"{v.NumPitstops}" : "", F, v.InPits ? B(255, 204, 0) : B(80, 105, 105), HorizontalAlignment.Center));

            // Gap to next (écart au précédent dans le classement)
            if (_colCfg.GapToNext)
            {
                string gap = classPos == 1 ? "---" : $"+{v.TimeBehindNext:F3}";
                P(g, c++, T(gap, F, classPos == 1 ? B(80,105,105) : B(170, 185, 190), HorizontalAlignment.Right));
            }

            // Best lap
            if (_colCfg.BestLap)
            {
                string bl = v.BestLapTime > 0 ? FT(v.BestLapTime) : "-:--.---";
                P(g, c++, T(bl, F, B(76, 217, 100), HorizontalAlignment.Right));
            }

            // Last lap
            if (_colCfg.LastLap)
            {
                string lt = v.LastLapTime > 0 ? FT(v.LastLapTime) : "--:--.---";
                Color lc = v.LastLapTime > 0 && Math.Abs(v.LastLapTime - v.BestLapTime) < 0.001
                    ? Color.FromRgb(76, 217, 100) : v.LastLapTime > 0
                    ? Color.FromRgb(200, 212, 216) : Color.FromRgb(70, 90, 90);
                P(g, c++, T(lt, F, BrushCache.Get(lc), HorizontalAlignment.Right));
            }

            // Delta (dernier - meilleur)
            if (_colCfg.Delta)
            {
                string delta = v.LastLapDelta == 0 ? "--" : v.LastLapDelta > 0 ? $"+{v.LastLapDelta:F3}" : $"{v.LastLapDelta:F3}";
                Color dc = v.LastLapDelta < 0 ? Color.FromRgb(76, 217, 100) : v.LastLapDelta > 0 ? Color.FromRgb(255, 204, 0) : Color.FromRgb(80, 105, 105);
                P(g, c++, T(delta, F, BrushCache.Get(dc), HorizontalAlignment.Right));
            }

            // Gap to leader
            if (_colCfg.GapToLeader)
            {
                string gl = classPos == 1 ? "---" : v.LapsBehindLeader > 0 ? $"+{v.LapsBehindLeader}L" : $"+{v.TimeBehindLeader:F3}";
                P(g, c++, T(gl, F, B(140, 160, 165), HorizontalAlignment.Right));
            }

            // Total laps
            if (_colCfg.TotalLaps)
                P(g, c++, T($"{v.TotalLaps}", F, B(130, 150, 155), HorizontalAlignment.Center));

            // Lap progress %
            if (_colCfg.LapProgress)
                P(g, c++, T($"{v.LapProgress:F1}%", F, B(120, 140, 145), HorizontalAlignment.Right));

            // Stint laps
            if (_colCfg.StintLaps)
                P(g, c++, T($"{v.StintLaps}", F, B(130, 150, 155), HorizontalAlignment.Center));

            // Stint time
            if (_colCfg.StintTime)
            {
                string st = v.StintTime > 0 ? $"{(int)(v.StintTime / 60)}:{(int)(v.StintTime % 60):D2}" : "--:--";
                P(g, c++, T(st, F, B(130, 150, 155), HorizontalAlignment.Right));
            }

            // Tire compound — 4 dots (FL FR / RL RR)
            if (_colCfg.TireCompound)
            {
                var axle = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 18, Height = 14 };
                axle.RowDefinitions.Add(new RowDefinition());
                axle.RowDefinitions.Add(new RowDefinition());
                axle.ColumnDefinitions.Add(new ColumnDefinition());
                axle.ColumnDefinitions.Add(new ColumnDefinition());
                Color cf = TireColor(v.FrontTireCompound);
                Color cr = TireColor(v.RearTireCompound);
                foreach (var (dr, dc, color) in new[] { (0,0,cf),(0,1,cf),(1,0,cr),(1,1,cr) })
                {
                    var dot = new Border { Width = 7, Height = 6, CornerRadius = new CornerRadius(2), Background = BrushCache.Get(color), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetRow(dot, dr); Grid.SetColumn(dot, dc);
                    axle.Children.Add(dot);
                }
                P(g, c++, axle);
            }

            // Damage
            if (_colCfg.Damage)
            {
                Color dmgC = v.DamagePercent > 50 ? Color.FromRgb(255, 59, 48) : v.DamagePercent > 20 ? Color.FromRgb(255, 204, 0) : Color.FromRgb(80, 105, 105);
                P(g, c++, T(v.DamagePercent > 0 ? $"{v.DamagePercent:F0}%" : "--", F, BrushCache.Get(dmgC), HorizontalAlignment.Right));
            }

            // Penalties
            if (_colCfg.Penalties)
                P(g, c++, T(v.NumPenalties > 0 ? $"{v.NumPenalties}" : "", F, B(255, 100, 0), HorizontalAlignment.Center));

            // Sector indicators
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

            // Flag / pit indicator
            if (_colCfg.Indicator)
            {
                Color ic = v.Flag switch
                {
                    1 => Color.FromRgb(0,   120, 255), // blue flag
                    2 => Color.FromRgb(255, 204,   0), // yellow flag
                    3 => Color.FromRgb(30,   30,  30), // black flag (dark)
                    4 => Color.FromRgb(200, 200, 200), // checkered
                    5 => Color.FromRgb(255,  59,  48), // DQ
                    _ => Colors.Transparent
                };
                if (v.InPits) ic = Color.FromRgb(88, 166, 255); // pit lane override
                var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = BrushCache.Get(ic), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                P(g, c++, dot);
            }

            row.Child = g;
            return row;
        }

        // ================================================================
        // GRID LAYOUT — doit correspondre EXACTEMENT à ColHeader() et Row()
        // ================================================================

        private Grid MakeGrid()
        {
            var g = new Grid { Margin = new Thickness(1, 0, 1, 0) };
            // Colonnes fixes
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });  // pos
            if (_colCfg.ClassBar) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });   // class bar
            // Colonnes optionnelles
            if (_colCfg.Driver)      g.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 70 });
            if (_colCfg.CarName)     g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            if (_colCfg.CarNumber)   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            if (_colCfg.PitStops)    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            if (_colCfg.GapToNext)   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            if (_colCfg.BestLap)     g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            if (_colCfg.LastLap)     g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            if (_colCfg.Delta)       g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            if (_colCfg.GapToLeader) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            if (_colCfg.TotalLaps)   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            if (_colCfg.LapProgress) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            if (_colCfg.StintLaps)   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            if (_colCfg.StintTime)   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            if (_colCfg.TireCompound) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            if (_colCfg.Damage)      g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            if (_colCfg.Penalties)   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            if (_colCfg.SectorStatus)g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            if (_colCfg.Speed)       g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            if (_colCfg.Indicator)   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            return g;
        }

        // ================================================================
        // MANUFACTURER BADGE
        // ================================================================

        private static readonly Dictionary<string, BitmapImage?> _logoCache = new();

        private static UIElement MakeBrandBadge(string vehicleName, string upgradePack = "")
        {
            string? key = DetectManufacturer(vehicleName) ?? DetectManufacturer(upgradePack);
            BitmapImage? bmp = null;
            if (key != null)
            {
                if (!_logoCache.TryGetValue(key, out bmp))
                {
                    try
                    {
                        var uri = new Uri($"pack://application:,,,/Resources/Manufacturers/{key}.png", UriKind.Absolute);
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = uri;
                        bi.DecodePixelHeight = 28;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        bmp = bi;
                    }
                    catch { bmp = null; }
                    _logoCache[key] = bmp;
                }
            }

            if (bmp != null)
            {
                var img = new Image
                {
                    Source = bmp,
                    Height = 13,
                    MaxWidth = 42,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                return img;
            }

            // Fallback: show UpgradePack then VehicleName for diagnosis
            string raw = !string.IsNullOrEmpty(upgradePack) ? upgradePack : vehicleName;
            string abbr = string.IsNullOrEmpty(raw) ? "---"
                : raw.Length > 10 ? raw[..10] : raw;
            return new Border
            {
                Background = BrushCache.Get(40, 55, 60),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = abbr, FontSize = 6, FontFamily = _consolas, Foreground = BrushCache.Get(160, 180, 185), HorizontalAlignment = HorizontalAlignment.Center }
            };
        }

        private static string? DetectManufacturer(string raw)
        {
            string v = (raw ?? "").ToLowerInvariant();

            // --- Nom de voiture / marque directe ---
            if (v.Contains("ferrari"))                          return "ferrari";
            if (v.Contains("porsche"))                          return "porsche";
            if (v.Contains("toyota"))                           return "toyota";
            if (v.Contains("peugeot"))                          return "peugeot";
            if (v.Contains("bmw"))                              return "bmw";
            if (v.Contains("cadillac"))                         return "cadillac";
            if (v.Contains("alpine"))                           return "alpine";
            if (v.Contains("lamborghini"))                      return "lamborghini";
            if (v.Contains("isotta"))                           return "isotta";
            if (v.Contains("mclaren"))                          return "mclaren";
            if (v.Contains("aston"))                            return "astonmartin";
            if (v.Contains("ford"))                             return "ford";
            if (v.Contains("mercedes") || v.Contains("amg"))   return "mercedes";
            if (v.Contains("chevrolet") || v.Contains("corvette")) return "chevrolet";
            if (v.Contains("acura"))                            return "acura";

            // --- Équipes LMU connues : Ferrari ---
            if (v.Contains("af corse") || v.Contains("afcorse"))    return "ferrari";
            if (v.Contains("iron lynx"))                             return "ferrari";
            if (v.Contains("iron dames"))                            return "ferrari";
            if (v.Contains("kessel"))                                return "ferrari";
            if (v.Contains("spirit of"))                             return "ferrari";
            if (v.Contains("racing spirit"))                         return "ferrari";
            if (v.Contains("racing spi"))                            return "ferrari";
            if (v.Contains("jmw"))                                   return "ferrari";
            if (v.Contains("richard mille") || v.Contains("richard mi")) return "ferrari";
            if (v.Contains("risi"))                                  return "ferrari";
            if (v.Contains("sky tempesta"))                          return "ferrari";
            if (v.Contains("cetilar"))                               return "ferrari";
            if (v.Contains("vista"))                                 return "ferrari";

            // --- Équipes LMU connues : Porsche ---
            if (v.Contains("gr racing") || v.Contains("gr-racing"))  return "porsche";
            if (v.Contains("proton"))                                 return "porsche";
            if (v.Contains("manthey"))                               return "porsche";
            if (v.Contains("absolute"))                              return "porsche";
            if (v.Contains("dinamic"))                               return "porsche";
            if (v.Contains("rutronik"))                              return "porsche";
            if (v.Contains("herberth"))                              return "porsche";
            if (v.Contains("penske"))                                return "porsche";
            if (v.Contains("estre"))                                 return "porsche";

            // --- Équipes LMU connues : BMW ---
            if (v.Contains("wrt"))                                   return "bmw";
            if (v.Contains("walkenhorst"))                           return "bmw";
            if (v.Contains("schubert"))                              return "bmw";
            if (v.Contains("bmw m team"))                            return "bmw";

            // --- Équipes LMU connues : McLaren ---
            if (v.Contains("united autosports") || v.Contains("united auto")) return "mclaren";
            if (v.Contains("jota"))                                  return "mclaren";
            if (v.Contains("garage 59") || v.Contains("garage59"))  return "mclaren";
            if (v.Contains("inception"))                             return "mclaren";

            // --- Équipes LMU connues : Lamborghini ---
            if (v.Contains("emil frey"))                             return "lamborghini";
            if (v.Contains("grasser"))                               return "lamborghini";
            if (v.Contains("t3 motorsport") || v.Contains("t3motor")) return "lamborghini";
            if (v.Contains("huracán") || v.Contains("huracan"))      return "lamborghini";
            if (v.Contains("sc63"))                                  return "lamborghini";

            // --- Équipes LMU connues : Aston Martin ---
            if (v.Contains("tf sport") || v.Contains("tfsport"))    return "astonmartin";
            if (v.Contains("beechdean"))                             return "astonmartin";
            if (v.Contains("heart of racing"))                       return "astonmartin";
            if (v.Contains("d'station"))                             return "astonmartin";
            if (v.Contains("nismo"))                                 return "astonmartin"; // Nismo runs AMR

            // --- Équipes LMU connues : Toyota ---
            if (v.Contains("gazoo") || v.Contains("toyota gazoo"))  return "toyota";
            if (v.Contains("gr010"))                                 return "toyota";

            // --- Équipes LMU connues : Peugeot ---
            if (v.Contains("totalenergies") || v.Contains("total en")) return "peugeot";
            if (v.Contains("9x8"))                                   return "peugeot";

            // --- Équipes LMU connues : Cadillac ---
            if (v.Contains("chip ganassi") || v.Contains("ganassi")) return "cadillac";
            if (v.Contains("action express"))                        return "cadillac";
            if (v.Contains("v-series") || v.Contains("vseries"))    return "cadillac";

            // --- Équipes LMU connues : Ford ---
            if (v.Contains("multimatic"))                            return "ford";
            if (v.Contains("mustang"))                               return "ford";
            if (v.Contains("m-sport"))                               return "ford";

            return null;
        }

        private static void P(Grid g, int col, UIElement el)
        {
            if (col < g.ColumnDefinitions.Count) { Grid.SetColumn(el, col); g.Children.Add(el); }
        }

        private static Color TireColor(string compound)
        {
            string c = (compound ?? "").ToLowerInvariant();
            if (c.Contains("wet") || c.Contains("rain") || c.Contains("inter")) return Color.FromRgb(0, 120, 255);
            if (c.Contains("hard"))   return Color.FromRgb(220, 220, 220);
            if (c.Contains("medium")) return Color.FromRgb(255, 200, 0);
            if (c.Contains("soft"))   return Color.FromRgb(220, 30, 30);
            if (c.Contains("super"))  return Color.FromRgb(220, 30, 30); // supersoft
            return Color.FromArgb(60, 130, 150, 150); // unknown
        }

        private static Border SD(SectorStatus s, bool cur)
        {
            Color bg = s switch { SectorStatus.SessionBest => Color.FromRgb(168, 85, 247), SectorStatus.PersonalBest => Color.FromRgb(76, 217, 100), SectorStatus.Slower => Color.FromRgb(255, 204, 0), _ => Color.FromArgb(30, 90, 120, 120) };
            return new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(2), Background = BrushCache.Get(bg), Margin = new Thickness(1, 0, 1, 0), BorderThickness = cur ? new Thickness(1) : new Thickness(0), BorderBrush = cur ? Brushes.White : null };
        }

        private static string Classify(string vc) { string c = (vc ?? "").ToUpperInvariant(); if (c.Contains("HYPERCAR") || c.Contains("LMH") || c.Contains("LMDH")) return "HYPERCAR"; if (c.Contains("LMP2")) return "LMP2"; if (c.Contains("LMP3")) return "LMP3"; if (c.Contains("GTE") || c.Contains("LMGT")) return "LMGTE"; if (c.Contains("GT3")) return "GT3"; if (c.Contains("GT4")) return "GT4"; return string.IsNullOrEmpty(vc) ? "OTHER" : vc.ToUpperInvariant(); }
        private static int ClassOrd(string c) => c switch { "HYPERCAR" => 0, "LMP2" => 1, "LMP3" => 2, "LMGTE" => 3, "GT3" => 4, "GT4" => 5, _ => 9 };
        private static Color ClassCol(string c) => c switch { "HYPERCAR" => Color.FromRgb(180, 20, 0), "LMP2" => Color.FromRgb(0, 100, 200), "LMP3" => Color.FromRgb(140, 140, 0), "LMGTE" => Color.FromRgb(0, 140, 50), "GT3" => Color.FromRgb(180, 120, 0), "GT4" => Color.FromRgb(100, 60, 140), _ => Color.FromRgb(80, 80, 80) };
        private static string FT(double t) { if (t <= 0) return "-:--.---"; var s = TimeSpan.FromSeconds(t); return $"{(int)s.TotalMinutes}:{s.Seconds:D2}.{s.Milliseconds:D3}"; }
        private static TextBlock T(string text, double sz, SolidColorBrush fg, HorizontalAlignment ha = HorizontalAlignment.Left) => new() { Text = text, FontSize = sz, FontFamily = _consolas, Foreground = fg, HorizontalAlignment = ha, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        private static SolidColorBrush B(byte r, byte g, byte b) => BrushCache.Get(r, g, b);

        private static int ComputeStandingsHash(List<VehicleData> vehicles)
        {
            var h = new HashCode();
            foreach (var v in vehicles)
            {
                h.Add(v.Position);
                h.Add(v.TotalLaps);
                h.Add((int)(v.TimeBehindNext * 1000));
                h.Add((int)(v.BestLapTime * 1000));
                h.Add(v.InPits);
                h.Add(v.Flag);
                h.Add(v.NumPitstops);
                h.Add(v.FrontTireCompound);
                h.Add(v.RearTireCompound);
            }
            return h.ToHashCode();
        }
    }
}
