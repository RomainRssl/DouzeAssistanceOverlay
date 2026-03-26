using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Blind spot indicator: two LED bars (left/right) that glow orange
    /// when a car is alongside in the blind spot zone.
    /// Intensity varies with proximity.
    /// </summary>
    public class BlindSpotOverlay : BaseOverlayWindow
    {
        private readonly Border _leftLed, _rightLed;
        private readonly Grid _container;

        public BlindSpotOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            double w = GetLedW(s);
            double h = GetLedH(s);

            _container = new Grid
            {
                Width = 300,
                Height = h + 10,
                Background = Brushes.Transparent
            };
            _container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _container.ColumnDefinitions.Add(new ColumnDefinition());
            _container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _leftLed = MakeLed(w, h);
            Grid.SetColumn(_leftLed, 0);
            _container.Children.Add(_leftLed);

            _rightLed = MakeLed(w, h);
            Grid.SetColumn(_rightLed, 2);
            _container.Children.Add(_rightLed);

            Content = _container;
        }

        private static double GetLedW(OverlaySettings s) =>
            s.CustomOptions.TryGetValue("LedWidth", out var v) ? Convert.ToDouble(v) : 8;

        private static double GetLedH(OverlaySettings s) =>
            s.CustomOptions.TryGetValue("LedHeight", out var v) ? Convert.ToDouble(v) : 50;

        public void UpdateLedSize(double w, double h)
        {
            _container.Height = h + 10;
            foreach (var led in new[] { _leftLed, _rightLed })
            {
                led.Width = w;
                led.Height = h;
                led.CornerRadius = new CornerRadius(Math.Max(2, w / 2));
                if (led.Child is StackPanel sp)
                {
                    double dotSize = Math.Max(3, w - 2);
                    foreach (var child in sp.Children)
                        if (child is Ellipse e) { e.Width = dotSize; e.Height = dotSize; }
                }
            }
        }

        private static Border MakeLed(double ledW, double ledH)
        {
            var border = new Border
            {
                Width = ledW, Height = ledH,
                CornerRadius = new CornerRadius(Math.Max(2, ledW / 2)),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 165, 0)),
                VerticalAlignment = VerticalAlignment.Center
            };

            double dotSize = Math.Max(3, ledW - 2);
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            for (int i = 0; i < 3; i++)
            {
                stack.Children.Add(new Ellipse
                {
                    Width = dotSize, Height = dotSize,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }

            border.Child = stack;
            return border;
        }

        public override void UpdateData()
        {
            var (left, right) = DataService.GetBlindSpots();

            UpdateLed(_leftLed, left);
            UpdateLed(_rightLed, right);
        }

        private static void UpdateLed(Border led, double intensity)
        {
            if (intensity <= 0)
            {
                // Off — very subtle
                led.Background = new SolidColorBrush(Color.FromArgb(15, 255, 165, 0));
                if (led.Child is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                        if (child is Ellipse e)
                            e.Fill = new SolidColorBrush(Color.FromArgb(20, 255, 165, 0));
                }
            }
            else
            {
                // On — intensity controls opacity and color
                byte alpha = (byte)(80 + intensity * 175); // 80-255
                byte dotAlpha = (byte)(100 + intensity * 155);

                // At high intensity: shift to red
                Color ledColor, dotColor;
                if (intensity > 0.7)
                {
                    ledColor = Color.FromArgb(alpha, 255, 80, 0);
                    dotColor = Color.FromArgb(dotAlpha, 255, 100, 0);
                }
                else
                {
                    ledColor = Color.FromArgb(alpha, 255, 165, 0);
                    dotColor = Color.FromArgb(dotAlpha, 255, 190, 0);
                }

                led.Background = new SolidColorBrush(ledColor);
                if (led.Child is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                        if (child is Ellipse e)
                            e.Fill = new SolidColorBrush(dotColor);
                }
            }
        }
    }
}
