using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class FuelStrategyOverlay : BaseOverlayWindow
    {
        // Fuel bar
        private readonly Border _fuelFill, _fuelBarBg;
        private readonly TextBlock _fuelBarText;

        // Energy bar + container (hidden for non-VE cars)
        private readonly Border _energyFill, _energyBarBg;
        private readonly TextBlock _energyBarText;
        private readonly FrameworkElement _energyBarContainer;

        // Main info
        private readonly TextBlock _fuelToAdd, _autonomyText, _limiterText;
        private readonly TextBlock _fuelPerLap, _energyPerLap, _raceLaps;
        private readonly TextBlock _maxStint, _stopsLeft, _compound;
        private readonly TextBlock _tireWear, _tireLapsLeft;

        // VE-to-add block (VE cars only)
        private readonly StackPanel _energyToAddBlock;
        private readonly TextBlock _energyToAdd;

        // Pit distance
        private readonly Border     _pitDistRow;
        private readonly TextBlock  _pitDistValue;
        private const double PIT_SHOW_DIST = 1000.0; // afficher si < 1 000 m ou pit demandé

        private int _flashCounter;

        public FuelStrategyOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var border = OverlayHelper.MakeBorder();
            var main = new StackPanel();
            main.Children.Add(OverlayHelper.MakeTitle("FUEL & STRATEGY"));

            // ── FUEL BAR ─────────────────────────────────────────────────────
            _fuelBarBg = MakeBarBg(Color.FromRgb(20, 35, 30));
            var fuelGrid = new Grid();
            _fuelFill = new Border
            {
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            fuelGrid.Children.Add(_fuelFill);
            _fuelBarText = MakeBarText();
            fuelGrid.Children.Add(_fuelBarText);
            _fuelBarBg.Child = fuelGrid;
            main.Children.Add(_fuelBarBg);

            // ── ENERGY BAR ───────────────────────────────────────────────────
            _energyBarBg = MakeBarBg(Color.FromRgb(10, 15, 35));
            var energyGrid = new Grid();
            _energyFill = new Border
            {
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            energyGrid.Children.Add(_energyFill);
            _energyBarText = MakeBarText();
            energyGrid.Children.Add(_energyBarText);
            _energyBarBg.Child = energyGrid;

            _energyBarContainer = _energyBarBg;
            _energyBarContainer.Visibility = Visibility.Collapsed;
            main.Children.Add(_energyBarContainer);

            // ── CARBURANT À AJOUTER ──────────────────────────────────────────
            var fuelAddBlock = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 2) };
            fuelAddBlock.Children.Add(L("CARBURANT À AJOUTER"));
            _fuelToAdd = new TextBlock { FontSize = 20, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, Foreground = BrushCache.Get(255, 204, 0), HorizontalAlignment = HorizontalAlignment.Center };
            fuelAddBlock.Children.Add(_fuelToAdd);
            main.Children.Add(fuelAddBlock);

            // ── ÉNERGIE À AJOUTER (VE only) ──────────────────────────────────
            _energyToAddBlock = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 2), Visibility = Visibility.Collapsed };
            _energyToAddBlock.Children.Add(L("ÉNERGIE À AJOUTER"));
            _energyToAdd = new TextBlock { FontSize = 20, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, HorizontalAlignment = HorizontalAlignment.Center };
            _energyToAddBlock.Children.Add(_energyToAdd);
            main.Children.Add(_energyToAddBlock);

            // ── AUTONOMY ─────────────────────────────────────────────────────
            var ar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) };
            ar.Children.Add(L("AUTONOMIE  "));
            _autonomyText = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, VerticalAlignment = VerticalAlignment.Center };
            ar.Children.Add(_autonomyText);
            _limiterText = new TextBlock { FontSize = 9, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            ar.Children.Add(_limiterText);
            main.Children.Add(ar);

            // ── DISTANCE ENTRÉE PIT ──────────────────────────────────────────
            var pitRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 3)
            };
            pitRow.Children.Add(new TextBlock
            {
                Text = "🏁 DIST. ENTRÉE PIT  ",
                FontSize = 8,
                FontFamily = OverlayHelper.FontConsolas,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushCache.Get(80, 110, 110),
                VerticalAlignment = VerticalAlignment.Center
            });
            _pitDistValue = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                VerticalAlignment = VerticalAlignment.Center
            };
            pitRow.Children.Add(_pitDistValue);
            _pitDistRow = new Border { Child = pitRow, Visibility = Visibility.Collapsed };
            main.Children.Add(_pitDistRow);

            // ── SEPARATOR ────────────────────────────────────────────────────
            main.Children.Add(new Border { Height = 1, Background = BrushCache.Get(36, 68, 68), Margin = new Thickness(0, 2, 0, 2) });

            // ── INFO GRID ────────────────────────────────────────────────────
            var g = new Grid { Margin = new Thickness(2) };
            for (int i = 0; i < 3; i++) g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < 3; i++) g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

            C(g, "FUEL/TOUR",    out _fuelPerLap,   0, 0);
            C(g, "ENRG/TOUR",   out _energyPerLap,  0, 1);
            C(g, "TOURS COURSE", out _raceLaps,      0, 2);
            C(g, "MAX RELAIS",   out _maxStint,      1, 0);
            C(g, "ARRÊTS",       out _stopsLeft,     1, 1);
            C(g, "GOMME",        out _compound,      1, 2);
            C(g, "USURE",        out _tireWear,      2, 0);
            C(g, "PNEUS REST.",  out _tireLapsLeft,  2, 1);
            main.Children.Add(g);

            border.Child = main;
            Content = border;
        }

        public override void UpdateData()
        {
            var f = DataService.GetFuelData();
            var p = DataService.GetPitStrategyData();

            double barW = _fuelBarBg.ActualWidth > 0 ? _fuelBarBg.ActualWidth : 200;

            // ── FUEL BAR ─────────────────────────────────────────────────────
            double fPct = f.FuelCapacity > 0 ? Math.Clamp(f.CurrentFuel / f.FuelCapacity, 0, 1) : 0;
            _fuelFill.Width = Math.Max(0, fPct * barW);
            _fuelBarText.Text = $"⛽  {f.CurrentFuel:F1} L  /  {f.FuelCapacity:F0} L";

            if (fPct <= 0.05)
            {
                _flashCounter++;
                bool on = (_flashCounter / 4) % 2 == 0;
                _fuelFill.Background = BrushCache.Get(on ? Color.FromRgb(255, 40, 40) : Color.FromRgb(120, 10, 10));
                _fuelBarBg.BorderBrush = on ? BrushCache.Get(255, 80, 80) : null;
                _fuelBarBg.BorderThickness = new Thickness(on ? 1.5 : 0);
            }
            else
            {
                _fuelBarBg.BorderBrush = null;
                _fuelBarBg.BorderThickness = new Thickness(0);
                _fuelFill.Background = fPct > 0.50
                    ? BrushCache.Get(30, 200, 100)
                    : fPct > 0.25
                        ? BrushCache.Get(255, 160, 0)
                        : BrushCache.Get(220, 50, 30);
            }

            // ── ENERGY BAR ───────────────────────────────────────────────────
            bool showVE = f.HasVirtualEnergy;
            _energyBarContainer.Visibility = showVE ? Visibility.Visible : Visibility.Collapsed;

            if (showVE)
            {
                double ePct = Math.Clamp(f.CurrentEnergy / 100.0, 0, 1);
                _energyFill.Width = Math.Max(0, ePct * barW);
                _energyBarText.Text = $"⚡  {f.CurrentEnergy:F0} %";

                if (ePct <= 0.05)
                {
                    _flashCounter++;
                    bool on = (_flashCounter / 4) % 2 == 0;
                    _energyFill.Background = BrushCache.Get(on ? Color.FromRgb(255, 40, 40) : Color.FromRgb(120, 10, 10));
                    _energyBarBg.BorderBrush = on ? BrushCache.Get(255, 80, 80) : null;
                    _energyBarBg.BorderThickness = new Thickness(on ? 1.5 : 0);
                }
                else
                {
                    _energyBarBg.BorderBrush = null;
                    _energyBarBg.BorderThickness = new Thickness(0);
                    _energyFill.Background = ePct > 0.50
                        ? BrushCache.Get(20, 60, 180)
                        : ePct > 0.25
                            ? BrushCache.Get(255, 160, 0)
                            : BrushCache.Get(220, 50, 30);
                }
            }

            // ── CARBURANT À AJOUTER ──────────────────────────────────────────
            bool fuelDataReady = f.FuelPerLap > 0 && f.ValidFuelSamples >= 2 && f.RaceLapsRemaining > 0;
            _fuelToAdd.Text = fuelDataReady ? $"{f.FuelToAdd:F1} L" : "-- L";

            // ── ÉNERGIE À AJOUTER ────────────────────────────────────────────
            bool veDataReady = showVE && f.EnergyToEnd > 0;
            _energyToAddBlock.Visibility = showVE ? Visibility.Visible : Visibility.Collapsed;
            if (showVE)
            {
                if (veDataReady)
                {
                    double toAdd = Math.Min(f.EnergyDeficit, 100.0);
                    bool hasDeficit = toAdd > 0;
                    _energyToAdd.Text = $"{toAdd:F0} %";
                    _energyToAdd.Foreground = hasDeficit
                        ? BrushCache.Get(0, 200, 255)       // cyan — VE à recharger
                        : BrushCache.Get(76, 217, 100);     // vert — VE suffisante
                }
                else
                {
                    _energyToAdd.Text = "-- %";
                    _energyToAdd.Foreground = BrushCache.Get(100, 120, 120);
                }
            }

            // ── AUTONOMY ─────────────────────────────────────────────────────
            if (f.RealAutonomy > 0)
            {
                _autonomyText.Text = $"{f.RealAutonomy:F1} tours";
                _autonomyText.Foreground = BrushCache.Get(
                    f.RealAutonomy < 3 ? Color.FromRgb(255, 59, 48) :
                    f.RealAutonomy < 5 ? Color.FromRgb(255, 204, 0) :
                    Color.FromRgb(76, 217, 100));
            }
            else
            {
                _autonomyText.Text = "--";
                _autonomyText.Foreground = BrushCache.Get(100, 120, 120);
            }

            // Limiter indicator: only show warning tags, never "= OK"
            _limiterText.Text = f.Limiter switch
            {
                LimitingFactor.Fuel   => "FUEL ⚠",
                LimitingFactor.Energy => "ENRG ⚠",
                _                     => ""
            };
            _limiterText.Foreground = BrushCache.Get(f.Limiter switch
            {
                LimitingFactor.Fuel   => Color.FromRgb(0, 200, 180),
                LimitingFactor.Energy => Color.FromRgb(160, 100, 255),
                _                     => Color.FromRgb(80, 80, 80)
            });

            // ── DISTANCE ENTRÉE PIT ──────────────────────────────────────────
            var (distToPit, pitState, inPits) = DataService.GetPitDistanceData();
            bool showPitDist = inPits || pitState >= 1 || (distToPit > 0 && distToPit <= PIT_SHOW_DIST);
            _pitDistRow.Visibility = showPitDist ? Visibility.Visible : Visibility.Collapsed;
            if (showPitDist)
            {
                // Get player position for pit states
                var allVeh = DataService.GetAllVehicles();
                var playerVeh = allVeh.FirstOrDefault(v => v.IsPlayer);
                // Format: "P22 (Cl7)" — uppercase CL avoids Consolas 'l' looking like '1'
                string posStr = playerVeh != null ? $"  P{playerVeh.Position} (CL{playerVeh.ClassPosition})" : "";

                if (inPits && pitState == 3)
                {
                    _pitDistValue.Text       = $"🔧 AU STAND{posStr}";
                    _pitDistValue.Foreground = BrushCache.Get(0, 210, 140);
                }
                else if (inPits || pitState == 2)
                {
                    _pitDistValue.Text       = $"⬛ EN STAND{posStr}";
                    _pitDistValue.Foreground = BrushCache.Get(0, 210, 140);
                }
                else if (pitState == 4)
                {
                    _pitDistValue.Text       = $"🚀 SORTIE{posStr}";
                    _pitDistValue.Foreground = BrushCache.Get(0, 180, 255);
                }
                else
                {
                    // Approche : distance countdown
                    string distTxt = distToPit >= 1000 ? $"{distToPit / 1000:F2} km"
                                   : distToPit >= 100  ? $"{distToPit:F0} m"
                                   :                     $"{distToPit:F1} m";

                    _pitDistValue.Text = distTxt;
                    _pitDistValue.Foreground = BrushCache.Get(
                        distToPit <= 150  ? Color.FromRgb(255,  59,  48) :   // rouge  — très proche
                        distToPit <= 400  ? Color.FromRgb(255, 204,   0) :   // jaune  — proche
                        pitState   >= 1   ? Color.FromRgb(  0, 180, 255) :   // bleu   — pit demandé
                                            Color.FromRgb(160, 200, 200));    // gris   — info neutre
                }
            }

            // ── INFO GRID ────────────────────────────────────────────────────
            _fuelPerLap.Text = f.FuelPerLap > 0 ? $"{f.FuelPerLap:F2} L" : "-- L";
            _energyPerLap.Text = showVE && f.EnergyPerLap > 0 ? $"{f.EnergyPerLap:F1}%" : showVE ? "--%": "N/A";
            _raceLaps.Text = f.RaceLapsRemaining > 0 ? $"{f.RaceLapsRemaining}" : "--";
            _maxStint.Text = f.MaxStintLaps > 0 && f.MaxStintLaps < 999 ? $"{f.MaxStintLaps} t" : "--";
            _stopsLeft.Text = $"{f.StopsRequired}";
            _stopsLeft.Foreground = BrushCache.Get(f.StopsRequired == 0 ? Color.FromRgb(76, 217, 100) : Color.FromRgb(255, 204, 0));
            _compound.Text = !string.IsNullOrEmpty(p.TireCompound) ? p.TireCompound : "--";
            _tireWear.Text = $"{p.TireWearAvg * 100:F0}%";
            _tireWear.Foreground = BrushCache.Get(p.TireWearAvg > 0.5 ? Color.FromRgb(76, 217, 100) : p.TireWearAvg > 0.25 ? Color.FromRgb(255, 204, 0) : Color.FromRgb(255, 59, 48));
            _tireLapsLeft.Text = p.TireLapsLeft < 999 ? $"{p.TireLapsLeft} t" : "--";
        }

        static Border MakeBarBg(Color c) => new()
        {
            Height = 18, MinWidth = 200, CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(c),
            Margin = new Thickness(0, 2, 0, 2),
            ClipToBounds = true
        };

        static TextBlock MakeBarText() => new()
        {
            FontSize = 9, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        static TextBlock L(string t) => new()
        {
            Text = t, FontSize = 7, FontWeight = FontWeights.SemiBold, FontFamily = OverlayHelper.FontConsolas,
            Foreground = BrushCache.Get(80, 110, 110),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };

        static void C(Grid g, string l, out TextBlock v, int r, int c)
        {
            var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            s.Children.Add(L(l));
            v = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, Foreground = BrushCache.Get(230, 237, 243), HorizontalAlignment = HorizontalAlignment.Center };
            s.Children.Add(v);
            Grid.SetRow(s, r); Grid.SetColumn(s, c);
            g.Children.Add(s);
        }
    }
}
