using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// LAPTIME LOG overlay — toggle between TABLE and GRAPH mode (click title).
    /// TABLE : session-best bar (purple) + per-lap list with time / delta / track temp.
    /// GRAPH : lap-time curve, dots colored green/red by improvement, purple = best, X = latest.
    /// </summary>
    public class LapHistoryOverlay : BaseOverlayWindow
    {
        // ────────────────────────────────────────────────────────────────────
        // PALETTE
        // ────────────────────────────────────────────────────────────────────
        private static readonly Color CBackground = Color.FromRgb(0x16, 0x16, 0x16);
        private static readonly Color CHeaderBg   = Color.FromRgb(0x22, 0x22, 0x22);
        private static readonly Color CBestBg     = Color.FromRgb(0x6B, 0x21, 0xA8);
        private static readonly Color CPurple     = Color.FromRgb(0xC0, 0x5A, 0xFF);
        private static readonly Color CYellow     = Color.FromRgb(0xFF, 0xC1, 0x07);
        private static readonly Color CWhite      = Color.FromRgb(0xFF, 0xFF, 0xFF);
        private static readonly Color CGreen      = Color.FromRgb(0x44, 0xDD, 0x44);
        private static readonly Color CRed        = Color.FromRgb(0xFF, 0x44, 0x44);
        private static readonly Color CGray       = Color.FromRgb(0x55, 0x55, 0x55);
        private static readonly Color CDivider    = Color.FromRgb(0x2C, 0x2C, 0x2C);
        private static readonly Color CSubtle     = Color.FromRgb(0xAA, 0xAA, 0xAA);
        private static readonly Color CGrid       = Color.FromArgb(45, 0xFF, 0xFF, 0xFF);

        private static readonly FontFamily FBold = new("Segoe UI");
        private static readonly FontFamily FMono = new("Consolas");

        // ────────────────────────────────────────────────────────────────────
        // TABLE LAYOUT CONSTANTS
        // ────────────────────────────────────────────────────────────────────
        private const double TotalW  = 320;
        private const double PadH    = 10;
        private const double ColLap  = 36;
        private const double ColTime = 96;
        private const double ColDelta= 78;

        // ────────────────────────────────────────────────────────────────────
        // GRAPH LAYOUT CONSTANTS
        // ────────────────────────────────────────────────────────────────────
        private const double GW      = TotalW;
        private const double GH      = 210;
        private const double GPadL   = 14;
        private const double GPadR   = 72;   // right margin for Y labels
        private const double GPadT   = 18;
        private const double GPadB   = 14;
        private const int    GL      = 9;    // max laps shown in graph
        private double DrawW => GW - GPadL - GPadR;
        private double DrawH => GH - GPadT - GPadB;

        // ────────────────────────────────────────────────────────────────────
        // CONTROLS
        // ────────────────────────────────────────────────────────────────────
        private readonly TextBlock   _titleModeBtn;  // "[TABLE]" / "[GRAPH]" toggle
        private readonly TextBlock   _bestTimeText;
        private readonly TextBlock   _bestTempText;
        private readonly StackPanel  _rows;
        private readonly Canvas      _graph;
        private readonly Border      _tableView;
        private readonly Border      _graphView;
        private bool _graphMode;
        private int  _lastCount = -1;

        // ────────────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────────────────────────────────────────
        public LapHistoryOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var root = new Border
            {
                Background   = new SolidColorBrush(CBackground),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Width        = TotalW
            };
            var sp = new StackPanel();

            // ── Title bar (click right side to toggle mode) ────────────────
            var titleBar = new Grid
            {
                Background = new SolidColorBrush(CHeaderBg),
                Cursor     = System.Windows.Input.Cursors.Hand
            };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition());
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text              = "LAPTIME LOG",
                FontFamily        = FBold,
                FontSize          = 11,
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(CWhite),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 7, 0, 7),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            titleBar.Children.Add(titleText);

            _titleModeBtn = new TextBlock
            {
                Text              = "[GRAPH]",
                FontFamily        = FMono,
                FontSize          = 8,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(_titleModeBtn, 1);
            titleBar.Children.Add(_titleModeBtn);

            titleBar.MouseLeftButtonDown += (_, _) => ToggleMode();
            sp.Children.Add(titleBar);

            // ── TABLE view ─────────────────────────────────────────────────
            _tableView = new Border();
            var tableSp = new StackPanel();

            // Session best bar
            var bestBar = new Grid { Background = new SolidColorBrush(CBestBg), Margin = new Thickness(0, 1, 0, 1) };
            bestBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            bestBar.ColumnDefinitions.Add(new ColumnDefinition());
            bestBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sessionLabel = new TextBlock
            {
                Text = "SESSION\nBEST", FontFamily = FBold, FontSize = 8.5,
                FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(CWhite),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(PadH, 9, 0, 9), LineHeight = 13
            };
            Grid.SetColumn(sessionLabel, 0);
            bestBar.Children.Add(sessionLabel);

            _bestTimeText = new TextBlock
            {
                Text = "--:--.---", FontFamily = FBold, FontSize = 22, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(CWhite),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(_bestTimeText, 1);
            bestBar.Children.Add(_bestTimeText);

            var tempPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, PadH, 0)
            };
            tempPanel.Children.Add(MakeRoadIcon(16, CWhite));
            _bestTempText = new TextBlock
            {
                Text = "--°C", FontFamily = FBold, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CWhite), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            tempPanel.Children.Add(_bestTempText);
            Grid.SetColumn(tempPanel, 2);
            bestBar.Children.Add(tempPanel);
            tableSp.Children.Add(bestBar);

            // Column headers
            tableSp.Children.Add(MakeHeaderRow());

            // Rows
            _rows = new StackPanel();
            tableSp.Children.Add(new ScrollViewer
            {
                Content = _rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                MaxHeight = 270
            });

            _tableView.Child = tableSp;
            sp.Children.Add(_tableView);

            // ── GRAPH view (hidden by default) ─────────────────────────────
            _graph = new Canvas
            {
                Width = GW, Height = GH, ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(8, 255, 255, 255))
            };
            _graphView = new Border
            {
                Child = _graph,
                Margin = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed
            };
            sp.Children.Add(_graphView);

            root.Child = sp;
            Content = root;
        }

        // ────────────────────────────────────────────────────────────────────
        // MODE TOGGLE
        // ────────────────────────────────────────────────────────────────────
        private void ToggleMode()
        {
            _graphMode = !_graphMode;
            _titleModeBtn.Text      = _graphMode ? "[TABLE]" : "[GRAPH]";
            _tableView.Visibility   = _graphMode ? Visibility.Collapsed : Visibility.Visible;
            _graphView.Visibility   = _graphMode ? Visibility.Visible   : Visibility.Collapsed;
            _lastCount = -1; // force redraw
        }

        // ────────────────────────────────────────────────────────────────────
        // UPDATE
        // ────────────────────────────────────────────────────────────────────
        public override void UpdateData()
        {
            var laps = DataService.GetLapHistory();
            if (laps.Count == _lastCount) return;
            _lastCount = laps.Count;

            if (_graphMode)
                DrawGraph(laps);
            else
                RebuildTable(laps);
        }

        // ────────────────────────────────────────────────────────────────────
        // TABLE
        // ────────────────────────────────────────────────────────────────────
        private void RebuildTable(List<LapRecord> laps)
        {
            double best = laps.Where(l => l.LapTime > 0).Select(l => l.LapTime).DefaultIfEmpty(0).Min();
            _bestTimeText.Text = best > 0 ? Fmt(best) : "--:--.---";

            var latest = laps.LastOrDefault();
            _bestTempText.Text = latest?.TrackTemp > 0 ? $"{latest.TrackTemp:F0}°C" : "--°C";

            _rows.Children.Clear();
            var rev = laps.AsEnumerable().Reverse().Take(25).ToList();
            for (int i = 0; i < rev.Count; i++)
            {
                var prev = i + 1 < rev.Count ? rev[i + 1] : null;
                bool isBest = best > 0 && Math.Abs(rev[i].LapTime - best) < 0.001;
                _rows.Children.Add(MakeLapRow(rev[i], prev, isBest));
            }
        }

        private UIElement MakeLapRow(LapRecord lap, LapRecord? prev, bool isBest)
        {
            var container = new Border
            {
                BorderBrush = new SolidColorBrush(CDivider),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(PadH, 5, PadH, 5)
            };
            if (isBest) container.Background = new SolidColorBrush(Color.FromArgb(18, 0x6B, 0x21, 0xA8));

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColLap) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColTime) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColDelta) });
            row.ColumnDefinitions.Add(new ColumnDefinition());

            Cell(row, $"{lap.LapNumber}", 0, isBest ? CYellow : CWhite, 12, FontWeights.Bold);
            Cell(row, Fmt(lap.LapTime), 1, isBest ? CYellow : CWhite, 12,
                isBest ? FontWeights.Bold : FontWeights.Normal, FMono);

            var (ds, dc) = CalcDelta(lap, prev);
            Cell(row, ds, 2, dc, 11, FontWeights.Normal, FMono);

            var tp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            tp.Children.Add(MakeRoadIcon(13, CSubtle));
            tp.Children.Add(new TextBlock
            {
                Text = lap.TrackTemp > 0 ? $" {lap.TrackTemp:F1}°c" : " --°c",
                FontFamily = FBold, FontSize = 10, Foreground = new SolidColorBrush(CSubtle),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(tp, 3);
            row.Children.Add(tp);

            container.Child = row;
            return container;
        }

        // ────────────────────────────────────────────────────────────────────
        // GRAPH
        // ────────────────────────────────────────────────────────────────────
        private void DrawGraph(List<LapRecord> laps)
        {
            _graph.Children.Clear();

            // Pick last GL valid laps
            var visible = laps.Where(l => l.LapTime > 0).TakeLast(GL).ToList();
            if (visible.Count < 1) { NoDataLabel(); return; }

            double best = visible.Min(l => l.LapTime);

            // Y scale
            double lo, hi;
            CalcYScale(visible, out lo, out hi);
            double step = PickStep(hi - lo);

            // ── Grid lines + Y labels ──────────────────────────────────────
            for (double t = lo; t <= hi + 0.001; t += step)
            {
                double y = TY(t, lo, hi);
                _graph.Children.Add(new Line
                {
                    X1 = GPadL, Y1 = y, X2 = GW - GPadR, Y2 = y,
                    Stroke = new SolidColorBrush(CGrid),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 5 }
                });
                var lbl = new TextBlock
                {
                    Text = FmtY(t), FontFamily = FMono, FontSize = 9,
                    Foreground = new SolidColorBrush(CSubtle)
                };
                Canvas.SetLeft(lbl, GW - GPadR + 6);
                Canvas.SetTop(lbl, y - 7);
                _graph.Children.Add(lbl);
            }

            // ── Compute point positions ────────────────────────────────────
            int n = visible.Count;
            var xs = new double[n];
            var ys = new double[n];
            var col = new Color[n];

            for (int i = 0; i < n; i++)
            {
                xs[i] = n == 1
                    ? GPadL + DrawW / 2
                    : GPadL + i / (double)(n - 1) * DrawW;
                ys[i] = TY(visible[i].LapTime, lo, hi);
                col[i] = DotColor(visible, i, best);
            }

            // ── Lines (gradient from dot to dot color) ─────────────────────
            for (int i = 1; i < n; i++)
                _graph.Children.Add(GradLine(xs[i-1], ys[i-1], xs[i], ys[i], col[i-1], col[i], 2.2));

            // ── Vertical marker at latest point ───────────────────────────
            _graph.Children.Add(new Line
            {
                X1 = xs[n-1], Y1 = GPadT - 6,
                X2 = xs[n-1], Y2 = GH - GPadB + 6,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                StrokeThickness = 1
            });

            // ── Dots ──────────────────────────────────────────────────────
            for (int i = 0; i < n; i++)
            {
                bool isBest    = Math.Abs(visible[i].LapTime - best) < 0.001;
                bool isCurrent = i == n - 1;
                DrawDot(xs[i], ys[i], col[i], isBest, isCurrent);
            }
        }

        private void NoDataLabel()
        {
            var tb = new TextBlock
            {
                Text = "No laps recorded", FontFamily = FBold, FontSize = 10,
                Foreground = new SolidColorBrush(CGray),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(tb, GW / 2 - 55);
            Canvas.SetTop(tb, GH / 2 - 8);
            _graph.Children.Add(tb);
        }

        private void DrawDot(double cx, double cy, Color baseCol, bool isBest, bool isCurrent)
        {
            double r = isBest || isCurrent ? 7 : 5.5;

            // Outer glow ring for best lap (purple)
            if (isBest)
            {
                var glow = new Ellipse
                {
                    Width = (r + 4) * 2, Height = (r + 4) * 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, CPurple.R, CPurple.G, CPurple.B)),
                    StrokeThickness = 3,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(glow, cx - r - 4); Canvas.SetTop(glow, cy - r - 4);
                _graph.Children.Add(glow);
            }

            // Main ring
            var ring = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = new SolidColorBrush(isBest ? CPurple : baseCol),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(55, baseCol.R, baseCol.G, baseCol.B))
            };
            Canvas.SetLeft(ring, cx - r); Canvas.SetTop(ring, cy - r);
            _graph.Children.Add(ring);

            // X marker for current/latest lap
            if (isCurrent)
            {
                double d = r * 0.55;
                foreach (var (ax, ay, bx, by) in new[] {
                    (cx - d, cy - d, cx + d, cy + d),
                    (cx + d, cy - d, cx - d, cy + d) })
                {
                    _graph.Children.Add(new Line
                    {
                        X1 = ax, Y1 = ay, X2 = bx, Y2 = by,
                        Stroke = new SolidColorBrush(baseCol),
                        StrokeThickness = 1.8,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap   = PenLineCap.Round
                    });
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // GRAPH HELPERS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Map a lap time to canvas Y — lower time = higher on canvas.</summary>
        private double TY(double t, double lo, double hi) =>
            GPadT + (t - lo) / (hi - lo) * DrawH;

        private static void CalcYScale(List<LapRecord> laps, out double lo, out double hi)
        {
            double min = laps.Min(l => l.LapTime);
            double max = laps.Max(l => l.LapTime);
            double range = Math.Max(max - min, 0.3);
            double pad   = range * 0.25;
            double step  = PickStep(range + pad * 2);
            lo = Math.Floor((min - pad) / step) * step;
            hi = Math.Ceiling((max + pad) / step) * step;
        }

        private static double PickStep(double range)
        {
            if (range < 0.5)  return 0.1;
            if (range < 1.5)  return 0.5;
            if (range < 4.0)  return 1.0;
            if (range < 10.0) return 2.0;
            return 5.0;
        }

        private static Color DotColor(List<LapRecord> laps, int idx, double best)
        {
            if (Math.Abs(laps[idx].LapTime - best) < 0.001) return CPurple;
            if (idx == 0) return CGreen;
            return laps[idx].LapTime < laps[idx - 1].LapTime ? CGreen : CRed;
        }

        private static UIElement GradLine(double x1, double y1, double x2, double y2,
                                          Color c1, Color c2, double thickness)
        {
            return new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                StrokeThickness    = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round,
                Stroke = new LinearGradientBrush
                {
                    StartPoint  = new Point(x1, y1),
                    EndPoint    = new Point(x2, y2),
                    MappingMode = BrushMappingMode.Absolute,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(c1, 0),
                        new GradientStop(c2, 1)
                    }
                }
            };
        }

        private static string FmtY(double t)
        {
            if (t <= 0) return "?";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        // ────────────────────────────────────────────────────────────────────
        // TABLE HELPERS
        // ────────────────────────────────────────────────────────────────────
        private static Border MakeHeaderRow()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(CHeaderBg),
                BorderBrush = new SolidColorBrush(CDivider), BorderThickness = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(PadH, 4, PadH, 4)
            };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColLap) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColTime) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColDelta) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            Hdr(row, "LAP", 0); Hdr(row, "TIME", 1); Hdr(row, "DELTA", 2); Hdr(row, "TEMP.", 3);
            border.Child = row;
            return border;
        }

        private static void Hdr(Grid g, string text, int col)
        {
            var tb = new TextBlock
            {
                Text = text, FontFamily = FBold, FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CYellow), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        private static void Cell(Grid g, string text, int col, Color c, double fs,
            FontWeight fw, FontFamily? ff = null)
        {
            var tb = new TextBlock
            {
                Text = text, FontFamily = ff ?? FBold, FontSize = fs, FontWeight = fw,
                Foreground = new SolidColorBrush(c), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        private static Canvas MakeRoadIcon(double sz, Color col)
        {
            double w = sz * 0.75;
            var canvas = new Canvas { Width = sz, Height = sz };
            var outline = new Border
            {
                Width = w, Height = sz - 4,
                BorderBrush = new SolidColorBrush(col), BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(1.5)
            };
            Canvas.SetLeft(outline, (sz - w) / 2);
            Canvas.SetTop(outline, 2);
            canvas.Children.Add(outline);
            canvas.Children.Add(new Line
            {
                X1 = sz/2, Y1 = 4, X2 = sz/2, Y2 = sz - 2,
                Stroke = new SolidColorBrush(col), StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            });
            return canvas;
        }

        private static (string text, Color color) CalcDelta(LapRecord lap, LapRecord? prev)
        {
            if (prev == null || lap.LapTime <= 0 || prev.LapTime <= 0) return ("--,----", CGray);
            double d    = lap.LapTime - prev.LapTime;
            string sign = d >= 0 ? "+" : "-";
            return d < 0
                ? ($"{sign}{Math.Abs(d):F3}", CGreen)
                : ($"{sign}{Math.Abs(d):F3}", CRed);
        }

        private static string Fmt(double t)
        {
            if (t <= 0) return "--:--.---";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
