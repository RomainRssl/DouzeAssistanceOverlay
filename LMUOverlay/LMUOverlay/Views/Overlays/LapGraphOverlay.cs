using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// LAPTIME GRAPH — lap-time curve over the last 9 laps.
    /// Dots: green = improved vs prev, red = slower, purple = session best, X = latest lap.
    /// Lines use a gradient between consecutive dot colors.
    /// Y-axis auto-scales with time labels on the right.
    /// </summary>
    public class LapGraphOverlay : BaseOverlayWindow
    {
        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly Color CBackground = Color.FromRgb(0x16, 0x16, 0x16);
        private static readonly Color CHeaderBg   = Color.FromRgb(0x22, 0x22, 0x22);
        private static readonly Color CPurple     = Color.FromRgb(0xC0, 0x5A, 0xFF);
        private static readonly Color CWhite      = Color.FromRgb(0xFF, 0xFF, 0xFF);
        private static readonly Color CGreen      = Color.FromRgb(0x44, 0xDD, 0x44);
        private static readonly Color CRed        = Color.FromRgb(0xFF, 0x44, 0x44);
        private static readonly Color CGray       = Color.FromRgb(0x55, 0x55, 0x55);
        private static readonly Color CSubtle     = Color.FromRgb(0xAA, 0xAA, 0xAA);
        private static readonly Color CGrid       = Color.FromArgb(45, 0xFF, 0xFF, 0xFF);

        private static readonly FontFamily FBold = new("Segoe UI");
        private static readonly FontFamily FMono = new("Consolas");

        // ── Graph dimensions ─────────────────────────────────────────────────
        private const double GW    = 320;
        private const double GH    = 210;
        private const double GPadL = 14;
        private const double GPadR = 72;
        private const double GPadT = 18;
        private const double GPadB = 14;
        private const int    GL    = 9;    // max laps displayed

        private double DrawW => GW - GPadL - GPadR;
        private double DrawH => GH - GPadT - GPadB;

        // ── Controls ─────────────────────────────────────────────────────────
        private readonly Canvas _graph;
        private int _lastCount = -1;

        public LapGraphOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var root = new Border
            {
                Background   = new SolidColorBrush(CBackground),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Width        = GW
            };
            var sp = new StackPanel();

            // Title bar
            sp.Children.Add(new Border
            {
                Background = new SolidColorBrush(CHeaderBg),
                Padding    = new Thickness(0, 7, 0, 7),
                Child      = new TextBlock
                {
                    Text = "LAPTIME GRAPH", FontFamily = FBold, FontSize = 11,
                    FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(CWhite),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            });

            // Graph canvas
            _graph = new Canvas
            {
                Width = GW, Height = GH, ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(8, 255, 255, 255)),
                Margin = new Thickness(0, 1, 0, 0)
            };
            sp.Children.Add(_graph);

            root.Child = sp;
            Content = root;
        }

        public override void UpdateData()
        {
            var laps = DataService.GetLapHistory();
            if (laps.Count == _lastCount) return;
            _lastCount = laps.Count;
            DrawGraph(laps);
        }

        // ── Graph drawing ────────────────────────────────────────────────────
        private void DrawGraph(List<LapRecord> laps)
        {
            _graph.Children.Clear();

            var visible = laps.Where(l => l.LapTime > 0).TakeLast(GL).ToList();
            if (visible.Count < 1) { NoData(); return; }

            double best = visible.Min(l => l.LapTime);
            CalcYScale(visible, out double lo, out double hi);
            double step = PickStep(hi - lo);

            // Grid lines + Y labels
            for (double t = lo; t <= hi + 0.001; t += step)
            {
                double y = TY(t, lo, hi);
                _graph.Children.Add(new Line
                {
                    X1 = GPadL, Y1 = y, X2 = GW - GPadR, Y2 = y,
                    Stroke = new SolidColorBrush(CGrid), StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 5 }
                });
                var lbl = new TextBlock
                {
                    Text = FmtY(t), FontFamily = FMono, FontSize = 9,
                    Foreground = new SolidColorBrush(CSubtle)
                };
                Canvas.SetLeft(lbl, GW - GPadR + 6); Canvas.SetTop(lbl, y - 7);
                _graph.Children.Add(lbl);
            }

            // Point positions + colors
            int n = visible.Count;
            var xs  = new double[n];
            var ys  = new double[n];
            var col = new Color[n];
            for (int i = 0; i < n; i++)
            {
                xs[i]  = n == 1 ? GPadL + DrawW / 2 : GPadL + i / (double)(n - 1) * DrawW;
                ys[i]  = TY(visible[i].LapTime, lo, hi);
                col[i] = DotColor(visible, i, best);
            }

            // Gradient lines
            for (int i = 1; i < n; i++)
                _graph.Children.Add(GradLine(xs[i-1], ys[i-1], xs[i], ys[i], col[i-1], col[i], 2.2));

            // Vertical marker at latest point
            _graph.Children.Add(new Line
            {
                X1 = xs[n-1], Y1 = GPadT - 6, X2 = xs[n-1], Y2 = GH - GPadB + 6,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                StrokeThickness = 1
            });

            // Dots
            for (int i = 0; i < n; i++)
                DrawDot(xs[i], ys[i], col[i],
                    isBest:    Math.Abs(visible[i].LapTime - best) < 0.001,
                    isCurrent: i == n - 1);
        }

        private void DrawDot(double cx, double cy, Color baseCol, bool isBest, bool isCurrent)
        {
            double r = isBest || isCurrent ? 7 : 5.5;

            if (isBest)
            {
                var glow = new Ellipse
                {
                    Width = (r + 4) * 2, Height = (r + 4) * 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, CPurple.R, CPurple.G, CPurple.B)),
                    StrokeThickness = 3, Fill = Brushes.Transparent
                };
                Canvas.SetLeft(glow, cx - r - 4); Canvas.SetTop(glow, cy - r - 4);
                _graph.Children.Add(glow);
            }

            var ring = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = new SolidColorBrush(isBest ? CPurple : baseCol),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(55, baseCol.R, baseCol.G, baseCol.B))
            };
            Canvas.SetLeft(ring, cx - r); Canvas.SetTop(ring, cy - r);
            _graph.Children.Add(ring);

            if (isCurrent)
            {
                double d = r * 0.55;
                foreach (var (ax, ay, bx, by) in new[] {
                    (cx-d, cy-d, cx+d, cy+d), (cx+d, cy-d, cx-d, cy+d) })
                {
                    _graph.Children.Add(new Line
                    {
                        X1 = ax, Y1 = ay, X2 = bx, Y2 = by,
                        Stroke = new SolidColorBrush(baseCol), StrokeThickness = 1.8,
                        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                    });
                }
            }
        }

        private void NoData()
        {
            var tb = new TextBlock
            {
                Text = "No laps recorded", FontFamily = FBold, FontSize = 10,
                Foreground = new SolidColorBrush(CGray)
            };
            Canvas.SetLeft(tb, GW / 2 - 55); Canvas.SetTop(tb, GH / 2 - 8);
            _graph.Children.Add(tb);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
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
                                          Color c1, Color c2, double thickness) =>
            new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Stroke = new LinearGradientBrush
                {
                    StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2),
                    MappingMode = BrushMappingMode.Absolute,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(c1, 0),
                        new GradientStop(c2, 1)
                    }
                }
            };

        private static string FmtY(double t)
        {
            if (t <= 0) return "?";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
