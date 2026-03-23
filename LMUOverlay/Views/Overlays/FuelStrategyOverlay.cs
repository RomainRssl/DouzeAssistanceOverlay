using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class FuelStrategyOverlay : BaseOverlayWindow
    {
        private readonly Border _fuelBar, _fuelBarBg, _energyBar, _energyBarBg;
        private readonly TextBlock _fuelBarText, _energyBarText;
        private readonly Border _windowBorder;
        private readonly TextBlock _windowStateText, _windowDetailText;
        private readonly TextBlock _fuelToAdd, _autonomyText, _limiterText;
        private readonly TextBlock _fuelPerLap, _energyPerLap, _raceLaps;
        private readonly TextBlock _maxStint, _stopsLeft, _compound;
        private readonly TextBlock _tireWear, _tireLapsLeft;
        private int _flashCounter;

        public FuelStrategyOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var border = OverlayHelper.MakeBorder();
            var main = new StackPanel();
            main.Children.Add(OverlayHelper.MakeTitle("FUEL & STRATEGY"));

            // FUEL BAR
            _fuelBarBg = BarBg(Color.FromRgb(15, 35, 35));
            var fg = new Grid();
            _fuelBar = new Border { CornerRadius = new CornerRadius(3), Background = B(0, 190, 170), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            fg.Children.Add(_fuelBar);
            _fuelBarText = BT();
            fg.Children.Add(_fuelBarText);
            _fuelBarBg.Child = fg;
            main.Children.Add(_fuelBarBg);

            // ENERGY BAR
            _energyBarBg = BarBg(Color.FromRgb(20, 15, 35));
            var eg = new Grid();
            _energyBar = new Border { CornerRadius = new CornerRadius(3), Background = B(130, 80, 220), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            eg.Children.Add(_energyBar);
            _energyBarText = BT();
            eg.Children.Add(_energyBarText);
            _energyBarBg.Child = eg;
            main.Children.Add(_energyBarBg);

            // PIT WINDOW
            _windowBorder = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 3, 0, 2) };
            var ws = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _windowStateText = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Center };
            _windowDetailText = new TextBlock { FontSize = 8, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Center };
            ws.Children.Add(_windowStateText); ws.Children.Add(_windowDetailText);
            _windowBorder.Child = ws;
            main.Children.Add(_windowBorder);

            // FUEL TO ADD
            var fr = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 3) };
            fr.Children.Add(L("CARBURANT À AJOUTER"));
            _fuelToAdd = new TextBlock { FontSize = 20, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = B(255, 204, 0), HorizontalAlignment = HorizontalAlignment.Center };
            fr.Children.Add(_fuelToAdd);
            main.Children.Add(fr);

            // AUTONOMY
            var ar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 3) };
            ar.Children.Add(L("AUTONOMIE  "));
            _autonomyText = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
            ar.Children.Add(_autonomyText);
            _limiterText = new TextBlock { FontSize = 9, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            ar.Children.Add(_limiterText);
            main.Children.Add(ar);

            // SEPARATOR
            main.Children.Add(new Border { Height = 1, Background = B(36, 68, 68), Margin = new Thickness(0, 2, 0, 2) });

            // INFO GRID
            var g = new Grid { Margin = new Thickness(2) };
            for (int i = 0; i < 3; i++) g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < 3; i++) g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

            C(g, "FUEL/TOUR", out _fuelPerLap, 0, 0);
            C(g, "ENRG/TOUR", out _energyPerLap, 0, 1);
            C(g, "TOURS COURSE", out _raceLaps, 0, 2);
            C(g, "MAX RELAIS", out _maxStint, 1, 0);
            C(g, "ARRÊTS", out _stopsLeft, 1, 1);
            C(g, "GOMME", out _compound, 1, 2);
            C(g, "USURE", out _tireWear, 2, 0);
            C(g, "PNEUS REST.", out _tireLapsLeft, 2, 1);
            main.Children.Add(g);

            border.Child = main;
            Content = border;
        }

        public override void UpdateData()
        {
            var f = DataService.GetFuelData();
            var p = DataService.GetPitStrategyData();

            double barW = _fuelBarBg.ActualWidth > 0 ? _fuelBarBg.ActualWidth : 200;

            // FUEL BAR
            double fPct = f.FuelCapacity > 0 ? Math.Clamp(f.CurrentFuel / f.FuelCapacity, 0, 1) : 0;
            _fuelBar.Width = Math.Max(0, fPct * barW);
            _fuelBarText.Text = $"⛽ {f.CurrentFuel:F1}L / {f.FuelCapacity:F0}L";
            _fuelBar.Background = new SolidColorBrush(fPct > 0.3 ? Color.FromRgb(0, 190, 170) : fPct > 0.15 ? Color.FromRgb(255, 180, 0) : Color.FromRgb(255, 50, 40));

            // ENERGY BAR
            bool hasHybrid = f.CurrentEnergy > 0.1 || f.EnergyPerLap > 0.01 || f.ValidEnergySamples > 0;
            if (hasHybrid)
            {
                double ePct = Math.Clamp(f.CurrentEnergy / 100.0, 0, 1);
                _energyBar.Width = Math.Max(0, ePct * barW);
                _energyBarText.Text = $"⚡ {f.CurrentEnergy:F0}%";
                _energyBar.Background = new SolidColorBrush(ePct > 0.4 ? Color.FromRgb(130, 80, 220) : ePct > 0.2 ? Color.FromRgb(200, 140, 40) : Color.FromRgb(255, 50, 40));

                if (f.Limiter == LimitingFactor.Energy && f.EnergyAutonomy < 3)
                {
                    _flashCounter++;
                    bool on = (_flashCounter / 4) % 2 == 0;
                    _energyBarBg.BorderBrush = new SolidColorBrush(on ? Color.FromRgb(200, 50, 255) : Colors.Transparent);
                    _energyBarBg.BorderThickness = new Thickness(on ? 1.5 : 0);
                }
                else { _energyBarBg.BorderBrush = null; _energyBarBg.BorderThickness = new Thickness(0); }
            }
            else
            {
                _energyBar.Width = 0;
                _energyBarText.Text = "⚡ N/A";
                _energyBarBg.Background = new SolidColorBrush(Color.FromRgb(25, 25, 30));
                _energyBarBg.BorderBrush = null;
                _energyBarBg.BorderThickness = new Thickness(0);
            }

            // PIT WINDOW
            switch (f.WindowState)
            {
                case PitWindowState.Critical:
                    _flashCounter++;
                    bool c = (_flashCounter / 3) % 2 == 0;
                    _windowBorder.Background = new SolidColorBrush(c ? Color.FromRgb(200, 30, 30) : Color.FromRgb(80, 10, 10));
                    _windowStateText.Text = "BOX NOW"; _windowStateText.Foreground = Brushes.White;
                    _windowDetailText.Text = "Panne imminente !"; _windowDetailText.Foreground = B(255, 180, 180);
                    break;
                case PitWindowState.WindowOpen:
                    _windowBorder.Background = new SolidColorBrush(Color.FromRgb(27, 94, 32));
                    _windowStateText.Text = "FENÊTRE OUVERTE"; _windowStateText.Foreground = B(76, 255, 100);
                    _windowDetailText.Text = $"Box dans les {f.WindowClose:F0} tours"; _windowDetailText.Foreground = B(160, 220, 160);
                    break;
                case PitWindowState.TooEarly:
                    _windowBorder.Background = new SolidColorBrush(Color.FromRgb(40, 50, 55));
                    _windowStateText.Text = "TROP TÔT"; _windowStateText.Foreground = B(150, 160, 170);
                    _windowDetailText.Text = $"Attendre {f.WindowOpen} tours"; _windowDetailText.Foreground = B(100, 110, 120);
                    break;
                default:
                    _windowBorder.Background = new SolidColorBrush(Color.FromRgb(30, 40, 40));
                    _windowStateText.Text = "EN ATTENTE"; _windowStateText.Foreground = B(100, 110, 110);
                    _windowDetailText.Text = $"{f.ValidFuelSamples}/2 tours"; _windowDetailText.Foreground = B(80, 90, 90);
                    break;
            }

            _fuelToAdd.Text = f.FuelPerLap > 0 ? $"{f.FuelToAdd:F1} L" : "-- L";

            // AUTONOMY
            if (f.RealAutonomy > 0)
            {
                _autonomyText.Text = $"{f.RealAutonomy:F1} tours";
                _autonomyText.Foreground = new SolidColorBrush(f.RealAutonomy < 3 ? Color.FromRgb(255, 59, 48) : f.RealAutonomy < 5 ? Color.FromRgb(255, 204, 0) : Color.FromRgb(76, 217, 100));
            }
            else { _autonomyText.Text = "--"; _autonomyText.Foreground = B(100, 120, 120); }

            _limiterText.Text = f.Limiter switch { LimitingFactor.Fuel => "FUEL ⚠", LimitingFactor.Energy => "ENRG ⚠", LimitingFactor.Balanced => "= OK", _ => "" };
            _limiterText.Foreground = new SolidColorBrush(f.Limiter switch { LimitingFactor.Fuel => Color.FromRgb(0, 200, 180), LimitingFactor.Energy => Color.FromRgb(160, 100, 255), LimitingFactor.Balanced => Color.FromRgb(76, 217, 100), _ => Color.FromRgb(80, 80, 80) });
            // Hide limiter for non-hybrid cars (fuel is always the only factor)
            if (!hasHybrid) _limiterText.Text = "";

            // INFO
            _fuelPerLap.Text = f.FuelPerLap > 0 ? $"{f.FuelPerLap:F2} L" : "-- L";
            _energyPerLap.Text = hasHybrid && f.EnergyPerLap > 0 ? $"{f.EnergyPerLap:F1}%" : hasHybrid ? "--%" : "N/A";
            _raceLaps.Text = f.RaceLapsRemaining > 0 ? $"{f.RaceLapsRemaining}" : "--";
            _maxStint.Text = f.MaxStintLaps > 0 && f.MaxStintLaps < 999 ? $"{f.MaxStintLaps} t" : "--";
            _stopsLeft.Text = $"{f.StopsRequired}";
            _stopsLeft.Foreground = new SolidColorBrush(f.StopsRequired == 0 ? Color.FromRgb(76, 217, 100) : Color.FromRgb(255, 204, 0));
            _compound.Text = !string.IsNullOrEmpty(p.TireCompound) ? p.TireCompound : "--";
            _tireWear.Text = $"{p.TireWearAvg * 100:F0}%";
            _tireWear.Foreground = new SolidColorBrush(p.TireWearAvg > 0.5 ? Color.FromRgb(76, 217, 100) : p.TireWearAvg > 0.25 ? Color.FromRgb(255, 204, 0) : Color.FromRgb(255, 59, 48));
            _tireLapsLeft.Text = p.TireLapsLeft < 999 ? $"{p.TireLapsLeft} t" : "--";
        }

        static Border BarBg(Color c) => new() { Height = 15, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(c), Margin = new Thickness(0, 1, 0, 1), ClipToBounds = true };
        static TextBlock BT() => new() { FontSize = 9, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        static TextBlock L(string t) => new() { Text = t, FontSize = 7, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Consolas"), Foreground = B(80, 110, 110), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        static void C(Grid g, string l, out TextBlock v, int r, int c) { var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; s.Children.Add(L(l)); v = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = B(230, 237, 243), HorizontalAlignment = HorizontalAlignment.Center }; s.Children.Add(v); Grid.SetRow(s, r); Grid.SetColumn(s, c); g.Children.Add(s); }
        static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
