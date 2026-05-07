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
        private readonly HashSet<string> _channels = new() { "Speed" };
        private string           _trackName    = "";

        private static readonly Dictionary<string, DoubleCollection?> ChannelDash = new()
        {
            ["Speed"]    = null,
            ["Throttle"] = new DoubleCollection { 8, 3 },
            ["Brake"]    = new DoubleCollection { 4, 2 },
            ["RPM"]      = new DoubleCollection { 12, 3 },
            ["Gear"]     = new DoubleCollection { 2, 2 },
            ["Steering"] = new DoubleCollection { 8, 2, 2, 2 },
        };

        // Tours de référence (séparés des tours de session)
        private LapTrace? _allTimeBest;
        private LapTrace? _opponentBest;
        private static readonly Color AllTimeBestColor  = Color.FromRgb(255, 215,   0); // Or
        private static readonly Color OpponentBestColor = Color.FromRgb(255, 140,   0); // Orange

        // Services (set externally)
        public DataService? DataService { get; set; }

        // ── Zoom / pan ────────────────────────────────────────────────────
        private double _zoomMin = 0.0;
        private double _zoomMax = 1.0;

        private bool   _isPanning;
        private double _panStartX;
        private double _panZoomMinAtStart;
        private double _panZoomMaxAtStart;

        // ── Cursor overlay (persistent elements re-added each DrawChart) ──
        private Line        _cursorLine    = null!;
        private Border      _cursorTooltip = null!;
        private TextBlock   _cursorText    = null!;

        // ====================================================================
        // INIT
        // ====================================================================

        public TelemetryPanel()
        {
            InitializeComponent();
            BuildCursorElements();
        }

        private void BuildCursorElements()
        {
            _cursorLine = new Line
            {
                Stroke          = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Visibility      = Visibility.Hidden,
                IsHitTestVisible = false
            };

            _cursorText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                LineHeight = 14
            };

            _cursorTooltip = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(210, 15, 15, 15)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(100, 80, 80, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Padding         = new Thickness(6, 4, 6, 4),
                Child           = _cursorText,
                Visibility      = Visibility.Hidden,
                IsHitTestVisible = false
            };
        }

        // Called by MainWindow when switching to TELEMETRY tab
        public void Refresh(DataService ds, string trackName)
        {
            DataService   = ds;
            _trackName    = trackName;
            _traces       = ds.GetLapTraces();
            _lapRecords   = ds.GetLapHistory();
            _allTimeBest  = ds.GetAllTimeBestTrace();
            _opponentBest = ds.GetOpponentBestTrace();
            SyncRecordButton();
            RebuildLapList();
            DrawChart();
        }

        private void SyncRecordButton()
        {
            if (DataService == null) return;
            bool rec = DataService.IsRecordingTelemetry;
            BtnRecord.Content    = rec ? "⏺ ENREG. ON" : "⏹ ENREG. OFF";
            BtnRecord.Foreground = rec
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void OnToggleRecording(object s, RoutedEventArgs e)
        {
            if (DataService == null) return;
            DataService.IsRecordingTelemetry = !DataService.IsRecordingTelemetry;
            SyncRecordButton();
        }

        // ====================================================================
        // LAP LIST
        // ====================================================================

        private void AddRefRow(string label, string time, Color color)
        {
            var sep = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(4, 4, 4, 4)
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = "★ ", Foreground = new SolidColorBrush(color),
                FontFamily = new FontFamily("Consolas"), FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(color),
                FontFamily = new FontFamily("Consolas"), FontSize = 10, FontWeight = FontWeights.Bold
            });
            info.Children.Add(new TextBlock
            {
                Text = time, Foreground = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B)),
                FontFamily = new FontFamily("Consolas"), FontSize = 10
            });
            stack.Children.Add(info);
            sep.Child = stack;
            LapListPanel.Children.Add(sep);
        }

        private void RebuildLapList()
        {
            LapListPanel.Children.Clear();
            _checked = new List<bool>(new bool[_traces.Count]);

            if (_allTimeBest != null)
                AddRefRow("BEST ALL-TIME", FormatTime(_allTimeBest.LapTime), AllTimeBestColor);
            if (_opponentBest != null)
                AddRefRow($"ADV. {_opponentBest.Compound}", FormatTime(_opponentBest.LapTime), OpponentBestColor);

            for (int i = Math.Max(0, _traces.Count - 3); i < _traces.Count; i++)
                _checked[i] = true;

            for (int i = 0; i < _traces.Count; i++)
            {
                var trace = _traces[i];
                var color = LapColors[i % LapColors.Length];
                int idx   = i;

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
                    Foreground = new SolidColorBrush(Color.FromRgb(82, 82, 82)),
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
                    Foreground = new SolidColorBrush(Color.FromRgb(82, 82, 82)),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10,
                    Margin = new Thickness(10, 10, 10, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            UpdateLegend();
        }

        private void UpdateLegend()
        {
            LegendPanel.Children.Clear();

            if (_allTimeBest != null)
                AddLegendItem("★ ALL-TIME", AllTimeBestColor, solid: true);
            if (_opponentBest != null && _channels.Contains("Speed"))
                AddLegendItem($"★ {_opponentBest.Compound}", OpponentBestColor, solid: false);

            for (int i = 0; i < _traces.Count; i++)
            {
                if (!_checked[i]) continue;
                AddLegendItem($"T{_traces[i].LapNumber}", LapColors[i % LapColors.Length], solid: true);
            }
        }

        private void AddLegendItem(string label, Color color, bool solid)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
            item.Children.Add(new Border
            {
                Width = 12, Height = 3,
                Background   = solid ? new SolidColorBrush(color) : Brushes.Transparent,
                BorderBrush  = new SolidColorBrush(color),
                BorderThickness = solid ? new Thickness(0) : new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            });
            item.Children.Add(new TextBlock
            {
                Text       = label,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 9,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });
            LegendPanel.Children.Add(item);
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

            foreach (var channel in _channels)
            {
                ChannelDash.TryGetValue(channel, out var dash);

                if (_allTimeBest?.Points.Count >= 2)
                    DrawTrace(_allTimeBest, AllTimeBestColor, plotW, plotH, channel, thickness: 2.0, opacity: 0.75);

                if (_opponentBest?.Points.Count >= 2 && channel == "Speed")
                    DrawTrace(_opponentBest, OpponentBestColor, plotW, plotH, channel, thickness: 1.5, opacity: 0.6, dashed: true);

                for (int i = 0; i < _traces.Count; i++)
                {
                    if (!_checked[i] || _traces[i].Points.Count < 2) continue;
                    DrawTrace(_traces[i], LapColors[i % LapColors.Length], plotW, plotH, channel, dashOverride: dash);
                }
            }

            // Cursor overlay elements — always on top, initially hidden
            _cursorLine.Visibility    = Visibility.Hidden;
            _cursorTooltip.Visibility = Visibility.Hidden;
            ChartCanvas.Children.Add(_cursorLine);
            ChartCanvas.Children.Add(_cursorTooltip);
        }

        private void DrawAxes(double plotW, double plotH, double totalW, double totalH)
        {
            var axisBrush  = new SolidColorBrush(Color.FromRgb(38, 38, 38));
            var labelBrush = new SolidColorBrush(Color.FromRgb(82, 82, 82));
            var fontFamily = new FontFamily("Consolas");

            DrawLine(PADDING_LEFT, PADDING_TOP + plotH, PADDING_LEFT + plotW, PADDING_TOP + plotH, axisBrush, 1);
            DrawLine(PADDING_LEFT, PADDING_TOP, PADDING_LEFT, PADDING_TOP + plotH, axisBrush, 1);

            double zoomSpan = _zoomMax - _zoomMin;
            for (int t = 0; t <= 4; t++)
            {
                double x = PADDING_LEFT + (t / 4.0) * plotW;
                DrawLine(x, PADDING_TOP, x, PADDING_TOP + plotH, axisBrush, 0.5);
                double trackPct = (_zoomMin + (t / 4.0) * zoomSpan) * 100.0;
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

            double yMin, yMax;
            string fmt;
            if (_channels.Count == 1)
            {
                string singleCh = "Speed";
                foreach (var c in _channels) { singleCh = c; break; }
                (yMin, yMax, fmt) = GetChannelRange(singleCh);
            }
            else
            {
                yMin = 0; yMax = 100; fmt = "{0:F0}%";
            }

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

        private void DrawTrace(LapTrace trace, Color color, double plotW, double plotH,
                               string channel,
                               double thickness = 1.5, double opacity = 1.0,
                               bool dashed = false, DoubleCollection? dashOverride = null)
        {
            var (yMin, yMax, _) = GetChannelRange(channel);
            double yRange   = yMax - yMin;
            if (yRange <= 0) yRange = 1;
            double zoomSpan = _zoomMax - _zoomMin;
            if (zoomSpan <= 0) zoomSpan = 1;

            var pts = new PointCollection();
            foreach (var p in trace.Points)
            {
                double val = GetValue(p, channel);
                double cx  = PADDING_LEFT + ((p.TrackPos - _zoomMin) / zoomSpan) * plotW;
                double cy  = PADDING_TOP  + (1.0 - (val - yMin) / yRange) * plotH;
                pts.Add(new Point(cx, cy));
            }

            var brush = new SolidColorBrush(color) { Opacity = opacity };
            var poly  = new Polyline
            {
                Points          = pts,
                Stroke          = brush,
                StrokeThickness = thickness,
                StrokeLineJoin  = PenLineJoin.Round
            };
            if (dashed)
                poly.StrokeDashArray = new DoubleCollection { 6, 3 };
            else if (dashOverride != null)
                poly.StrokeDashArray = dashOverride;

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
        // ZOOM / PAN
        // ====================================================================

        private void OnChartMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double w = ChartCanvas.ActualWidth;
            double plotW = w - PADDING_LEFT - PADDING_RIGHT;
            if (plotW <= 0) return;

            var    pos           = e.GetPosition(ChartCanvas);
            double zoomSpan      = _zoomMax - _zoomMin;
            double mouseTrackPos = _zoomMin + ((pos.X - PADDING_LEFT) / plotW) * zoomSpan;
            mouseTrackPos = Math.Clamp(mouseTrackPos, _zoomMin, _zoomMax);

            double factor  = e.Delta > 0 ? 0.75 : 1.33;
            double newSpan = Math.Clamp(zoomSpan * factor, 0.02, 1.0);

            double newMin = mouseTrackPos - (mouseTrackPos - _zoomMin) * (newSpan / zoomSpan);
            double newMax = newMin + newSpan;

            if (newMin < 0) { newMax -= newMin; newMin = 0; }
            if (newMax > 1) { newMin -= (newMax - 1); newMax = 1; }

            _zoomMin = Math.Clamp(newMin, 0, 1);
            _zoomMax = Math.Clamp(newMax, 0, 1);
            if (_zoomMax - _zoomMin < 0.02) _zoomMax = _zoomMin + 0.02;

            DrawChart();
            e.Handled = true;
        }

        private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _isPanning           = true;
            _panStartX           = e.GetPosition(ChartCanvas).X;
            _panZoomMinAtStart   = _zoomMin;
            _panZoomMaxAtStart   = _zoomMax;
            ChartCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void OnChartMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _isPanning = false;
            ChartCanvas.ReleaseMouseCapture();
        }

        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            double w     = ChartCanvas.ActualWidth;
            double h     = ChartCanvas.ActualHeight;
            double plotW = w - PADDING_LEFT - PADDING_RIGHT;
            double plotH = h - PADDING_TOP  - PADDING_BOTTOM;
            if (plotW <= 0 || plotH <= 0) return;

            var pos = e.GetPosition(ChartCanvas);

            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                double dx      = pos.X - _panStartX;
                double span    = _panZoomMaxAtStart - _panZoomMinAtStart;
                double dTrack  = -(dx / plotW) * span;
                double newMin  = _panZoomMinAtStart + dTrack;
                double newMax  = _panZoomMaxAtStart + dTrack;

                if (newMin < 0) { newMin = 0; newMax = span; }
                if (newMax > 1) { newMax = 1; newMin = 1 - span; }

                _zoomMin = Math.Clamp(newMin, 0, 1);
                _zoomMax = Math.Clamp(newMax, 0, 1);
                DrawChart();
                return;
            }

            // Cursor line
            double cx = pos.X;
            if (cx >= PADDING_LEFT && cx <= PADDING_LEFT + plotW &&
                pos.Y >= PADDING_TOP && pos.Y <= PADDING_TOP + plotH)
            {
                _cursorLine.X1 = cx; _cursorLine.Y1 = PADDING_TOP;
                _cursorLine.X2 = cx; _cursorLine.Y2 = PADDING_TOP + plotH;
                _cursorLine.Visibility = Visibility.Visible;

                double trackPos = _zoomMin + ((cx - PADDING_LEFT) / plotW) * (_zoomMax - _zoomMin);
                UpdateCursorTooltip(cx, w, trackPos);
            }
            else
            {
                _cursorLine.Visibility    = Visibility.Hidden;
                _cursorTooltip.Visibility = Visibility.Hidden;
            }
        }

        private void OnChartMouseLeave(object sender, MouseEventArgs e)
        {
            _cursorLine.Visibility    = Visibility.Hidden;
            _cursorTooltip.Visibility = Visibility.Hidden;
            if (_isPanning)
            {
                _isPanning = false;
                ChartCanvas.ReleaseMouseCapture();
            }
        }

        private void OnResetZoom(object sender, RoutedEventArgs e)
        {
            _zoomMin = 0.0;
            _zoomMax = 1.0;
            DrawChart();
        }

        private void UpdateCursorTooltip(double cx, double canvasW, double trackPos)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Pos: {trackPos * 100:F1}%");

            var allTraces = new List<(string label, LapTrace trace)>();
            if (_allTimeBest?.Points.Count >= 2)
                allTraces.Add(("BEST", _allTimeBest));
            if (_opponentBest?.Points.Count >= 2)
                allTraces.Add(("ADV", _opponentBest));
            for (int i = 0; i < _traces.Count; i++)
                if (_checked[i] && _traces[i].Points.Count >= 2)
                    allTraces.Add(($"T{_traces[i].LapNumber}", _traces[i]));

            string firstChannel = _channels.Count > 0 ? _channels.First() : "Speed";
            foreach (var (label, trace) in allTraces)
            {
                double val = InterpolateTrace(trace, trackPos, firstChannel);
                sb.AppendLine($"{label}: {FormatValue(val, firstChannel)}");
            }

            _cursorText.Text = sb.ToString().TrimEnd();

            double tooltipLeft = cx + 10;
            if (tooltipLeft + 120 > canvasW - PADDING_RIGHT)
                tooltipLeft = cx - 130;

            Canvas.SetLeft(_cursorTooltip, tooltipLeft);
            Canvas.SetTop(_cursorTooltip,  PADDING_TOP + 4);
            _cursorTooltip.Visibility = Visibility.Visible;
        }

        private static double InterpolateTrace(LapTrace trace, double trackPos, string channel)
        {
            var pts = trace.Points;
            if (pts.Count == 0) return 0;
            int lo = 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (pts[i].TrackPos <= trackPos) lo = i;
                else break;
            }
            int hi = Math.Min(lo + 1, pts.Count - 1);
            if (lo == hi) return GetValue(pts[lo], channel);
            double span = pts[hi].TrackPos - pts[lo].TrackPos;
            if (span <= 0) return GetValue(pts[lo], channel);
            double t = (trackPos - pts[lo].TrackPos) / span;
            return GetValue(pts[lo], channel) + t * (GetValue(pts[hi], channel) - GetValue(pts[lo], channel));
        }

        // ====================================================================
        // CHANNEL HELPERS
        // ====================================================================

        private static double GetValue(TelemetryPoint p, string channel) => channel switch
        {
            "Speed"    => p.Speed,
            "Throttle" => p.Throttle * 100,
            "Brake"    => p.Brake    * 100,
            "RPM"      => p.RPM,
            "Gear"     => p.Gear,
            "Steering" => p.Steering * 100,
            _          => p.Speed
        };

        private static (double min, double max, string fmt) GetChannelRange(string channel) => channel switch
        {
            "Speed"    => (0,    350,   "{0:F0}"),
            "Throttle" => (0,    100,   "{0:F0}%"),
            "Brake"    => (0,    100,   "{0:F0}%"),
            "RPM"      => (0,    15000, "{0:F0}"),
            "Gear"     => (0,    10,    "{0:F0}"),
            "Steering" => (-100, 100,   "{0:F0}"),
            _          => (0,    350,   "{0:F0}")
        };

        private static string FormatValue(double v, string channel) => channel switch
        {
            "Speed"    => $"{v:F0} km/h",
            "Throttle" => $"{v:F0}%",
            "Brake"    => $"{v:F0}%",
            "RPM"      => $"{v:F0} tr/m",
            "Gear"     => $"{v:F0}",
            "Steering" => $"{v:F0}%",
            _          => $"{v:F0}"
        };

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void OnChannelCheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is string ch)
            {
                if (cb.IsChecked == true) _channels.Add(ch);
                else _channels.Remove(ch);
            }
            DrawChart();
        }

        private void OnChartSizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

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
                string carClass = DataService?.CurrentCarClass ?? "";
                string path = _excel.Export(_lapRecords, _traces, _trackName, carClass);
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
                var (laps, traces, trackName, carClass) = _excel.Import(dlg.FileName);
                _lapRecords = laps;
                _traces     = traces;
                // Use circuit name from file metadata; fall back to filename if old format
                _trackName  = !string.IsNullOrEmpty(trackName)
                    ? trackName
                    : System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);

                // Load matching all-time best + opponent best from disk for this circuit
                if (DataService != null && !string.IsNullOrEmpty(trackName))
                    DataService.LoadBestsForTrack(trackName, carClass);

                _allTimeBest  = DataService?.GetAllTimeBestTrace();
                _opponentBest = DataService?.GetOpponentBestTrace();

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
