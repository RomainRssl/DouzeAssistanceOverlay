using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_aheadName, 1);
            aheadRow.Children.Add(_aheadName);

            _aheadClass = new TextBlock
            {
                FontSize = 8, Margin = new Thickness(4, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 106, 115)),
                VerticalAlignment = VerticalAlignment.Center
            };

            _aheadGap = new TextBlock
            {
                Text = "--.---", FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0)),
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
                Background = new SolidColorBrush(Color.FromArgb(30, 88, 166, 255)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2)
            };
            var playerGrid = new Grid();
            playerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            playerGrid.ColumnDefinitions.Add(new ColumnDefinition());

            _playerPos = new TextBlock
            {
                Text = "P-", FontSize = 16, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(_playerPos, 0);
            playerGrid.Children.Add(_playerPos);

            _playerName = new TextBlock
            {
                Text = "---", FontSize = 13, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
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
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_behindName, 1);
            behindRow.Children.Add(_behindName);

            _behindClass = new TextBlock
            {
                FontSize = 8, Margin = new Thickness(4, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 106, 115)),
                VerticalAlignment = VerticalAlignment.Center
            };

            _behindGap = new TextBlock
            {
                Text = "--.---", FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0)),
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
            if (ahead != null)
            {
                _aheadName.Text = $"P{ahead.Position}  {OverlayHelper.FormatName(ahead.DriverName)}";
                _aheadGap.Text = $"-{gapAhead:F3}";
                _aheadGap.Foreground = new SolidColorBrush(
                    gapAhead < 1.0 ? Color.FromRgb(255, 59, 48) :
                    gapAhead < 3.0 ? Color.FromRgb(255, 204, 0) :
                    Color.FromRgb(76, 217, 100));
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
                _behindGap.Foreground = new SolidColorBrush(
                    gapBehind < 1.0 ? Color.FromRgb(255, 59, 48) :
                    gapBehind < 3.0 ? Color.FromRgb(255, 204, 0) :
                    Color.FromRgb(76, 217, 100));
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
