using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
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
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // Main status
            _statusText = new TextBlock
            {
                FontSize = 20, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(_statusText);

            // Detail line
            _detailText = new TextBlock
            {
                FontSize = 10, FontFamily = OverlayHelper.FontConsolas,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            sp.Children.Add(_detailText);

            // Distance info
            _distanceText = new TextBlock
            {
                FontSize = 9, FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextSecondary),
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

            var tm = ThemeManager.Current;
            if (isClear)
            {
                // SAFE — green
                _mainBorder.Background = BrushCache.Get(Color.FromArgb(220, 20, (byte)(tm.StateGood.G / 2), 30));
                _statusText.Text = "✓ PISTE LIBRE";
                _statusText.Foreground = BrushCache.Get(tm.StateGood);
                _detailText.Text = "Vous pouvez rejoindre";
                _detailText.Foreground = BrushCache.Get(Color.FromArgb(200, (byte)(tm.StateGood.R / 2), tm.StateGood.G, (byte)(tm.StateGood.B / 2 + 60)));
                _distanceText.Text = "";
                _flashCounter = 0;
            }
            else if (nearDist > 80)
            {
                // CAUTION — orange, car approaching but still some distance
                _mainBorder.Background = BrushCache.Get(Color.FromArgb(220, (byte)(tm.StateWarn.R / 2), 70, 10));
                _statusText.Text = "⚠ ATTENTION";
                _statusText.Foreground = BrushCache.Get(tm.StateWarn);
                _detailText.Text = "Voiture en approche";
                _detailText.Foreground = BrushCache.Get(Color.FromArgb(200, tm.StateWarn.R, (byte)(tm.StateWarn.G * 3 / 4), 100));
                _distanceText.Text = $"{nearDist:F0}m — {nearSpeed:F0} km/h";
                _flashCounter = 0;
            }
            else
            {
                // DANGER — red flashing, car very close behind
                _flashCounter++;
                bool on = (_flashCounter / 3) % 2 == 0;
                _mainBorder.Background = BrushCache.Get(on
                    ? Color.FromArgb(230, (byte)(tm.StateDanger.R / 2), 20, 20)
                    : Color.FromArgb(200, (byte)(tm.StateDanger.R / 3), 10, 10));
                _statusText.Text = "✕ ATTENDRE";
                _statusText.Foreground = BrushCache.Get(on ? tm.StateDanger : Color.FromRgb((byte)(tm.StateDanger.R * 3 / 4), 50, 50));
                _detailText.Text = "Ne pas rejoindre !";
                _detailText.Foreground = BrushCache.Get(Color.FromArgb(200, tm.StateDanger.R, 180, 180));
                _distanceText.Text = $"{nearDist:F0}m — {nearSpeed:F0} km/h";
            }
        }
    }
}
