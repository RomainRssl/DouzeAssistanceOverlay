using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
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
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.ClassLmp2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            main.Children.Add(_positionText);

            // Car Ahead
            var aheadPanel = CreateGapRow(
                ThemeManager.Current.StateGood, "▲",
                out _aheadName, out _aheadGap, out _aheadDelta);
            main.Children.Add(aheadPanel);

            // Separator
            main.Children.Add(new Border
            {
                Height = 1,
                Background = BrushCache.Get(Color.FromArgb(40, ThemeManager.Current.TextMuted.R, ThemeManager.Current.TextMuted.G, ThemeManager.Current.TextMuted.B)),
                Margin = new Thickness(8, 4, 8, 4)
            });

            // Car Behind
            var behindPanel = CreateGapRow(
                ThemeManager.Current.StateDanger, "▼",
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
                FontSize = 11, FontFamily = OverlayHelper.FontSegoeUISB,
                Foreground = BrushCache.Get(ThemeManager.Current.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameText, 1);
            g.Children.Add(nameText);

            gapText = new TextBlock
            {
                FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(gapText, 2);
            g.Children.Add(gapText);

            deltaText = new TextBlock
            {
                FontSize = 10, FontFamily = OverlayHelper.FontConsolas,
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
                    ? BrushCache.Get(ThemeManager.Current.StateDanger)
                    : BrushCache.Get(ThemeManager.Current.StateGood);

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
                    ? BrushCache.Get(ThemeManager.Current.StateGood)
                    : BrushCache.Get(ThemeManager.Current.StateDanger);

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
