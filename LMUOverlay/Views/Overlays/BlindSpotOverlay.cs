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
        private readonly Ellipse _leftDot, _rightDot;

        private const double LED_W = 8;
        private const double LED_H = 50;

        public BlindSpotOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            // Transparent container — just two LEDs spaced apart
            var grid = new Grid
            {
                Width = 300,
                Height = LED_H + 10,
                Background = Brushes.Transparent
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // LEFT LED
            _leftLed = MakeLed(out _leftDot);
            Grid.SetColumn(_leftLed, 0);
            grid.Children.Add(_leftLed);

            // RIGHT LED
            _rightLed = MakeLed(out _rightDot);
            Grid.SetColumn(_rightLed, 2);
            grid.Children.Add(_rightLed);

            Content = grid;
        }

        private static Border MakeLed(out Ellipse dot)
        {
            var border = new Border
            {
                Width = LED_W, Height = LED_H,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 165, 0)),
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            dot = new Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            stack.Children.Add(dot);

            // Second dot
            var dot2 = new Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            stack.Children.Add(dot2);

            // Third dot
            var dot3 = new Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            stack.Children.Add(dot3);

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
