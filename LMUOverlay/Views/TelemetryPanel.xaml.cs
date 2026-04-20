using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;
using Microsoft.Win32;

namespace LMUOverlay.Views
{
    public partial class TelemetryPanel : UserControl
    {
        // ====================================================================
        // CONSTANTS
        // ====================================================================

        private static readonly Color[] LapColors =
        {
            Color.FromRgb(0,   204, 170),  // teal
            Color.FromRgb(255, 200,  50),  // yellow
            Color.FromRgb(255,  80,  80),  // red
            Color.FromRgb(100, 180, 255),  // blue
            Color.FromRgb(200, 100, 255),  // purple
            Color.FromRgb(255, 150,  50),  // orange
            Color.FromRgb(180, 255, 100),  // lime
            Color.FromRgb(255, 100, 180),  // pink
        };

        private const int PADDING_LEFT   = 50;
        private const int PADDING_RIGHT  = 16;
        private const int PADDING_TOP    = 16;
        private const int PADDING_BOTTOM = 30;

        // ====================================================================
        // STATE
        // ====================================================================

        private readonly ExcelExportService _excel = new();

        private List<LapTrace>   _traces       = new();
        private List<LapRecord>  _lapRecords   = new();
        private List<bool>       _checked      = new();
        private string           _channel      = "Speed";
        private string           _trackName    = "";

        // Zoom / pan
        private double _zoomMin = 0.0;
        private double _zoomMax = 1.0;
        private bool   _isPanning;
        private Point  _panAnchor;
        private double _panZoomMinAtStart;
        private double _panZoomMaxAtStart;

        // Services (set externally)
        public DataService?      DataService   { get; set; }

        // ====================================================================
        // INIT
        // ====================================================================

        public TelemetryPanel() => InitializeComponent();

        // Called by MainWindow when switching to TELEMETRY tab
        public void Refresh(DataService ds, string trackName)
        {
            DataService = ds;
            _trackName  = trackName;
            _traces     = ds.GetLapTraces();
            _lapRecords = ds.GetLapHistory();
            RebuildLapList();
            DrawChart();
        }

        // ====================================================================
        // LAP LIST
        // ====================================================================

        private void RebuildLapList()
        {
            LapListPanel.Children.Clear();
            _checked = new List<bool>(new bool[_traces.Count]);

            // Auto-select last 3 laps
            for (int i = Math.Max(0, _traces.Count - 3); i < _traces.Count; i++)
                _checked[i] = true;

            for (int i = 0; i < _traces.Count; i++)
            {
                var trace = _traces[i];
                var color = LapColors[i % LapColors.Length];
                int idx   = i; // capture for closure

                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                row.ColumnDefinitions.Add(new ColumnDefinition());

                var cb = new CheckBox
                {
                    IsChecked = _checked[idx],
                    VerticalAlignment = VerticalAlignment.Center
                };
                cb.Checked   += (_, _) => { _checked[idx] = true;  DrawChart(); };
                cb.Unchecked += (_, _) => { _checked[idx] = false; DrawChart(); };
                Grid.SetColumn(cb, 0);

                var dot = new Ellipse
                {
                    Width = 6, Height = 6,
                    Fill = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 1);

                var m = _lapRecords.FirstOrDefault(l => l.LapNumber == trace.LapNumber);
                string s1 = m != null ? FormatTime(m.Sector1) : "-";
                string s2 = m != null ? FormatTime(m.Sector2) : "-";
                string s3 = m != null ? FormatTime(m.Sector3) : "-";

                var info = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
                info.Children.Add(new TextBlock
                {
                    Text = $"T{trace.LapNumber}  {FormatTime(trace.LapTime)}",
                    Foreground = new SolidColorBrush(color),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10, FontWeight = FontWeights.Bold
                });
                info.Children.Add(new TextBlock
                {
                    Text = $"  {s1}  {s2}  {s3}",
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 110, 110)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9
                });
                Grid.SetColumn(info, 2);

                row.Children.Add(cb);
                row.Children.Add(dot);
                row.Children.Add(info);
                LapListPanel.Children.Add(row);
            }

            if (_traces.Count == 0)
            {
                LapListPanel.Children.Add(new TextBlock
                {
                    Text = "Aucun tour enregistré.\nFaites au moins 1 tour\npuis revenez ici.",
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 110, 110)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Margin = new Thickness(8, 8, 8, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            UpdateLegend();
        }

        private void UpdateLegend()
        {
            LegendPanel.Children.Clear();
            for (int i = 0; i < _traces.Count; i++)
            {
                if (!_checked[i]) continue;
                var color = LapColors[i % LapColors.Length];
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
                item.Children.Add(new Border
                {
                    Width = 12, Height = 3,
                    Background = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                });
                item.Children.Add(new TextBlock
                {
                    Text = $"T{_traces[i].LapNumber}",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center
                });
                LegendPanel.Children.Add(item);
            }
        }

        // ====================================================================
        // CHART DRAWING
        // ====================================================================

        private void DrawChart()
        {
            if (ChartCanvas == null) return;
            ChartCanvas.Children.Clear();
            UpdateLegend();

            double w = ChartCanvas.ActualWidth;
            double h = ChartCanvas.ActualHeight;
            if (w < 50 || h < 50) return;

            double plotW = w - PADDING_LEFT - PADDING_RIGHT;
            double plotH = h - PADDING_TOP  - PADDING_BOTTOM;

            DrawAxes(plotW, plotH, w, h);

            for (int i = 0; i < _traces.Count; i++)
            {
                if (!_checked[i] || _traces[i].Points.Count < 2) continue;
                DrawTrace(_traces[i], LapColors[i % LapColors.Length], plotW, plotH);
            }
        }

        private void DrawAxes(double plotW, double plotH, double totalW, double totalH)
        {
            var axisBrush = new SolidColorBrush(Color.FromRgb(36, 68, 68));
            var labelBrush = new SolidColorBrush(Color.FromRgb(80, 110, 110));
            var fontFamily = new FontFamily("Consolas");

            // X axis
            DrawLine(PADDING_LEFT, PADDING_TOP + plotH, PADDING_LEFT + plotW, PADDING_TOP + plotH, axisBrush, 1);

            // Y axis
            DrawLine(PADDING_LEFT, PADDING_TOP, PADDING_LEFT, PADDING_TOP + plotH, axisBrush, 1);

            // X grid lines + labels (reflect current zoom window)
            for (int t = 0; t <= 4; t++)
            {
                double x = PADDING_LEFT + (t / 4.0) * plotW;
                DrawLine(x, PADDING_TOP, x, PADDING_TOP + plotH, axisBrush, 0.5);
                double trackPct = (_zoomMin + (t / 4.0) * (_zoomMax - _zoomMin)) * 100;
                var tb = new TextBlock
                {
                    Text = $"{trackPct:F0}%",
                    FontFamily = fontFamily, FontSize = 8,
                    Foreground = labelBrush
                };
                Canvas.SetLeft(tb, x - 10);
                Canvas.SetTop(tb, PADDING_TOP + plotH + 4);
                ChartCanvas.Children.Add(tb);
            }

            // Y axis labels (0%, 50%, 100%)
            var (yMin, yMax, fmt) = GetChannelRange();
            for (int t = 0; t <= 4; t++)
            {
                double y = PADDING_TOP + (1.0 - t / 4.0) * plotH;
                DrawLine(PADDING_LEFT, y, PADDING_LEFT + plotW, y, axisBrush, 0.5);
                double val = yMin + (t / 4.0) * (yMax - yMin);
                var tb = new TextBlock
                {
                    Text = string.Format(fmt, val),
                    FontFamily = fontFamily, FontSize = 8,
                    Foreground = labelBrush
                };
                Canvas.SetRight(tb, totalW - PADDING_LEFT + 4);
                Canvas.SetTop(tb, y - 6);
                ChartCanvas.Children.Add(tb);
            }
        }

        private void DrawTrace(LapTrace trace, Color color, double plotW, double plotH)
        {
            var (yMin, yMax, _) = GetChannelRange();
            double yRange     = yMax - yMin;
            if (yRange <= 0) yRange = 1;
            double zoomRange  = _zoomMax - _zoomMin;
            if (zoomRange <= 0) zoomRange = 1;

            var pts = new PointCollection();
            foreach (var p in trace.Points)
            {
                if (p.TrackPos < _zoomMin - 0.001 || p.TrackPos > _zoomMax + 0.001) continue;
                double val = GetValue(p);
                double cx = PADDING_LEFT + (p.TrackPos - _zoomMin) / zoomRange * plotW;
                double cy = PADDING_TOP  + (1.0 - (val - yMin) / yRange) * plotH;
                pts.Add(new Point(cx, cy));
            }

            if (pts.Count < 2) return;
            var poly = new Polyline
            {
                Points = pts,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round
            };
            ChartCanvas.Children.Add(poly);
        }

        private void DrawLine(double x1, double y1, double x2, double y2, Brush brush, double thickness)
        {
            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = brush, StrokeThickness = thickness
            };
            ChartCanvas.Children.Add(line);
        }

        // ====================================================================
        // CHANNEL HELPERS
        // ====================================================================

        private double GetValue(TelemetryPoint p) => _channel switch
        {
            "Speed"    => p.Speed,
            "Throttle" => p.Throttle * 100,
            "Brake"    => p.Brake    * 100,
            "RPM"      => p.RPM,
            "Gear"     => p.Gear,
            "Steering" => p.Steering * 100,
            _          => p.Speed
        };

        private (double min, double max, string fmt) GetChannelRange() => _channel switch
        {
            "Speed"    => (0,    350,   "{0:F0}"),
            "Throttle" => (0,    100,   "{0:F0}%"),
            "Brake"    => (0,    100,   "{0:F0}%"),
            "RPM"      => (0,    15000, "{0:F0}"),
            "Gear"     => (0,    10,    "{0:F0}"),
            "Steering" => (-100, 100,   "{0:F0}"),
            _          => (0,    350,   "{0:F0}")
        };

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void OnChannelChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbChannel.SelectedItem is ComboBoxItem item)
                _channel = item.Tag?.ToString() ?? "Speed";
            DrawChart();
        }

        private void OnChartSizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

        private void OnChartMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double plotW = ChartCanvas.ActualWidth - PADDING_LEFT - PADDING_RIGHT;
            if (plotW <= 0) return;

            double mouseX    = e.GetPosition(ChartCanvas).X;
            double zoomRange = _zoomMax - _zoomMin;
            double trackPos  = _zoomMin + (mouseX - PADDING_LEFT) / plotW * zoomRange;
            trackPos = Math.Clamp(trackPos, _zoomMin, _zoomMax);

            double factor   = e.Delta > 0 ? 0.75 : 1.333;
            double newRange = Math.Clamp(zoomRange * factor, 0.02, 1.0);
            double ratio    = (trackPos - _zoomMin) / zoomRange;

            _zoomMin = trackPos - ratio * newRange;
            _zoomMax = trackPos + (1.0 - ratio) * newRange;

            if (_zoomMin < 0) { _zoomMax -= _zoomMin; _zoomMin = 0; }
            if (_zoomMax > 1) { _zoomMin -= _zoomMax - 1; _zoomMax = 1; }
            _zoomMin = Math.Max(0, _zoomMin);
            _zoomMax = Math.Min(1, _zoomMax);

            DrawChart();
            e.Handled = true;
        }

        private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                _isPanning           = true;
                _panAnchor           = e.GetPosition(ChartCanvas);
                _panZoomMinAtStart   = _zoomMin;
                _panZoomMaxAtStart   = _zoomMax;
                ChartCanvas.CaptureMouse();
            }
        }

        private void OnChartDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _zoomMin = 0;
            _zoomMax = 1;
            DrawChart();
        }

        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            var    pos   = e.GetPosition(ChartCanvas);
            double plotW = ChartCanvas.ActualWidth  - PADDING_LEFT - PADDING_RIGHT;
            double plotH = ChartCanvas.ActualHeight - PADDING_TOP  - PADDING_BOTTOM;

            if (_isPanning && plotW > 0)
            {
                double dx       = pos.X - _panAnchor.X;
                double dTrack   = dx / plotW * (_panZoomMaxAtStart - _panZoomMinAtStart);
                double newMin   = _panZoomMinAtStart - dTrack;
                double newMax   = _panZoomMaxAtStart - dTrack;
                double span     = _panZoomMaxAtStart - _panZoomMinAtStart;
                if (newMin < 0) { newMin = 0; newMax = span; }
                if (newMax > 1) { newMax = 1; newMin = 1 - span; }
                _zoomMin = newMin;
                _zoomMax = newMax;
                DrawChart();
            }

            UpdateCursorLine(pos, plotW, plotH);
        }

        private void OnChartMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            ChartCanvas.ReleaseMouseCapture();
        }

        private void OnChartMouseLeave(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            ChartCanvas.ReleaseMouseCapture();
            CursorCanvas.Children.Clear();
        }

        private void UpdateCursorLine(Point pos, double plotW, double plotH)
        {
            CursorCanvas.Children.Clear();
            double x = pos.X;
            if (x < PADDING_LEFT || x > PADDING_LEFT + plotW || plotW <= 0) return;

            var lineBrush = new SolidColorBrush(Color.FromArgb(160, 220, 220, 220));
            var line = new Line
            {
                X1 = x, Y1 = PADDING_TOP,
                X2 = x, Y2 = PADDING_TOP + plotH,
                Stroke = lineBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            CursorCanvas.Children.Add(line);

            double trackPct   = (_zoomMin + (x - PADDING_LEFT) / plotW * (_zoomMax - _zoomMin)) * 100;
            var label = new TextBlock
            {
                Text       = $"{trackPct:F1}%",
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 8,
                Foreground = lineBrush,
                Background = new SolidColorBrush(Color.FromArgb(140, 13, 22, 22))
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double labelX = x + 4;
            if (labelX + label.DesiredSize.Width > PADDING_LEFT + plotW)
                labelX = x - 4 - label.DesiredSize.Width;
            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, PADDING_TOP + 2);
            CursorCanvas.Children.Add(label);
        }

        private void OnClearTraces(object s, RoutedEventArgs e)
        {
            _traces.Clear();
            _lapRecords.Clear();
            RebuildLapList();
            DrawChart();
        }

        private void OnExport(object s, RoutedEventArgs e)
        {
            if (_traces.Count == 0 && _lapRecords.Count == 0)
            {
                MessageBox.Show("Aucune donnée à exporter.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                string path = _excel.Export(_lapRecords, _traces, _trackName);
                MessageBox.Show($"Exporté :\n{path}", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur export : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnImport(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Importer télémétrie",
                Filter = "Fichiers Excel (*.xlsx)|*.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var (laps, traces) = _excel.Import(dlg.FileName);
                _lapRecords = laps;
                _traces     = traces;
                _trackName  = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                RebuildLapList();
                DrawChart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur import : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====================================================================
        // HELPERS
        // ====================================================================

        private static string FormatTime(double s)
        {
            if (s <= 0) return "-";
            int m = (int)(s / 60);
            double sec = s - m * 60;
            return $"{m}:{sec:00.000}";
        }
    }
}
