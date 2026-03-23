using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class LapHistoryOverlay : BaseOverlayWindow
    {
        private readonly StackPanel _tablePanel;
        private readonly Canvas _graphCanvas;
        private readonly Border _tableContainer, _graphContainer;
        private readonly TextBlock _bestLapLabel, _modeLabel;
        private bool _graphMode;
        private int _lastCount;

        private const double GW = 300, GH = 120;
        private const int GRAPH_LAPS = 6;

        public LapHistoryOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var border = OverlayHelper.MakeBorder();
            var sp = new StackPanel();

            // Title row with mode toggle
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition());
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sp.Children.Add(titleRow);

            var title = OverlayHelper.MakeTitle("LAP HISTORY");
            titleRow.Children.Add(title);

            _modeLabel = new TextBlock
            {
                Text = "[TABLE]", FontSize = 9, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            _modeLabel.MouseLeftButtonDown += (s2, e) =>
            {
                _graphMode = !_graphMode;
                _modeLabel.Text = _graphMode ? "[GRAPH]" : "[TABLE]";
                _tableContainer.Visibility = _graphMode ? Visibility.Collapsed : Visibility.Visible;
                _graphContainer.Visibility = _graphMode ? Visibility.Visible : Visibility.Collapsed;
                _lastCount = -1; // force refresh
            };
            Grid.SetColumn(_modeLabel, 1);
            titleRow.Children.Add(_modeLabel);

            _bestLapLabel = new TextBlock
            {
                FontSize = 11, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(76, 217, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            sp.Children.Add(_bestLapLabel);

            // === TABLE MODE ===
            _tableContainer = new Border();
            var tableInner = new StackPanel();

            var hdr = new Grid { Margin = new Thickness(2, 0, 2, 2) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            H(hdr, "LAP", 0); H(hdr, "TIME", 1); H(hdr, "S1", 2); H(hdr, "S2", 3); H(hdr, "S3", 4); H(hdr, "FUEL", 5);
            tableInner.Children.Add(hdr);

            _tablePanel = new StackPanel();
            var scroll = new ScrollViewer { Content = _tablePanel, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, MaxHeight = 200 };
            tableInner.Children.Add(scroll);
            _tableContainer.Child = tableInner;
            sp.Children.Add(_tableContainer);

            // === GRAPH MODE ===
            _graphCanvas = new Canvas
            {
                Width = GW, Height = GH,
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255))
            };
            _graphContainer = new Border
            {
                Child = _graphCanvas,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed
            };
            sp.Children.Add(_graphContainer);

            border.Child = sp;
            Content = border;
        }

        public override void UpdateData()
        {
            var laps = DataService.GetLapHistory();
            if (laps.Count == _lastCount) return;
            _lastCount = laps.Count;

            double bestTime = laps.Where(l => l.LapTime > 0).Select(l => l.LapTime).DefaultIfEmpty(0).Min();
            _bestLapLabel.Text = bestTime > 0 ? $"Best: {Fmt(bestTime)}" : "";

            if (_graphMode)
                DrawGraph(laps, bestTime);
            else
                DrawTable(laps, bestTime);
        }

        // ================================================================
        // TABLE MODE
        // ================================================================

        private void DrawTable(List<LapRecord> laps, double bestTime)
        {
            _tablePanel.Children.Clear();
            foreach (var lap in laps.AsEnumerable().Reverse().Take(20))
            {
                bool isBest = bestTime > 0 && Math.Abs(lap.LapTime - bestTime) < 0.001;
                Color tc = isBest ? Color.FromRgb(76, 217, 100) : Color.FromRgb(200, 210, 210);

                var row = new Grid { Margin = new Thickness(2, 0, 2, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition());

                V(row, $"{lap.LapNumber}", 0, tc, HorizontalAlignment.Left);
                V(row, Fmt(lap.LapTime), 1, tc);
                V(row, FS(lap.Sector1), 2, tc);
                V(row, FS(lap.Sector2), 3, tc);
                V(row, FS(lap.Sector3), 4, tc);
                V(row, $"{lap.FuelRemaining:F1}", 5, Color.FromRgb(100, 120, 120));

                if (isBest)
                {
                    var bg = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 76, 217, 100)), CornerRadius = new CornerRadius(2) };
                    Grid.SetColumnSpan(bg, 6);
                    row.Children.Insert(0, bg);
                }

                _tablePanel.Children.Add(row);
            }
        }

        // ================================================================
        // GRAPH MODE — last 6 laps as line chart
        // ================================================================

        private void DrawGraph(List<LapRecord> laps, double bestTime)
        {
            _graphCanvas.Children.Clear();
            var recent = laps.AsEnumerable().Reverse().Take(GRAPH_LAPS).Reverse().ToList();
            if (recent.Count < 2) return;

            double minTime = recent.Where(l => l.LapTime > 0).Select(l => l.LapTime).DefaultIfEmpty(60).Min();
            double maxTime = recent.Where(l => l.LapTime > 0).Select(l => l.LapTime).DefaultIfEmpty(120).Max();
            double range = Math.Max(1, maxTime - minTime);
            double padTop = 15, padBot = 20, padL = 5, padR = 5;
            double drawW = GW - padL - padR;
            double drawH = GH - padTop - padBot;

            // Best time reference line
            if (bestTime > 0)
            {
                double by = padTop + drawH - ((bestTime - minTime) / range * drawH);
                by = Math.Clamp(by, padTop, padTop + drawH);
                var bestLine = new Line { X1 = padL, X2 = GW - padR, Y1 = by, Y2 = by, Stroke = new SolidColorBrush(Color.FromArgb(60, 76, 217, 100)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 } };
                _graphCanvas.Children.Add(bestLine);

                var bestLabel = new TextBlock { Text = $"Best {Fmt(bestTime)}", FontSize = 7, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(76, 217, 100)) };
                Canvas.SetLeft(bestLabel, padL + 2); Canvas.SetTop(bestLabel, by - 10);
                _graphCanvas.Children.Add(bestLabel);
            }

            // Lap time line
            var points = new PointCollection();
            for (int i = 0; i < recent.Count; i++)
            {
                double x = padL + (i / (double)(GRAPH_LAPS - 1)) * drawW;
                double y = padTop + drawH - ((recent[i].LapTime - minTime) / range * drawH);
                y = Math.Clamp(y, padTop, padTop + drawH);
                points.Add(new Point(x, y));
            }

            var line = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            _graphCanvas.Children.Add(line);

            // Dots + labels
            for (int i = 0; i < recent.Count; i++)
            {
                var pt = points[i];
                bool isBest = bestTime > 0 && Math.Abs(recent[i].LapTime - bestTime) < 0.001;
                Color dotCol = isBest ? Color.FromRgb(76, 217, 100) : Color.FromRgb(88, 166, 255);

                var dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(dotCol) };
                Canvas.SetLeft(dot, pt.X - 3.5); Canvas.SetTop(dot, pt.Y - 3.5);
                _graphCanvas.Children.Add(dot);

                // Time label above dot
                var label = new TextBlock
                {
                    Text = Fmt(recent[i].LapTime), FontSize = 7,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(dotCol)
                };
                Canvas.SetLeft(label, pt.X - 18); Canvas.SetTop(label, pt.Y - 12);
                _graphCanvas.Children.Add(label);

                // Lap number below
                var lapNum = new TextBlock
                {
                    Text = $"L{recent[i].LapNumber}", FontSize = 7,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 100, 100))
                };
                Canvas.SetLeft(lapNum, pt.X - 8); Canvas.SetTop(lapNum, GH - padBot + 4);
                _graphCanvas.Children.Add(lapNum);
            }
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private static void H(Grid g, string t, int c)
        {
            var tb = new TextBlock { Text = t, FontSize = 7, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(60, 80, 80)), HorizontalAlignment = c == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right };
            Grid.SetColumn(tb, c); g.Children.Add(tb);
        }

        private static void V(Grid g, string t, int c, Color col, HorizontalAlignment ha = HorizontalAlignment.Right)
        {
            var tb = new TextBlock { Text = t, FontSize = 10, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(col), HorizontalAlignment = ha };
            Grid.SetColumn(tb, c); g.Children.Add(tb);
        }

        private static string Fmt(double t)
        {
            if (t <= 0) return "--:--.---";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        private static string FS(double t) => t > 0 ? $"{t:F2}" : "--.--";
    }
}
