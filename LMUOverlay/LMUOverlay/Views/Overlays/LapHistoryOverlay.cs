using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// LAPTIME LOG — racing-style lap history table.
    /// Session-best bar (purple) + per-lap list: time / delta vs prev / track temp.
    /// </summary>
    public class LapHistoryOverlay : BaseOverlayWindow
    {
        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly Color CBackground = Color.FromRgb(0x16, 0x16, 0x16);
        private static readonly Color CHeaderBg   = Color.FromRgb(0x22, 0x22, 0x22);
        private static readonly Color CBestBg     = Color.FromRgb(0x6B, 0x21, 0xA8);
        private static readonly Color CYellow     = Color.FromRgb(0xFF, 0xC1, 0x07);
        private static readonly Color CWhite      = Color.FromRgb(0xFF, 0xFF, 0xFF);
        private static readonly Color CGreen      = Color.FromRgb(0x44, 0xDD, 0x44);
        private static readonly Color CRed        = Color.FromRgb(0xFF, 0x44, 0x44);
        private static readonly Color CGray       = Color.FromRgb(0x55, 0x55, 0x55);
        private static readonly Color CDivider    = Color.FromRgb(0x2C, 0x2C, 0x2C);
        private static readonly Color CSubtle     = Color.FromRgb(0xAA, 0xAA, 0xAA);

        private static readonly FontFamily FBold = new("Segoe UI");
        private static readonly FontFamily FMono = new("Consolas");

        // ── Layout ───────────────────────────────────────────────────────────
        private const double TotalW  = 320;
        private const double PadH    = 10;
        private const double ColLap  = 36;
        private const double ColTime = 96;
        private const double ColDelta= 78;

        // ── Controls ─────────────────────────────────────────────────────────
        private readonly TextBlock  _bestTimeText;
        private readonly TextBlock  _bestTempText;
        private readonly StackPanel _rows;
        private int _lastCount = -1;

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

            // Title bar
            sp.Children.Add(new Border
            {
                Background = new SolidColorBrush(CHeaderBg),
                Padding    = new Thickness(0, 7, 0, 7),
                Child      = new TextBlock
                {
                    Text = "LAPTIME LOG", FontFamily = FBold, FontSize = 11,
                    FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(CWhite),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            });

            // Session best bar
            var bestBar = new Grid { Background = new SolidColorBrush(CBestBg), Margin = new Thickness(0, 1, 0, 1) };
            bestBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            bestBar.ColumnDefinitions.Add(new ColumnDefinition());
            bestBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(new TextBlock
            {
                Text = "SESSION\nBEST", FontFamily = FBold, FontSize = 8.5,
                FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(CWhite),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(PadH, 9, 0, 9), LineHeight = 13
            }.Also(tb => bestBar.Children.Add(tb)), 0);

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
            sp.Children.Add(bestBar);

            // Column headers
            sp.Children.Add(MakeHeaderRow());

            // Rows
            _rows = new StackPanel();
            sp.Children.Add(new ScrollViewer
            {
                Content = _rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                MaxHeight = 270
            });

            root.Child = sp;
            Content = root;
        }

        public override void UpdateData()
        {
            var laps = DataService.GetLapHistory();
            if (laps.Count == _lastCount) return;
            _lastCount = laps.Count;
            Rebuild(laps);
        }

        private void Rebuild(List<LapRecord> laps)
        {
            double best = laps.Where(l => l.LapTime > 0).Select(l => l.LapTime).DefaultIfEmpty(0).Min();
            _bestTimeText.Text = best > 0 ? Fmt(best) : "--:--.---";
            var latest = laps.LastOrDefault();
            _bestTempText.Text = latest?.TrackTemp > 0 ? $"{latest.TrackTemp:F0}°C" : "--°C";

            _rows.Children.Clear();
            var rev = laps.AsEnumerable().Reverse().Take(25).ToList();
            for (int i = 0; i < rev.Count; i++)
                _rows.Children.Add(MakeLapRow(rev[i], i + 1 < rev.Count ? rev[i + 1] : null,
                    best > 0 && Math.Abs(rev[i].LapTime - best) < 0.001));
        }

        private static UIElement MakeLapRow(LapRecord lap, LapRecord? prev, bool isBest)
        {
            var container = new Border
            {
                BorderBrush = new SolidColorBrush(CDivider), BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(PadH, 5, PadH, 5)
            };
            if (isBest) container.Background = new SolidColorBrush(Color.FromArgb(18, 0x6B, 0x21, 0xA8));

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColLap) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColTime) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColDelta) });
            row.ColumnDefinitions.Add(new ColumnDefinition());

            Cell(row, $"{lap.LapNumber}", 0, isBest ? CYellow : CWhite, 12, FontWeights.Bold);
            Cell(row, Fmt(lap.LapTime),   1, isBest ? CYellow : CWhite, 12,
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

        // ── Shared helpers ────────────────────────────────────────────────────
        internal static Border MakeHeaderRow()
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

        internal static void Hdr(Grid g, string text, int col)
        {
            var tb = new TextBlock
            {
                Text = text, FontFamily = FBold, FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CYellow), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col); g.Children.Add(tb);
        }

        internal static void Cell(Grid g, string text, int col, Color c, double fs,
            FontWeight fw, FontFamily? ff = null)
        {
            var tb = new TextBlock
            {
                Text = text, FontFamily = ff ?? FBold, FontSize = fs, FontWeight = fw,
                Foreground = new SolidColorBrush(c), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col); g.Children.Add(tb);
        }

        internal static Canvas MakeRoadIcon(double sz, Color col)
        {
            double w = sz * 0.75;
            var canvas = new Canvas { Width = sz, Height = sz };
            var outline = new Border
            {
                Width = w, Height = sz - 4,
                BorderBrush = new SolidColorBrush(col), BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(1.5)
            };
            Canvas.SetLeft(outline, (sz - w) / 2); Canvas.SetTop(outline, 2);
            canvas.Children.Add(outline);
            canvas.Children.Add(new Line
            {
                X1 = sz/2, Y1 = 4, X2 = sz/2, Y2 = sz - 2,
                Stroke = new SolidColorBrush(col), StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            });
            return canvas;
        }

        internal static (string text, Color color) CalcDelta(LapRecord lap, LapRecord? prev)
        {
            if (prev == null || lap.LapTime <= 0 || prev.LapTime <= 0) return ("--,----", CGray);
            double d = lap.LapTime - prev.LapTime;
            string sign = d >= 0 ? "+" : "-";
            return d < 0 ? ($"{sign}{Math.Abs(d):F3}", CGreen) : ($"{sign}{Math.Abs(d):F3}", CRed);
        }

        internal static string Fmt(double t)
        {
            if (t <= 0) return "--:--.---";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }

    // Extension helper used inline
    internal static class WpfExt
    {
        internal static T Also<T>(this T obj, Action<T> action) { action(obj); return obj; }
    }
}
