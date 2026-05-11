using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class TireInfoOverlay : BaseOverlayWindow
    {
        // Per-tire UI elements
        private readonly Border[] _bandL = new Border[4], _bandM = new Border[4], _bandR = new Border[4];
        private readonly TextBlock[] _tempAvg = new TextBlock[4];
        private readonly TextBlock[] _wearText = new TextBlock[4];
        private readonly TextBlock[] _pressText = new TextBlock[4];
        private readonly TextBlock[] _brakeText = new TextBlock[4];
        private readonly TextBlock _compoundText;

        public TireInfoOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {


            var border = OverlayHelper.MakeBorder();
            var main = new StackPanel();

            // Title + compound
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition());
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.Children.Add(OverlayHelper.MakeTitle("PNEUS"));
            _compoundText = new TextBlock
            {
                FontSize = 11, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.StateBestLap),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_compoundText, 1);
            titleRow.Children.Add(_compoundText);
            main.Children.Add(titleRow);

            // Car layout: 2x2 grid with center spacer
            var carGrid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            carGrid.ColumnDefinitions.Add(new ColumnDefinition());   // left tire
            carGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // center
            carGrid.ColumnDefinitions.Add(new ColumnDefinition());   // right tire
            carGrid.RowDefinitions.Add(new RowDefinition());         // front
            carGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) }); // spacer
            carGrid.RowDefinitions.Add(new RowDefinition());         // rear

            // Create 4 tire panels
            var fl = MakeTirePanel(0); Grid.SetRow(fl, 0); Grid.SetColumn(fl, 0); carGrid.Children.Add(fl);
            var fr = MakeTirePanel(1); Grid.SetRow(fr, 0); Grid.SetColumn(fr, 2); carGrid.Children.Add(fr);
            var rl = MakeTirePanel(2); Grid.SetRow(rl, 2); Grid.SetColumn(rl, 0); carGrid.Children.Add(rl);
            var rr = MakeTirePanel(3); Grid.SetRow(rr, 2); Grid.SetColumn(rr, 2); carGrid.Children.Add(rr);

            // Car center line
            var carLine = new Border
            {
                Width = 6, CornerRadius = new CornerRadius(3),
                Background = BrushCache.Get(Color.FromArgb(50, ThemeManager.Current.ClassLmp2.R, ThemeManager.Current.ClassLmp2.G, ThemeManager.Current.ClassLmp2.B)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 3, 0, 3)
            };
            Grid.SetRow(carLine, 0); Grid.SetRowSpan(carLine, 3); Grid.SetColumn(carLine, 1);
            carGrid.Children.Add(carLine);

            main.Children.Add(carGrid);
            border.Child = main;
            Content = border;
        }

        private Border MakeTirePanel(int idx)
        {
            string[] labels = { "AV-G", "AV-D", "AR-G", "AR-D" };

            var panel = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Padding = new Thickness(3),
                Margin = new Thickness(2)
            };

            var sp = new StackPanel();

            // Label
            sp.Children.Add(new TextBlock
            {
                Text = labels[idx], FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = BrushCache.Get(ThemeManager.Current.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // === TIRE RECTANGLE with 3 colored bands ===
            var tireBorder = new Border
            {
                Width = 90, Height = 50,
                CornerRadius = new CornerRadius(5),
                BorderBrush = BrushCache.Get(ThemeManager.Current.Border),
                BorderThickness = new Thickness(2),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var tireContent = new Grid();

            // 3 color bands
            var bandsGrid = new Grid();
            bandsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            bandsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            bandsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            _bandL[idx] = new Border { Background = BrushCache.Get(ThemeManager.Current.StateGood) };
            _bandM[idx] = new Border { Background = BrushCache.Get(ThemeManager.Current.StateGood) };
            _bandR[idx] = new Border { Background = BrushCache.Get(ThemeManager.Current.StateGood) };

            Grid.SetColumn(_bandL[idx], 0);
            Grid.SetColumn(_bandM[idx], 1);
            Grid.SetColumn(_bandR[idx], 2);
            bandsGrid.Children.Add(_bandL[idx]);
            bandsGrid.Children.Add(_bandM[idx]);
            bandsGrid.Children.Add(_bandR[idx]);

            tireContent.Children.Add(bandsGrid);

            // Temperature text centered on top of bands
            _tempAvg[idx] = new TextBlock
            {
                Text = "00°", FontSize = 18, FontWeight = FontWeights.ExtraBold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            tireContent.Children.Add(_tempAvg[idx]);

            tireBorder.Child = tireContent;
            sp.Children.Add(tireBorder);

            // === Info row below tire: Wear | Pressure | Brake ===
            var infoRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            infoRow.ColumnDefinitions.Add(new ColumnDefinition());
            infoRow.ColumnDefinitions.Add(new ColumnDefinition());
            infoRow.ColumnDefinitions.Add(new ColumnDefinition());

            _wearText[idx] = new TextBlock
            {
                FontSize = 10, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_wearText[idx], 0);
            infoRow.Children.Add(_wearText[idx]);

            _pressText[idx] = new TextBlock
            {
                FontSize = 9, FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_pressText[idx], 1);
            infoRow.Children.Add(_pressText[idx]);

            _brakeText[idx] = new TextBlock
            {
                FontSize = 9, FontFamily = OverlayHelper.FontConsolas,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_brakeText[idx], 2);
            infoRow.Children.Add(_brakeText[idx]);

            sp.Children.Add(infoRow);

            panel.Child = sp;
            return panel;
        }

        public override void UpdateData()
        {
            var tires = DataService.GetTireData();
            if (tires == null || tires.Length < 4) return;

            _compoundText.Text = tires[0].Compound;

            for (int i = 0; i < 4; i++)
            {
                var t = tires[i];

                // Temperature bands
                double tL = Norm(t.Temperature != null && t.Temperature.Length > 0 ? t.Temperature[0] : 0);
                double tM = Norm(t.Temperature != null && t.Temperature.Length > 1 ? t.Temperature[1] : 0);
                double tR = Norm(t.Temperature != null && t.Temperature.Length > 2 ? t.Temperature[2] : 0);

                _bandL[i].Background = new SolidColorBrush(TC(tL));
                _bandM[i].Background = new SolidColorBrush(TC(tM));
                _bandR[i].Background = new SolidColorBrush(TC(tR));

                double avg = (tL + tM + tR) / 3.0;
                _tempAvg[i].Text = $"{avg:F0}°";

                // Wear
                double w = Math.Clamp(Math.Abs(t.Wear), 0, 1) * 100;
                _wearText[i].Text = $"{w:F0}%";
                var tm = ThemeManager.Current;
                _wearText[i].Foreground = BrushCache.Get(
                    w > 70 ? tm.StateGood :
                    w > 40 ? tm.StateWarn :
                    tm.StateDanger);

                // Pressure
                _pressText[i].Text = t.Pressure > 0 && t.Pressure < 500
                    ? $"{t.Pressure:F0} kPa" : "-- kPa";

                // Brake
                double bt = Norm(t.BrakeTemp);
                _brakeText[i].Text = bt > 0 ? $"{bt:F0}°" : "--°";
                _brakeText[i].Foreground = BrushCache.Get(
                    bt > 700 ? tm.StateDanger :
                    bt > 500 ? tm.StateWarn :
                    tm.TextMuted);
            }
        }

        private static double Norm(double t)
        {
            if (double.IsNaN(t) || double.IsInfinity(t)) return 0;
            if (t < -50 || t > 500) return 0;
            return t;  // DataService already converts Kelvin to Celsius
        }

        private static Color TC(double t)
        {
            var tm = ThemeManager.Current;
            if (t < 50)  return Color.FromRgb(33, 82, 209);
            if (t < 70)  return tm.ClassLmp2;
            if (t < 85)  return Color.FromRgb((byte)(tm.StateGood.R * 3 / 4), (byte)(tm.StateGood.G * 9 / 10), (byte)(tm.StateGood.B * 3 / 4));
            if (t < 100) return tm.StateGood;
            if (t < 110) return tm.StateWarn;
            if (t < 125) return Color.FromRgb((byte)(tm.StateWarn.R), (byte)(tm.StateWarn.G * 3 / 5), 0);
            return tm.StateDanger;
        }
    }
}
