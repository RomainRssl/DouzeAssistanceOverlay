using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Circular dashboard overlay inspired by the LMU in-car display.
    /// Shows: warning icons, speed (large), gear, fuel bar (red), energy bar (blue).
    /// </summary>
    public class CompteurOverlay : BaseOverlayWindow
    {
        private const double DIAL_SIZE = 240;
        private const double MAX_SPEED = 350.0;

        // Warning icons row
        private readonly Ellipse _iconLights;
        private readonly Ellipse _iconABS;
        private readonly Ellipse _iconTC;
        private readonly Ellipse _iconPit;
        private readonly Ellipse _iconOil;

        // Center speed + gear
        private readonly TextBlock _speedText;
        private readonly TextBlock _gearText;
        private readonly TextBlock _kphLabel;
        private readonly TextBlock _ffbLabel;

        // Fuel bar
        private readonly Border _fuelBarFill;
        private readonly Border _fuelBarBg;
        private readonly TextBlock _fuelText;
        private readonly TextBlock _fuelLapsText;

        // Energy bar
        private readonly Border _eneBarFill;
        private readonly Border _eneBarBg;
        private readonly TextBlock _eneLabel;
        private readonly TextBlock _eneText;
        private readonly TextBlock _eneLapsText;

        // Dirty-check caches
        private int    _prevGear      = int.MinValue;
        private int    _prevSpeedInt  = -1;
        private double _prevFuelPct   = -1;
        private double _prevEnePct    = -1;
        private string _prevFuelText  = "";
        private string _prevEneText   = "";
        private string _prevFuelLaps  = "";
        private string _prevEneLaps   = "";
        private bool   _prevAbsOn     = false;
        private bool   _prevTcOn      = false;
        private bool   _prevPitOn     = false;
        private bool   _prevOilOn     = false;
        private int    _pitFlash;

        public CompteurOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var outer = new Border
            {
                Width           = DIAL_SIZE,
                Height          = DIAL_SIZE,
                CornerRadius    = new CornerRadius(DIAL_SIZE / 2),
                Background      = new SolidColorBrush(Color.FromArgb(230, 15, 20, 30)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(45, 55, 70)),
                BorderThickness = new Thickness(3),
                ClipToBounds    = true
            };

            var canvas = new Canvas { Width = DIAL_SIZE, Height = DIAL_SIZE };

            // ── Warning icons row (top strip) ────────────────────────────
            double iconY   = 36;
            double iconR   = 7;
            double iconGap = 26;
            double iconStartX = DIAL_SIZE / 2 - 2 * iconGap;

            _iconLights = MakeIcon(iconStartX + 0 * iconGap, iconY, iconR, Color.FromRgb(60, 80, 100));
            _iconABS    = MakeIcon(iconStartX + 1 * iconGap, iconY, iconR, Color.FromRgb(60, 80, 100));
            _iconTC     = MakeIcon(iconStartX + 2 * iconGap, iconY, iconR, Color.FromRgb(60, 80, 100));
            _iconPit    = MakeIcon(iconStartX + 3 * iconGap, iconY, iconR, Color.FromRgb(60, 80, 100));
            _iconOil    = MakeIcon(iconStartX + 4 * iconGap, iconY, iconR, Color.FromRgb(60, 80, 100));

            AddIconLabel(canvas, iconStartX + 0 * iconGap, iconY + iconR + 3, "HDL");
            AddIconLabel(canvas, iconStartX + 1 * iconGap, iconY + iconR + 3, "ABS");
            AddIconLabel(canvas, iconStartX + 2 * iconGap, iconY + iconR + 3, "TC");
            AddIconLabel(canvas, iconStartX + 3 * iconGap, iconY + iconR + 3, "PIT");
            AddIconLabel(canvas, iconStartX + 4 * iconGap, iconY + iconR + 3, "OIL");

            canvas.Children.Add(_iconLights);
            canvas.Children.Add(_iconABS);
            canvas.Children.Add(_iconTC);
            canvas.Children.Add(_iconPit);
            canvas.Children.Add(_iconOil);

            // ── Speed (large center) ─────────────────────────────────────
            _speedText = new TextBlock
            {
                FontSize            = 72,
                FontWeight          = FontWeights.Bold,
                FontFamily          = new FontFamily("Consolas"),
                Foreground          = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Text                = "0"
            };
            Canvas.SetLeft(_speedText, 0);
            Canvas.SetTop(_speedText, 70);
            _speedText.Width = DIAL_SIZE;
            _speedText.TextAlignment = TextAlignment.Center;
            canvas.Children.Add(_speedText);

            // ── Gear (below speed) ───────────────────────────────────────
            _gearText = new TextBlock
            {
                FontSize            = 22,
                FontWeight          = FontWeights.Bold,
                FontFamily          = new FontFamily("Consolas"),
                Foreground          = new SolidColorBrush(Color.FromRgb(255, 235, 60)),
                Text                = "N",
                Width               = DIAL_SIZE,
                TextAlignment       = TextAlignment.Center,
            };
            Canvas.SetLeft(_gearText, 0);
            Canvas.SetTop(_gearText, 148);
            canvas.Children.Add(_gearText);

            // ── KPH / BOSCH label ────────────────────────────────────────
            _kphLabel = new TextBlock
            {
                Text          = "KPH",
                FontSize      = 9,
                FontWeight    = FontWeights.SemiBold,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = new SolidColorBrush(Color.FromRgb(120, 140, 160)),
                Width         = DIAL_SIZE,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(_kphLabel, 0);
            Canvas.SetTop(_kphLabel, 168);
            canvas.Children.Add(_kphLabel);

            // ── FFB label ────────────────────────────────────────────────
            _ffbLabel = new TextBlock
            {
                Text          = "- FFB +",
                FontSize      = 8,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = new SolidColorBrush(Color.FromRgb(80, 100, 120)),
                Width         = DIAL_SIZE,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(_ffbLabel, 0);
            Canvas.SetTop(_ffbLabel, 179);
            canvas.Children.Add(_ffbLabel);

            // ── ENE label (left of energy bar area) ──────────────────────
            _eneLabel = new TextBlock
            {
                Text       = "ENE",
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 220))
            };
            Canvas.SetLeft(_eneLabel, 20);
            Canvas.SetTop(_eneLabel, 190);
            canvas.Children.Add(_eneLabel);

            // ── Fuel bar (red) ───────────────────────────────────────────
            double barMargin  = 28;
            double barWidth   = DIAL_SIZE - barMargin * 2;
            double fuelBarTop = 202;

            _fuelBarBg = new Border
            {
                Width           = barWidth,
                Height          = 14,
                CornerRadius    = new CornerRadius(3),
                Background      = new SolidColorBrush(Color.FromRgb(40, 10, 10)),
                ClipToBounds    = true
            };
            _fuelBarFill = new Border
            {
                Height          = 14,
                CornerRadius    = new CornerRadius(3),
                Background      = new SolidColorBrush(Color.FromRgb(220, 50, 50)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width           = barWidth
            };
            _fuelBarBg.Child = _fuelBarFill;
            Canvas.SetLeft(_fuelBarBg, barMargin);
            Canvas.SetTop(_fuelBarBg, fuelBarTop);
            canvas.Children.Add(_fuelBarBg);

            _fuelText = new TextBlock
            {
                FontSize   = 8,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.White,
                Width      = barWidth,
                TextAlignment = TextAlignment.Left
            };
            Canvas.SetLeft(_fuelText, barMargin + 3);
            Canvas.SetTop(_fuelText, fuelBarTop + 1);
            canvas.Children.Add(_fuelText);

            _fuelLapsText = new TextBlock
            {
                FontSize   = 8,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Width      = barWidth,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(_fuelLapsText, barMargin);
            Canvas.SetTop(_fuelLapsText, fuelBarTop + 1);
            canvas.Children.Add(_fuelLapsText);

            // ── Energy bar (blue) ────────────────────────────────────────
            double eneBarTop = 220;

            _eneBarBg = new Border
            {
                Width        = barWidth,
                Height       = 14,
                CornerRadius = new CornerRadius(3),
                Background   = new SolidColorBrush(Color.FromRgb(10, 20, 50)),
                ClipToBounds = true
            };
            _eneBarFill = new Border
            {
                Height          = 14,
                CornerRadius    = new CornerRadius(3),
                Background      = new SolidColorBrush(Color.FromRgb(40, 120, 220)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width           = barWidth
            };
            _eneBarBg.Child = _eneBarFill;
            Canvas.SetLeft(_eneBarBg, barMargin);
            Canvas.SetTop(_eneBarBg, eneBarTop);
            canvas.Children.Add(_eneBarBg);

            _eneText = new TextBlock
            {
                FontSize      = 8,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = Brushes.White,
                Width         = barWidth,
                TextAlignment = TextAlignment.Left
            };
            Canvas.SetLeft(_eneText, barMargin + 3);
            Canvas.SetTop(_eneText, eneBarTop + 1);
            canvas.Children.Add(_eneText);

            _eneLapsText = new TextBlock
            {
                FontSize      = 8,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Width         = barWidth,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(_eneLapsText, barMargin);
            Canvas.SetTop(_eneLapsText, eneBarTop + 1);
            canvas.Children.Add(_eneLapsText);

            outer.Child = canvas;
            Content = outer;
        }

        private static Ellipse MakeIcon(double cx, double cy, double r, Color fill)
        {
            var e = new Ellipse
            {
                Width  = r * 2,
                Height = r * 2,
                Fill   = new SolidColorBrush(fill)
            };
            Canvas.SetLeft(e, cx - r);
            Canvas.SetTop(e, cy - r);
            return e;
        }

        private static void AddIconLabel(Canvas canvas, double cx, double y, string text)
        {
            var tb = new TextBlock
            {
                Text       = text,
                FontSize   = 6,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(70, 90, 110)),
                Width      = 28,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, cx - 14);
            Canvas.SetTop(tb, y);
            canvas.Children.Add(tb);
        }

        public override void UpdateData()
        {
            var d = DataService.GetDashboardData();

            // ── Gear ──────────────────────────────────────────────────────
            if (d.Gear != _prevGear)
            {
                _gearText.Text = d.Gear switch { -1 => "R", 0 => "N", _ => d.Gear.ToString() };
                _prevGear = d.Gear;
            }

            // ── Speed ─────────────────────────────────────────────────────
            int speedInt = (int)d.Speed;
            if (speedInt != _prevSpeedInt)
            {
                _speedText.Text = speedInt.ToString();
                _prevSpeedInt = speedInt;
            }

            // ── Fuel bar ──────────────────────────────────────────────────
            double fuelPct = d.FuelCapacity > 0 ? Math.Clamp(d.Fuel / d.FuelCapacity, 0, 1) : 0;
            if (Math.Abs(fuelPct - _prevFuelPct) > 0.002)
            {
                double barW = _fuelBarBg.Width > 0 ? _fuelBarBg.Width * fuelPct : 0;
                _fuelBarFill.Width = Math.Max(0, barW);
                _prevFuelPct = fuelPct;

                Color fuelCol = fuelPct > 0.3 ? Color.FromRgb(220, 50, 50) :
                                fuelPct > 0.1 ? Color.FromRgb(220, 140, 0) :
                                Color.FromRgb(255, 30, 30);
                _fuelBarFill.Background = BrushCache.Get(fuelCol);
            }

            string fuelTxt = $"{d.Fuel:F1}L";
            if (fuelTxt != _prevFuelText) { _fuelText.Text = fuelTxt; _prevFuelText = fuelTxt; }

            string fuelLaps = d.FuelPerLap > 0 ? $"({d.Fuel / d.FuelPerLap:F1} laps)" : "";
            if (fuelLaps != _prevFuelLaps) { _fuelLapsText.Text = fuelLaps; _prevFuelLaps = fuelLaps; }

            // ── Energy bar ────────────────────────────────────────────────
            double enePct = Math.Clamp(d.Energy / 100.0, 0, 1);
            if (Math.Abs(enePct - _prevEnePct) > 0.002)
            {
                double barW = _eneBarBg.Width > 0 ? _eneBarBg.Width * enePct : 0;
                _eneBarFill.Width = Math.Max(0, barW);
                _prevEnePct = enePct;

                Color eneCol = enePct > 0.3 ? Color.FromRgb(40, 120, 220) :
                               enePct > 0.1 ? Color.FromRgb(220, 140, 0) :
                               Color.FromRgb(220, 50, 50);
                _eneBarFill.Background = BrushCache.Get(eneCol);
            }

            string eneTxt = d.Energy > 0 ? $"{d.Energy:F1}%" : "N/A";
            if (eneTxt != _prevEneText) { _eneText.Text = eneTxt; _prevEneText = eneTxt; }

            string eneLaps = d.EnergyPerLap > 0 ? $"({d.Energy / d.EnergyPerLap:F1} laps)" : "";
            if (eneLaps != _prevEneLaps) { _eneLapsText.Text = eneLaps; _prevEneLaps = eneLaps; }

            // ── Warning icons ─────────────────────────────────────────────
            bool absOn = d.ABS > 0;
            if (absOn != _prevAbsOn)
            {
                _iconABS.Fill = BrushCache.Get(absOn ? Color.FromRgb(255, 200, 0) : Color.FromRgb(60, 80, 100));
                _prevAbsOn = absOn;
            }

            bool tcOn = d.Stability > 0;
            if (tcOn != _prevTcOn)
            {
                _iconTC.Fill = BrushCache.Get(tcOn ? Color.FromRgb(255, 200, 0) : Color.FromRgb(60, 80, 100));
                _prevTcOn = tcOn;
            }

            bool oilOn = d.OilTemp > 140;
            if (oilOn != _prevOilOn)
            {
                _iconOil.Fill = BrushCache.Get(oilOn ? Color.FromRgb(255, 80, 0) : Color.FromRgb(60, 80, 100));
                _prevOilOn = oilOn;
            }

            // Pit limiter — flash green
            bool pitOn = d.PitLimiter;
            if (pitOn != _prevPitOn || pitOn)
            {
                if (pitOn)
                {
                    _pitFlash++;
                    bool on = (_pitFlash / 4) % 2 == 0;
                    _iconPit.Fill = BrushCache.Get(on ? Color.FromRgb(0, 220, 100) : Color.FromRgb(0, 60, 30));
                }
                else
                {
                    _pitFlash = 0;
                    _iconPit.Fill = BrushCache.Get(Color.FromRgb(60, 80, 100));
                }
                _prevPitOn = pitOn;
            }
        }
    }
}
