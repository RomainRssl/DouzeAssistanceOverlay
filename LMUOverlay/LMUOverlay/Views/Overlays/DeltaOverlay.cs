using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class DeltaOverlay : BaseOverlayWindow
    {
        private readonly TextBlock _deltaText, _bestLapText, _lastLapText, _currentLapText, _predictedText;
        private readonly TextBlock _bestLapLabel;
        private readonly Border _deltaBar;

        public DeltaOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {


            var border = OverlayHelper.MakeBorder();
            var sp = new StackPanel();
            sp.Children.Add(OverlayHelper.MakeTitle("DELTA"));

            // Big delta number
            _deltaText = new TextBlock
            {
                Text = "+0.000", FontSize = 28, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            sp.Children.Add(_deltaText);

            // Delta bar (visual indicator)
            var barBg = new Border
            {
                Height = 6, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Margin = new Thickness(2, 0, 2, 4)
            };
            _deltaBar = new Border
            {
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 0
            };
            barBg.Child = _deltaBar;
            sp.Children.Add(barBg);

            // Info grid
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());

            var c1 = MakeInfoPair("MEILLEUR", out _bestLapText, out _bestLapLabel);
            var c2 = MakeInfoPair("DERNIER", out _lastLapText);
            Grid.SetColumn(c1, 0); Grid.SetColumn(c2, 1);
            g.Children.Add(c1); g.Children.Add(c2);
            sp.Children.Add(g);

            var g2 = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            g2.ColumnDefinitions.Add(new ColumnDefinition());
            g2.ColumnDefinitions.Add(new ColumnDefinition());
            var c3 = MakeInfoPair("EN COURS", out _currentLapText);
            var c4 = MakeInfoPair("PRÉDIT", out _predictedText);
            Grid.SetColumn(c3, 0); Grid.SetColumn(c4, 1);
            g2.Children.Add(c3); g2.Children.Add(c4);
            sp.Children.Add(g2);

            border.Child = sp;
            Content = border;
        }

        private static StackPanel MakeInfoPair(string label, out TextBlock val, out TextBlock lbl)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            lbl = new TextBlock { Text = label, FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(100, 106, 115)), HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(lbl);
            val = new TextBlock { FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)), HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(val);
            return sp;
        }

        private static StackPanel MakeInfoPair(string label, out TextBlock val)
        {
            return MakeInfoPair(label, out val, out _);
        }

        public override void UpdateData()
        {
            var d = DataService.GetDeltaData();

            string sign = d.CurrentDelta >= 0 ? "+" : "";
            _deltaText.Text = $"{sign}{d.CurrentDelta:F3}";

            Color col = d.CurrentDelta < -0.3 ? Color.FromRgb(76, 217, 100) :
                        d.CurrentDelta < 0.3 ? Color.FromRgb(230, 237, 243) :
                        Color.FromRgb(255, 59, 48);
            _deltaText.Foreground = BrushCache.Get(col);

            // Delta bar
            double barW = Math.Min(Math.Abs(d.CurrentDelta) * 30, 100);
            _deltaBar.Width = Math.Max(0, barW);
            _deltaBar.Background = BrushCache.Get(col);
            _deltaBar.HorizontalAlignment = d.CurrentDelta < 0
                ? HorizontalAlignment.Left : HorizontalAlignment.Right;

            // Show class best lap time if it is faster than the player's personal best
            double classBest = DataService.GetClassSessionBestLapTime();
            bool usingClassBest = classBest > 0 && (d.BestLapTime <= 0 || classBest < d.BestLapTime);
            _bestLapLabel.Text = usingClassBest ? "MEILLEUR CLS" : "MEILLEUR";
            _bestLapText.Text  = FormatTime(usingClassBest ? classBest : d.BestLapTime);

            _lastLapText.Text = FormatTime(d.LastLapTime);
            _currentLapText.Text = FormatTime(d.CurrentLapTime);
            _predictedText.Text = FormatTime(d.PredictedLapTime);
        }

        private static string FormatTime(double t)
        {
            if (t <= 0) return "--:--.---";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
