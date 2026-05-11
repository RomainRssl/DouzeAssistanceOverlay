using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class RelativeAheadBehindOverlay : BaseOverlayWindow
    {
        private readonly TextBlock _aheadName, _aheadGap, _aheadClass;
        private readonly TextBlock _playerName, _playerPos;
        private readonly TextBlock _behindName, _behindGap, _behindClass;
        private readonly Border _aheadClassDot, _behindClassDot;

        public RelativeAheadBehindOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {


            var border = OverlayHelper.MakeBorder();
            var sp = new StackPanel();

            // --- Car ahead ---
            var aheadRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            aheadRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });   // class dot
            aheadRow.ColumnDefinitions.Add(new ColumnDefinition());                                // name
            aheadRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // gap

            _aheadClassDot = new Border
            {
                Width = 4, CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 2, 6, 2)
            };
            Grid.SetColumn(_aheadClassDot, 0);
            aheadRow.Children.Add(_aheadClassDot);

            _aheadName = new TextBlock
            {
                Text = "---", FontSize = 12,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_aheadName, 1);
            aheadRow.Children.Add(_aheadName);

            _aheadClass = new TextBlock
            {
                FontSize = 8, Margin = new Thickness(4, 0, 0, 0),
                Foreground = BrushCache.Get(ThemeManager.Current.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };

            _aheadGap = new TextBlock
            {
                Text = "--.---", FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.StateWarn),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_aheadGap, 2);
            aheadRow.Children.Add(_aheadGap);

            sp.Children.Add(aheadRow);

            // --- Player (center) ---
            var playerRow = new Grid
            {
                Margin = new Thickness(0, 1, 0, 1)
            };
            var playerBg = new Border
            {
                Background = BrushCache.Get(Color.FromArgb(30, ThemeManager.Current.ClassLmp2.R, ThemeManager.Current.ClassLmp2.G, ThemeManager.Current.ClassLmp2.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2)
            };
            var playerGrid = new Grid();
            playerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            playerGrid.ColumnDefinitions.Add(new ColumnDefinition());

            _playerPos = new TextBlock
            {
                Text = "P-", FontSize = 16, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.ClassLmp2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(_playerPos, 0);
            playerGrid.Children.Add(_playerPos);

            _playerName = new TextBlock
            {
                Text = "---", FontSize = 13, FontWeight = FontWeights.SemiBold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_playerName, 1);
            playerGrid.Children.Add(_playerName);

            playerBg.Child = playerGrid;
            playerRow.Children.Add(playerBg);
            sp.Children.Add(playerRow);

            // --- Car behind ---
            var behindRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            behindRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            behindRow.ColumnDefinitions.Add(new ColumnDefinition());
            behindRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _behindClassDot = new Border
            {
                Width = 4, CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 2, 6, 2)
            };
            Grid.SetColumn(_behindClassDot, 0);
            behindRow.Children.Add(_behindClassDot);

            _behindName = new TextBlock
            {
                Text = "---", FontSize = 12,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_behindName, 1);
            behindRow.Children.Add(_behindName);

            _behindClass = new TextBlock
            {
                FontSize = 8, Margin = new Thickness(4, 0, 0, 0),
                Foreground = BrushCache.Get(ThemeManager.Current.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };

            _behindGap = new TextBlock
            {
                Text = "--.---", FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.StateWarn),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_behindGap, 2);
            behindRow.Children.Add(_behindGap);

            sp.Children.Add(behindRow);

            border.Child = sp;
            Content = border;
        }

        public override void UpdateData()
        {
            var (ahead, gapAhead, behind, gapBehind) = DataService.GetGapData();
            var all = DataService.GetAllVehicles();
            var player = all.FirstOrDefault(v => v.IsPlayer);

            // Player
            if (player != null)
            {
                _playerPos.Text = $"P{player.Position}";
                _playerName.Text = OverlayHelper.FormatName(player.DriverName);
            }

            // Ahead
            var tm = ThemeManager.Current;
            if (ahead != null)
            {
                _aheadName.Text = $"P{ahead.Position}  {OverlayHelper.FormatName(ahead.DriverName)}";
                _aheadGap.Text = $"-{gapAhead:F3}";
                _aheadGap.Foreground = BrushCache.Get(
                    gapAhead < 1.0 ? tm.StateDanger :
                    gapAhead < 3.0 ? tm.StateWarn :
                    tm.StateGood);
                var cls = OverlayHelper.GetClassColor(ahead.VehicleClass);
                _aheadClassDot.Background = new SolidColorBrush(cls);
            }
            else
            {
                _aheadName.Text = "— Leader —";
                _aheadGap.Text = "";
                _aheadClassDot.Background = Brushes.Transparent;
            }

            // Behind
            if (behind != null)
            {
                _behindName.Text = $"P{behind.Position}  {OverlayHelper.FormatName(behind.DriverName)}";
                _behindGap.Text = $"+{gapBehind:F3}";
                _behindGap.Foreground = BrushCache.Get(
                    gapBehind < 1.0 ? tm.StateDanger :
                    gapBehind < 3.0 ? tm.StateWarn :
                    tm.StateGood);
                var cls = OverlayHelper.GetClassColor(behind.VehicleClass);
                _behindClassDot.Background = new SolidColorBrush(cls);
            }
            else
            {
                _behindName.Text = "— Dernier —";
                _behindGap.Text = "";
                _behindClassDot.Background = Brushes.Transparent;
            }
        }
    }
}
