using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// "Retour en piste" overlay — appears only when the player is slow or stopped.
    /// Shows whether it's safe to rejoin (no car within 150m behind).
    /// </summary>
    public class RejoinOverlay : BaseOverlayWindow
    {
        private readonly Border _mainBorder;
        private readonly TextBlock _statusText;
        private readonly TextBlock _detailText;
        private readonly TextBlock _distanceText;
        private int _flashCounter;

        private const double SPEED_THRESHOLD = 15; // m/s (~54 km/h)

        public RejoinOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            _mainBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                MinWidth = 180,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            // Title
            sp.Children.Add(new TextBlock
            {
                Text = "RETOUR EN PISTE",
                FontSize = 8, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 120)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // Main status
            _statusText = new TextBlock
            {
                FontSize = 20, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(_statusText);

            // Detail line
            _detailText = new TextBlock
            {
                FontSize = 10, FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            sp.Children.Add(_detailText);

            // Distance info
            _distanceText = new TextBlock
            {
                FontSize = 9, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 140)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            sp.Children.Add(_distanceText);

            _mainBorder.Child = sp;
            Content = _mainBorder;
        }

        public override void UpdateData()
        {
            double playerSpeed = DataService.GetPlayerSpeed();

            // Only show when player is slow/stopped
            if (playerSpeed > SPEED_THRESHOLD)
            {
                _mainBorder.Visibility = Visibility.Collapsed;
                _flashCounter = 0;
                return;
            }

            _mainBorder.Visibility = Visibility.Visible;

            var (isClear, nearDist, nearSpeed) = DataService.GetTrackClearBehind(150);

            if (isClear)
            {
                // SAFE — green
                _mainBorder.Background = new SolidColorBrush(Color.FromArgb(220, 20, 80, 30));
                _statusText.Text = "✓ PISTE LIBRE";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 255, 100));
                _detailText.Text = "Vous pouvez rejoindre";
                _detailText.Foreground = new SolidColorBrush(Color.FromRgb(150, 220, 160));
                _distanceText.Text = "";
                _flashCounter = 0;
            }
            else if (nearDist > 80)
            {
                // CAUTION — orange, car approaching but still some distance
                _mainBorder.Background = new SolidColorBrush(Color.FromArgb(220, 120, 70, 10));
                _statusText.Text = "⚠ ATTENTION";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 50));
                _detailText.Text = "Voiture en approche";
                _detailText.Foreground = new SolidColorBrush(Color.FromRgb(220, 180, 100));
                _distanceText.Text = $"{nearDist:F0}m — {nearSpeed:F0} km/h";
                _flashCounter = 0;
            }
            else
            {
                // DANGER — red flashing, car very close behind
                _flashCounter++;
                bool on = (_flashCounter / 3) % 2 == 0;
                _mainBorder.Background = new SolidColorBrush(on
                    ? Color.FromArgb(230, 160, 20, 20)
                    : Color.FromArgb(200, 100, 10, 10));
                _statusText.Text = "✕ ATTENDRE";
                _statusText.Foreground = new SolidColorBrush(on
                    ? Color.FromRgb(255, 80, 80)
                    : Color.FromRgb(200, 50, 50));
                _detailText.Text = "Ne pas rejoindre !";
                _detailText.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 180));
                _distanceText.Text = $"{nearDist:F0}m — {nearSpeed:F0} km/h";
            }
        }
    }
}
