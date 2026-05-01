using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Damage overlay - coordonnées calées pixel par pixel sur les assets natifs.
    /// DentSeverity[8]: 0=FL, 1=FR, 2=RL, 3=RR, 4=Rear, 5=Front, 6=Left, 7=Right
    /// </summary>
    public class DamageOverlay : BaseOverlayWindow
    {
        private const double W  = 160;
        private const double BY = 14;   // offset Y du body dans le canvas (espace titre)
        private const double H  = 320;  // BY(14) + aileron_bottom(220+13=233) + textes + marge

        private const string RES = "pack://application:,,,/Resources/Damage/";

        private readonly Rectangle _zoneFL, _zoneFR, _zoneRL, _zoneRR;
        private readonly Rectangle _zoneFront, _zoneRear;
        private readonly Rectangle _zoneLeft, _zoneRight;
        private readonly Rectangle _zoneAileron;
        private readonly Rectangle _zoneMotor;

        private readonly TextBlock _damagePct, _repairTime;
        private int _flashCounter;

        public DamageOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var canvas = new Canvas
            {
                Width  = W,
                Height = H,
                Background = new SolidColorBrush(Color.FromArgb(210, 8, 8, 8))
            };

            // Titre
            var title = new TextBlock
            {
                Text          = "DAMAGE",
                FontSize      = 9,
                FontWeight    = FontWeights.Bold,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = new SolidColorBrush(Color.FromRgb(100, 130, 130)),
                Width         = W,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(title, 0);
            Canvas.SetTop(title,  2);
            canvas.Children.Add(title);

            // Body fond (160x241 natif)
            AddMaskedRect(canvas, "body.png",       0,       BY +   0, 160, 241,
                Color.FromArgb(80, 150, 170, 170), out _);

            // Front bumper [4] (129x41 natif)
            _zoneFront = AddZone(canvas, "front_bump.png",  15, BY +   5, 129,  41);

            // FL triangle [0] (23x42 natif)
            _zoneFL    = AddZone(canvas, "fl.png",           8, BY +  41,  23,  42);

            // FR triangle [1] (160x241 natif, plein cadre)
            _zoneFR    = AddZone(canvas, "fr.png",           0, BY +   0, 160, 241);

            // Left side [6] (12x65 natif)
            _zoneLeft  = AddZone(canvas, "left.png",        14, BY +  77,  12,  65);

            // Right side [7] (12x64 natif)
            _zoneRight = AddZone(canvas, "right.png",      132, BY +  77,  12,  64);

            // RL triangle [2] (23x41 natif)
            _zoneRL    = AddZone(canvas, "rl.png",           8, BY + 137,  23,  41);

            // RR triangle [3] (23x41 natif)
            _zoneRR    = AddZone(canvas, "rr.png",         126, BY + 137,  23,  41);

            // Rear bumper [5] (129x38 natif)
            _zoneRear  = AddZone(canvas, "rear_bump.png",   15, BY + 174, 129,  38);

            // Aileron (145x13 natif) — y=220 dans repère body
            _zoneAileron = AddZone(canvas, "aileron.png",    7, BY + 220, 145,  13);

            // Motor icon (160x241 natif, plein cadre)
            _zoneMotor = AddZone(canvas, "motor.png",        0, BY +   0, 160, 241);

            // Damage %
            _damagePct = new TextBlock
            {
                FontSize      = 15,
                FontWeight    = FontWeights.Bold,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = Brushes.White,
                TextAlignment = TextAlignment.Center,
                Width         = W
            };
            Canvas.SetLeft(_damagePct, 0);
            Canvas.SetTop(_damagePct,  BY + 220 + 13 + 8);
            canvas.Children.Add(_damagePct);

            // Repair time
            _repairTime = new TextBlock
            {
                FontSize      = 11,
                FontWeight    = FontWeights.Bold,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = Brushes.White,
                TextAlignment = TextAlignment.Center,
                Width         = W
            };
            Canvas.SetLeft(_repairTime, 0);
            Canvas.SetTop(_repairTime,  BY + 220 + 13 + 28);
            canvas.Children.Add(_repairTime);

            Content = canvas;
        }

        public override void UpdateData()
        {
            var d    = DataService.GetDamageData();
            double[] sev = d.DentSeverity;

            double maxSev = 0;
            for (int i = 0; i < 8 && i < sev.Length; i++)
                maxSev = Math.Max(maxSev, sev[i]);

            bool hasDamage = maxSev > 0 || d.AnyDetached || d.Overheating;
            double dmgPct  = Math.Clamp(maxSev * 33, 0, 100);

            _damagePct.Text = $"{dmgPct:F0}%";
            _damagePct.Foreground = new SolidColorBrush(
                dmgPct < 10 ? Color.FromRgb(180, 195, 195) :
                dmgPct < 40 ? Color.FromRgb(255, 204,   0) :
                              Color.FromRgb(255,  59,  48));

            if (d.EstimatedRepairTime > 0)
            {
                var ts = TimeSpan.FromSeconds(d.EstimatedRepairTime);
                _repairTime.Text = $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}:{(ts.Milliseconds / 10):D2}";
                _repairTime.Foreground = new SolidColorBrush(
                    d.EstimatedRepairTime > 120 ? Color.FromRgb(255,  59, 48) :
                    d.EstimatedRepairTime >  30 ? Color.FromRgb(255, 204,  0) :
                                                  Color.FromRgb(180, 195, 195));
            }
            else
            {
                _repairTime.Text       = "00:00:00";
                _repairTime.Foreground = new SolidColorBrush(Color.FromRgb(60, 70, 70));
            }

            SetZone(_zoneFL,      sev.Length > 0 ? sev[0] : 0);
            SetZone(_zoneFR,      sev.Length > 1 ? sev[1] : 0);
            SetZone(_zoneRL,      sev.Length > 2 ? sev[2] : 0);
            SetZone(_zoneRR,      sev.Length > 3 ? sev[3] : 0);
            SetZone(_zoneFront,   sev.Length > 5 ? sev[5] : 0);
            SetZone(_zoneRear,    sev.Length > 4 ? sev[4] : 0);
            SetZone(_zoneLeft,    sev.Length > 6 ? sev[6] : 0);
            SetZone(_zoneRight,   sev.Length > 7 ? sev[7] : 0);
            SetZone(_zoneAileron, sev.Length > 4 ? sev[4] : 0);

            if (d.Overheating)
            {
                _flashCounter++;
                bool on = (_flashCounter / 3) % 2 == 0;
                _zoneMotor.Fill = new SolidColorBrush(
                    on ? Color.FromRgb(255, 59, 48) : Color.FromRgb(120, 20, 20));
            }
            else if (hasDamage)
            {
                _flashCounter = 0;
                _zoneMotor.Fill = new SolidColorBrush(Color.FromRgb(255, 204, 0));
            }
            else
            {
                _flashCounter = 0;
                _zoneMotor.Fill = new SolidColorBrush(Color.FromArgb(60, 150, 170, 170));
            }
        }

        private static void SetZone(Rectangle rect, double severity)
        {
            Color c;
            if (severity <= 0)
                c = Color.FromArgb(60, 150, 170, 170);
            else if (severity < 1)
                c = Color.FromRgb(255, 235,  59);
            else if (severity < 2)
                c = Color.FromRgb(255, 149,   0);
            else
                c = Color.FromRgb(255,  59,  48);
            rect.Fill = new SolidColorBrush(c);
        }

        private static Rectangle AddZone(Canvas canvas, string imgName,
            double x, double y, double w, double h)
        {
            AddMaskedRect(canvas, imgName, x, y, w, h,
                Color.FromArgb(60, 150, 170, 170), out var rect);
            return rect;
        }

        private static void AddMaskedRect(Canvas canvas, string imgName,
            double x, double y, double w, double h, Color fill, out Rectangle rect)
        {
            rect = new Rectangle
            {
                Width  = w,
                Height = h,
                Fill   = new SolidColorBrush(fill),
                IsHitTestVisible = false
            };
            try
            {
                var bmp = new BitmapImage(new Uri($"{RES}{imgName}", UriKind.Absolute));
                rect.OpacityMask = new ImageBrush(bmp) { Stretch = Stretch.Fill };
            }
            catch { }
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect,  y);
            canvas.Children.Add(rect);
        }
    }
}
