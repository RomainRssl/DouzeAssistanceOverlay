using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class DashboardOverlay : BaseOverlayWindow
    {
        private readonly DashboardDisplayConfig _cfg;
        private readonly TextBlock _gearText;

        // Speed bar
        private readonly Border _speedBarFill, _speedBarBg;
        private readonly TextBlock _speedValueText;
        private const double MAX_SPEED = 350; // km/h

        // RPM bar
        private readonly Border _rpmBarFill, _rpmBarBg;
        private readonly TextBlock _rpmValueText;
        private int _flashCounter;

        // Pit limiter blink
        private int _pitFlashCounter;

        // Info cells
        private readonly Dictionary<string, (Border Cell, TextBlock Value)> _cells = new();

        // Special refs for pit-limiter LAP cell
        private Border? _lapCellBorder;
        private TextBlock? _lapLabel;
        private TextBlock? _lapValue;

        public DashboardOverlay(DataService ds, OverlaySettings s, DashboardDisplayConfig cfg) : base(ds, s)
        {
            _cfg = cfg;

            var border = OverlayHelper.MakeBorder();
            var mainStack = new StackPanel();

            // ================================================================
            // SPEED BAR (top)
            // ================================================================
            _speedBarBg = new Border
            {
                Height = 18, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(15, 35, 35)),
                Margin = new Thickness(0, 0, 0, 2),
                ClipToBounds = true
            };
            var speedGrid = new Grid();
            _speedBarFill = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(0, 180, 160)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            speedGrid.Children.Add(_speedBarFill);
            _speedValueText = new TextBlock
            {
                FontSize = 11, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            speedGrid.Children.Add(_speedValueText);
            _speedBarBg.Child = speedGrid;
            mainStack.Children.Add(_speedBarBg);

            // ================================================================
            // RPM BAR
            // ================================================================
            _rpmBarBg = new Border
            {
                Height = 18, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(20, 40, 40)),
                Margin = new Thickness(0, 0, 0, 2),
                ClipToBounds = true
            };
            var rpmGrid = new Grid();
            _rpmBarFill = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(OverlayHelper.AccBlue),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            rpmGrid.Children.Add(_rpmBarFill);
            _rpmValueText = new TextBlock
            {
                FontSize = 11, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            rpmGrid.Children.Add(_rpmValueText);
            _rpmBarBg.Child = rpmGrid;
            mainStack.Children.Add(_rpmBarBg);

            // ================================================================
            // MAIN GRID: 2 rows × 7 columns
            // ================================================================
            var grid = new Grid();
            for (int i = 0; i < 7; i++)
                grid.ColumnDefinitions.Add(i == 3
                    ? new ColumnDefinition { Width = new GridLength(70) }
                    : new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            // GEAR (center, spans 2 rows)
            var gearBorder = new Border
            {
                Background = new SolidColorBrush(OverlayHelper.BgAccent),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2)
            };
            _gearText = new TextBlock
            {
                Text = "N", FontSize = 42, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(OverlayHelper.TextGear),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            gearBorder.Child = _gearText;
            Grid.SetColumn(gearBorder, 3); Grid.SetRow(gearBorder, 0); Grid.SetRowSpan(gearBorder, 2);
            grid.Children.Add(gearBorder);

            // --- ROW 0: POS | FUEL | FUEL/LAP | [GEAR] | FUEL LEFT | LAPS LEFT | LAP ---
            Cell(grid, "ShowPosition",    "POS",       0, 0, OverlayHelper.BgCell);
            Cell(grid, "ShowFuel",        "FUEL",      0, 1, OverlayHelper.BgCellAlt);
            Cell(grid, "ShowFuelPerLap",  "FUEL/LAP",  0, 2, OverlayHelper.BgCellAlt);
            Cell(grid, "ShowTimeRemaining","FUEL LEFT", 0, 4, OverlayHelper.BgCellAlt);
            Cell(grid, "ShowLapsRemaining","LAPS LEFT", 0, 5, OverlayHelper.BgCell);
            CellLap(grid,                              0, 6, OverlayHelper.BgCell);

            // --- ROW 1: ENERGY | ENRG/LAP | WATER | [GEAR] | OIL | OVERHEAT | ABS ---
            Cell(grid, "ShowEnergy",      "ENERGY",    1, 0, OverlayHelper.BgBlue);
            Cell(grid, "ShowEnergyPerLap","ENRG/LAP",  1, 1, OverlayHelper.BgBlue);
            Cell(grid, "ShowWaterTemp",   "WATER",     1, 2, OverlayHelper.BgCellAlt);
            Cell(grid, "ShowOilTemp",     "OIL",       1, 4, OverlayHelper.BgOrange);
            Cell(grid, "ShowOverheating", "OVERHEAT",  1, 5, OverlayHelper.BgRed);
            Cell(grid, "ShowABS",         "ABS",       1, 6, OverlayHelper.BgRed);

            mainStack.Children.Add(grid);
            border.Child = mainStack;
            Content = border;
        }

        private void Cell(Grid grid, string cfgKey, string label, int row, int col, Color bg)
        {
            var cellBorder = OverlayHelper.MakeCell(bg);
            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(OverlayHelper.MakeLabel(label));
            var val = OverlayHelper.MakeValue(14);
            sp.Children.Add(val);
            cellBorder.Child = sp;

            Grid.SetRow(cellBorder, row); Grid.SetColumn(cellBorder, col);
            grid.Children.Add(cellBorder);
            _cells[cfgKey] = (cellBorder, val);
        }

        /// <summary>Special LAP cell that can flash "PIT" when pit limiter is active.</summary>
        private void CellLap(Grid grid, int row, int col, Color bg)
        {
            _lapCellBorder = OverlayHelper.MakeCell(bg);
            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _lapLabel = OverlayHelper.MakeLabel("LAP");
            _lapValue = OverlayHelper.MakeValue(14);
            sp.Children.Add(_lapLabel);
            sp.Children.Add(_lapValue);
            _lapCellBorder.Child = sp;

            Grid.SetRow(_lapCellBorder, row); Grid.SetColumn(_lapCellBorder, col);
            grid.Children.Add(_lapCellBorder);
            _cells["ShowLap"] = (_lapCellBorder, _lapValue);
        }

        public override void UpdateData()
        {
            var d = DataService.GetDashboardData();

            // ---- GEAR ----
            _gearText.Text = d.Gear switch { -1 => "R", 0 => "N", _ => d.Gear.ToString() };

            // ---- SPEED BAR ----
            double speedPct = Math.Clamp(d.Speed / MAX_SPEED, 0, 1);
            double speedBarW = _speedBarBg.ActualWidth > 0 ? _speedBarBg.ActualWidth * speedPct : 0;
            _speedBarFill.Width = Math.Max(0, speedBarW);
            _speedValueText.Text = $"{d.Speed:F0} KM/H";

            Color speedCol = d.Speed > 300 ? Color.FromRgb(255, 59, 48) :
                             d.Speed > 200 ? Color.FromRgb(0, 200, 170) :
                             Color.FromRgb(0, 150, 130);
            _speedBarFill.Background = new SolidColorBrush(speedCol);
            _speedBarBg.Visibility = _cfg.ShowSpeed ? Visibility.Visible : Visibility.Collapsed;

            // ---- RPM BAR ----
            double rpmPct = d.MaxRPM > 0 ? Math.Clamp(d.RPM / d.MaxRPM, 0, 1) : 0;
            double rpmBarW = _rpmBarBg.ActualWidth > 0 ? _rpmBarBg.ActualWidth * rpmPct : 0;
            _rpmBarFill.Width = Math.Max(0, rpmBarW);
            _rpmValueText.Text = $"{d.RPM:F0} RPM";
            _rpmBarBg.Visibility = _cfg.ShowRPM ? Visibility.Visible : Visibility.Collapsed;

            if (rpmPct > 0.97)
            {
                _flashCounter++;
                bool on = (_flashCounter / 3) % 2 == 0;
                _rpmBarFill.Background = new SolidColorBrush(on ? OverlayHelper.AccRed : Color.FromRgb(80, 0, 0));
                _rpmBarBg.Background = new SolidColorBrush(on ? Color.FromRgb(60, 10, 10) : Color.FromRgb(20, 40, 40));
                _rpmValueText.Foreground = new SolidColorBrush(on ? Colors.White : Color.FromRgb(200, 80, 80));
            }
            else
            {
                _flashCounter = 0;
                Color barCol = rpmPct > 0.90 ? OverlayHelper.AccRed :
                               rpmPct > 0.80 ? OverlayHelper.AccYellow :
                               OverlayHelper.AccBlue;
                _rpmBarFill.Background = new SolidColorBrush(barCol);
                _rpmBarBg.Background = new SolidColorBrush(Color.FromRgb(20, 40, 40));
                _rpmValueText.Foreground = Brushes.White;
            }

            // ---- CELLS ----
            Set("ShowPosition", $"P{d.Position}");

            // ---- LAP cell — flashes "PIT" when pit limiter is active ----
            if (_lapCellBorder != null && _lapLabel != null && _lapValue != null)
            {
                bool lapVisible = _cfg.ShowLap;
                _lapCellBorder.Visibility = lapVisible ? Visibility.Visible : Visibility.Collapsed;
                if (lapVisible)
                {
                    if (d.PitLimiter)
                    {
                        _pitFlashCounter++;
                        bool pitOn = (_pitFlashCounter / 4) % 2 == 0;
                        _lapCellBorder.Background = new SolidColorBrush(
                            pitOn ? Color.FromRgb(0, 60, 30) : Color.FromRgb(0, 20, 10));
                        _lapLabel.Text = "PIT";
                        _lapLabel.Foreground = new SolidColorBrush(
                            pitOn ? Color.FromRgb(0, 255, 130) : Color.FromRgb(0, 100, 60));
                        _lapValue.Text = "LIMITER";
                        _lapValue.FontSize = 9;
                        _lapValue.Foreground = new SolidColorBrush(
                            pitOn ? Color.FromRgb(0, 220, 110) : Color.FromRgb(0, 80, 50));
                    }
                    else
                    {
                        _pitFlashCounter = 0;
                        _lapCellBorder.Background = new SolidColorBrush(OverlayHelper.BgCell);
                        _lapLabel.Text = "LAP";
                        _lapLabel.Foreground = new SolidColorBrush(OverlayHelper.TextSecondary);
                        _lapValue.Text = $"{d.TotalLaps}";
                        _lapValue.FontSize = 14;
                        _lapValue.Foreground = new SolidColorBrush(OverlayHelper.TextValue);
                    }
                }
            }

            if (SetVis("ShowFuel"))
            {
                var c = _cells["ShowFuel"];
                c.Value.Text = $"{d.Fuel:F1}L";
                double pct = d.FuelCapacity > 0 ? d.Fuel / d.FuelCapacity : 0;
                c.Value.Foreground = new SolidColorBrush(
                    pct > 0.3 ? OverlayHelper.AccGreen :
                    pct > 0.15 ? OverlayHelper.AccYellow : OverlayHelper.AccRed);
            }

            Set("ShowFuelPerLap", d.FuelPerLap > 0 ? $"{d.FuelPerLap:F2}" : "0.00");

            if (SetVis("ShowTimeRemaining"))
            {
                var c = _cells["ShowTimeRemaining"];
                if (d.TimeRemaining > 0)
                {
                    var ts = TimeSpan.FromSeconds(d.TimeRemaining);
                    c.Value.Text = ts.TotalHours >= 1
                        ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                        : $"{ts.Minutes}:{ts.Seconds:D2}";
                }
                else c.Value.Text = "0:00";
            }

            Set("ShowLapsRemaining", $"{d.LapsRemaining:F1}");

            if (SetVis("ShowEnergy"))
            {
                var c = _cells["ShowEnergy"];
                c.Value.Text = $"{d.Energy:F0}%";
                c.Value.Foreground = new SolidColorBrush(
                    d.Energy > 50 ? OverlayHelper.AccGreen :
                    d.Energy > 20 ? OverlayHelper.AccYellow : OverlayHelper.AccRed);
            }

            Set("ShowEnergyPerLap", d.EnergyPerLap > 0 ? $"{d.EnergyPerLap:F1}%" : "--");
            Set("ShowABS", $"{d.ABS}");

            // ---- WATER TEMP ----
            if (SetVis("ShowWaterTemp"))
            {
                var c = _cells["ShowWaterTemp"];
                c.Value.Text = d.WaterTemp > 0 ? $"{d.WaterTemp:F0}°C" : "--";
                c.Value.Foreground = new SolidColorBrush(
                    d.WaterTemp > 110 ? OverlayHelper.AccRed :
                    d.WaterTemp > 100 ? OverlayHelper.AccYellow :
                    OverlayHelper.AccGreen);
            }

            // ---- OIL TEMP ----
            if (SetVis("ShowOilTemp"))
            {
                var c = _cells["ShowOilTemp"];
                c.Value.Text = d.OilTemp > 0 ? $"{d.OilTemp:F0}°C" : "--";
                c.Value.Foreground = new SolidColorBrush(
                    d.OilTemp > 140 ? OverlayHelper.AccRed :
                    d.OilTemp > 130 ? OverlayHelper.AccYellow :
                    OverlayHelper.AccGreen);
            }

            // ---- OVERHEATING ----
            if (SetVis("ShowOverheating"))
            {
                var c = _cells["ShowOverheating"];
                if (d.Overheating)
                {
                    c.Value.Text = "⚠ HOT";
                    c.Value.Foreground = new SolidColorBrush(OverlayHelper.AccRed);
                    c.Cell.Background = new SolidColorBrush(Color.FromArgb(180, 100, 0, 0));
                }
                else
                {
                    c.Value.Text = "OK";
                    c.Value.Foreground = new SolidColorBrush(OverlayHelper.AccGreen);
                    c.Cell.Background = new SolidColorBrush(OverlayHelper.BgRed);
                }
            }
        }

        private void Set(string key, string text)
        {
            if (_cells.TryGetValue(key, out var c))
            {
                c.Value.Text = text;
                c.Cell.Visibility = _cfg.IsVisible(key) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool SetVis(string key)
        {
            if (_cells.TryGetValue(key, out var c))
            {
                bool vis = _cfg.IsVisible(key);
                c.Cell.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
                return vis;
            }
            return false;
        }
    }
}
