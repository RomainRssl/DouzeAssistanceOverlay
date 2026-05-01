using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Gap timer overlay showing real-time gap to the car ahead and behind.
    /// </summary>
    public class GapOverlay : BaseOverlayWindow
    {
        private readonly TextBlock _aheadName, _aheadGap, _aheadDelta;
        private readonly TextBlock _behindName, _behindGap, _behindDelta;
        private readonly TextBlock _positionText;
        private double _prevGapAhead, _prevGapBehind;

        public GapOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {


            var border = OverlayHelper.MakeBorder();
            var main = new StackPanel();

            var title = OverlayHelper.MakeTitle("ÉCARTS");
            main.Children.Add(title);

            _positionText = new TextBlock
            {
                FontSize = 12, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            main.Children.Add(_positionText);

            // Car Ahead
            var aheadPanel = CreateGapRow(
                Color.FromRgb(76, 217, 100), "▲",
                out _aheadName, out _aheadGap, out _aheadDelta);
            main.Children.Add(aheadPanel);

            // Separator
            main.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(40, 139, 148, 158)),
                Margin = new Thickness(8, 4, 8, 4)
            });

            // Car Behind
            var behindPanel = CreateGapRow(
                Color.FromRgb(255, 59, 48), "▼",
                out _behindName, out _behindGap, out _behindDelta);
            main.Children.Add(behindPanel);

            border.Child = main;
            Content = border;
        }

        private static Grid CreateGapRow(Color accentColor, string arrow,
            out TextBlock nameText, out TextBlock gapText, out TextBlock deltaText)
        {
            var g = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var arrowText = new TextBlock
            {
                Text = arrow, FontSize = 14,
                Foreground = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(arrowText, 0);
            g.Children.Add(arrowText);

            nameText = new TextBlock
            {
                FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameText, 1);
            g.Children.Add(nameText);

            gapText = new TextBlock
            {
                FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(gapText, 2);
            g.Children.Add(gapText);

            deltaText = new TextBlock
            {
                FontSize = 10, FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(deltaText, 3);
            g.Children.Add(deltaText);

            return g;
        }

        public override void UpdateData()
        {
            var (ahead, gapAhead, behind, gapBehind) = DataService.GetGapData();

            // Position
            var player = DataService.GetAllVehicles().FirstOrDefault(v => v.IsPlayer);
            _positionText.Text = player != null ? $"P{player.Position}" : "P-";

            // Ahead
            if (ahead != null)
            {
                _aheadName.Text = OverlayHelper.FormatName(ahead.DriverName);
                _aheadGap.Text = $"-{gapAhead:F3}";

                double delta = gapAhead - _prevGapAhead;
                _aheadDelta.Text = delta >= 0 ? $"↗{Math.Abs(delta):F2}" : $"↘{Math.Abs(delta):F2}";
                _aheadDelta.Foreground = delta >= 0
                    ? new SolidColorBrush(Color.FromRgb(255, 100, 100))
                    : new SolidColorBrush(Color.FromRgb(100, 255, 100));

                _prevGapAhead = gapAhead;
            }
            else
            {
                _aheadName.Text = "---";
                _aheadGap.Text = "--.---";
                _aheadDelta.Text = "";
            }

            // Behind
            if (behind != null)
            {
                _behindName.Text = OverlayHelper.FormatName(behind.DriverName);
                _behindGap.Text = $"+{gapBehind:F3}";

                double delta = gapBehind - _prevGapBehind;
                _behindDelta.Text = delta >= 0 ? $"↗{Math.Abs(delta):F2}" : $"↘{Math.Abs(delta):F2}";
                _behindDelta.Foreground = delta >= 0
                    ? new SolidColorBrush(Color.FromRgb(100, 255, 100))
                    : new SolidColorBrush(Color.FromRgb(255, 100, 100));

                _prevGapBehind = gapBehind;
            }
            else
            {
                _behindName.Text = "---";
                _behindGap.Text = "--.---";
                _behindDelta.Text = "";
            }
        }
    }
}
