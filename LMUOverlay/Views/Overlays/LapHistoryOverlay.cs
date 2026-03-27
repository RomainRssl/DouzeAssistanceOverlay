using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// LAPTIME LOG overlay — racing-style lap history table.
    /// Shows: session best bar (purple), per-lap list with time / delta vs prev / track temp.
    /// </summary>
    public class LapHistoryOverlay : BaseOverlayWindow
    {
        // ────────────────────────────────────────────────────────────────────
        // PALETTE
        // ────────────────────────────────────────────────────────────────────
        private static readonly Color CBackground = Color.FromRgb(0x16, 0x16, 0x16);
        private static readonly Color CHeaderBg   = Color.FromRgb(0x22, 0x22, 0x22);
        private static readonly Color CBestBg     = Color.FromRgb(0x6B, 0x21, 0xA8); // purple
        private static readonly Color CYellow     = Color.FromRgb(0xFF, 0xC1, 0x07);
        private static readonly Color CWhite      = Color.FromRgb(0xFF, 0xFF, 0xFF);
        private static readonly Color CGreen      = Color.FromRgb(0x44, 0xDD, 0x44);
        private static readonly Color CRed        = Color.FromRgb(0xFF, 0x44, 0x44);
        private static readonly Color CGray       = Color.FromRgb(0x55, 0x55, 0x55);
        private static readonly Color CDivider    = Color.FromRgb(0x2C, 0x2C, 0x2C);
        private static readonly Color CSubtle     = Color.FromRgb(0xAA, 0xAA, 0xAA);

        private static readonly FontFamily FBold   = new("Segoe UI");
        private static readonly FontFamily FMono   = new("Consolas");

        // ────────────────────────────────────────────────────────────────────
        // LAYOUT CONSTANTS
        // ────────────────────────────────────────────────────────────────────
        private const double TotalW  = 320;
        private const double PadH    = 10;   // horizontal inner padding
        private const double ColLap  = 36;
        private const double ColTime = 96;
        private const double ColDelta= 78;
        // ColTemp = remainder

        // ────────────────────────────────────────────────────────────────────
        // CONTROLS
        // ────────────────────────────────────────────────────────────────────
        private readonly TextBlock _bestTimeText;
        private readonly TextBlock _bestTempText;
        private readonly StackPanel _rows;
        private int _lastCount = -1;

        // ────────────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────────────────────────────────────────
        public LapHistoryOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var root = new Border
            {
                Background    = new SolidColorBrush(CBackground),
                CornerRadius  = new CornerRadius(4),
                ClipToBounds  = true,
                Width         = TotalW
            };
            var sp = new StackPanel();

            // ── Title bar ──────────────────────────────────────────────────
            sp.Children.Add(MakeTitleBar());

            // ── Session best bar ───────────────────────────────────────────
            var bestBar = new Grid
            {
                Background = new SolidColorBrush(CBestBg),
                Margin     = new Thickness(0, 1, 0, 1)
            };
            bestBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            bestBar.ColumnDefinitions.Add(new ColumnDefinition());
            bestBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: "SESSION BEST" label
            var sessionLabel = new TextBlock
            {
                Text                = "SESSION\nBEST",
                FontFamily          = FBold,
                FontSize            = 8.5,
                FontWeight          = FontWeights.Bold,
                Foreground          = new SolidColorBrush(CWhite),
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(PadH, 9, 0, 9),
                LineHeight          = 13
            };
            Grid.SetColumn(sessionLabel, 0);
            bestBar.Children.Add(sessionLabel);

            // Center: best lap time
            _bestTimeText = new TextBlock
            {
                Text                    = "--:--.---",
                FontFamily              = FBold,
                FontSize                = 22,
                FontWeight              = FontWeights.Bold,
                Foreground              = new SolidColorBrush(CWhite),
                VerticalAlignment       = VerticalAlignment.Center,
                HorizontalAlignment     = HorizontalAlignment.Center
            };
            Grid.SetColumn(_bestTimeText, 1);
            bestBar.Children.Add(_bestTimeText);

            // Right: road icon + track temp
            var tempPanel = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(0, 0, PadH, 0)
            };
            tempPanel.Children.Add(MakeRoadIcon(16, CWhite));
            _bestTempText = new TextBlock
            {
                Text              = "--°C",
                FontFamily        = FBold,
                FontSize          = 11,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = new SolidColorBrush(CWhite),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(5, 0, 0, 0)
            };
            tempPanel.Children.Add(_bestTempText);
            Grid.SetColumn(tempPanel, 2);
            bestBar.Children.Add(tempPanel);

            sp.Children.Add(bestBar);

            // ── Column headers ─────────────────────────────────────────────
            sp.Children.Add(MakeHeaderRow());

            // ── Lap rows (scrollable) ──────────────────────────────────────
            _rows = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = _rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                MaxHeight = 270
            };
            sp.Children.Add(scroll);

            root.Child = sp;
            Content = root;
        }

        // ────────────────────────────────────────────────────────────────────
        // UPDATE
        // ────────────────────────────────────────────────────────────────────
        public override void UpdateData()
        {
            var laps = DataService.GetLapHistory();
            if (laps.Count == _lastCount) return;
            _lastCount = laps.Count;
            Rebuild(laps);
        }

        private void Rebuild(List<LapRecord> laps)
        {
            // Session best
            double best = laps.Where(l => l.LapTime > 0).Select(l => l.LapTime).DefaultIfEmpty(0).Min();
            _bestTimeText.Text = best > 0 ? Fmt(best) : "--:--.---";

            // Track temp from most recent lap
            var latest = laps.LastOrDefault();
            _bestTempText.Text = latest != null && latest.TrackTemp > 0
                ? $"{latest.TrackTemp:F0}°C"
                : "--°C";

            // Rebuild rows — most recent at top
            _rows.Children.Clear();
            var reversed = laps.AsEnumerable().Reverse().Take(25).ToList();
            for (int i = 0; i < reversed.Count; i++)
            {
                var lap  = reversed[i];
                var prev = i + 1 < reversed.Count ? reversed[i + 1] : null; // prev in time = next in reversed list
                bool isBest = best > 0 && Math.Abs(lap.LapTime - best) < 0.001;
                _rows.Children.Add(MakeLapRow(lap, prev, isBest, i));
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // ROW BUILDER
        // ────────────────────────────────────────────────────────────────────
        private UIElement MakeLapRow(LapRecord lap, LapRecord? prev, bool isBest, int index)
        {
            var container = new Border
            {
                BorderBrush     = new SolidColorBrush(CDivider),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(PadH, 5, PadH, 5)
            };

            // Subtle highlight for best lap
            if (isBest)
                container.Background = new SolidColorBrush(Color.FromArgb(18, 0x6B, 0x21, 0xA8));

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColLap) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColTime) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColDelta) });
            row.ColumnDefinitions.Add(new ColumnDefinition());

            // LAP number
            var lapTb = new TextBlock
            {
                Text              = $"{lap.LapNumber}",
                FontFamily        = FBold,
                FontSize          = 12,
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(isBest ? CYellow : CWhite),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lapTb, 0);
            row.Children.Add(lapTb);

            // LAP TIME
            var timeTb = new TextBlock
            {
                Text              = Fmt(lap.LapTime),
                FontFamily        = FMono,
                FontSize          = 12,
                FontWeight        = isBest ? FontWeights.Bold : FontWeights.Normal,
                Foreground        = new SolidColorBrush(isBest ? CYellow : CWhite),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeTb, 1);
            row.Children.Add(timeTb);

            // DELTA vs previous lap
            var (deltaStr, deltaColor) = CalcDelta(lap, prev);
            var deltaTb = new TextBlock
            {
                Text              = deltaStr,
                FontFamily        = FMono,
                FontSize          = 11,
                Foreground        = new SolidColorBrush(deltaColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(deltaTb, 2);
            row.Children.Add(deltaTb);

            // TEMP
            var tempPanel = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            tempPanel.Children.Add(MakeRoadIcon(13, CSubtle));
            tempPanel.Children.Add(new TextBlock
            {
                Text       = lap.TrackTemp > 0 ? $" {lap.TrackTemp:F1}°c" : " --°c",
                FontFamily = FBold,
                FontSize   = 10,
                Foreground = new SolidColorBrush(CSubtle),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(tempPanel, 3);
            row.Children.Add(tempPanel);

            container.Child = row;
            return container;
        }

        // ────────────────────────────────────────────────────────────────────
        // HELPERS — BUILDERS
        // ────────────────────────────────────────────────────────────────────
        private static Border MakeTitleBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(CHeaderBg),
                Padding    = new Thickness(0, 7, 0, 7)
            };
            bar.Child = new TextBlock
            {
                Text                    = "LAPTIME LOG",
                FontFamily              = FBold,
                FontSize                = 11,
                FontWeight              = FontWeights.Bold,
                Foreground              = new SolidColorBrush(CWhite),
                HorizontalAlignment     = HorizontalAlignment.Center,
                VerticalAlignment       = VerticalAlignment.Center
            };
            return bar;
        }

        private static Border MakeHeaderRow()
        {
            var border = new Border
            {
                Background      = new SolidColorBrush(CHeaderBg),
                BorderBrush     = new SolidColorBrush(CDivider),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding         = new Thickness(PadH, 4, PadH, 4)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColLap) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColTime) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColDelta) });
            row.ColumnDefinitions.Add(new ColumnDefinition());

            Hdr(row, "LAP",   0, HorizontalAlignment.Left);
            Hdr(row, "TIME",  1, HorizontalAlignment.Left);
            Hdr(row, "DELTA", 2, HorizontalAlignment.Left);
            Hdr(row, "TEMP.", 3, HorizontalAlignment.Left);

            border.Child = row;
            return border;
        }

        private static void Hdr(Grid g, string text, int col, HorizontalAlignment ha)
        {
            var tb = new TextBlock
            {
                Text                = text,
                FontFamily          = FBold,
                FontSize            = 9,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(CYellow),
                HorizontalAlignment = ha,
                VerticalAlignment   = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        /// <summary>Road icon: white rounded rectangle with dashed center divider.</summary>
        private static Canvas MakeRoadIcon(double sz, Color col)
        {
            double w = sz * 0.75, h = sz;
            var canvas = new Canvas { Width = sz, Height = sz };
            var outline = new Border
            {
                Width           = w,
                Height          = h - 4,
                BorderBrush     = new SolidColorBrush(col),
                BorderThickness = new Thickness(1.2),
                CornerRadius    = new CornerRadius(1.5)
            };
            Canvas.SetLeft(outline, (sz - w) / 2);
            Canvas.SetTop(outline, 2);
            canvas.Children.Add(outline);

            var centerLine = new Line
            {
                X1 = sz / 2, Y1 = 4,
                X2 = sz / 2, Y2 = sz - 2,
                Stroke          = new SolidColorBrush(col),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };
            canvas.Children.Add(centerLine);
            return canvas;
        }

        // ────────────────────────────────────────────────────────────────────
        // HELPERS — LOGIC
        // ────────────────────────────────────────────────────────────────────
        private static (string text, Color color) CalcDelta(LapRecord lap, LapRecord? prev)
        {
            if (prev == null || lap.LapTime <= 0 || prev.LapTime <= 0)
                return ("--,----", CGray);

            double d = lap.LapTime - prev.LapTime;
            string sign  = d >= 0 ? "+" : "-";
            string abs   = $"{Math.Abs(d):F3}";
            return d < 0
                ? ($"{sign}{abs}", CGreen)
                : ($"{sign}{abs}", CRed);
        }

        private static string Fmt(double t)
        {
            if (t <= 0) return "--:--.---";
            var ts = TimeSpan.FromSeconds(t);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
