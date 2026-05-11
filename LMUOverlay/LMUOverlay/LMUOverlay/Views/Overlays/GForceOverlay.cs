using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class GForceOverlay : BaseOverlayWindow
    {
        private readonly Canvas _gCanvas;
        private readonly Ellipse _gDot;
        private readonly TextBlock _latText, _lonText, _combinedText;
        private readonly List<Ellipse> _trailDots = new();
        private readonly Queue<(double x, double y)> _trail = new();
        private const double CanvasSize = 130, MaxG = 4.0;

        public GForceOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {

            var border = OverlayHelper.MakeBorder();
            var sp = new StackPanel();
            sp.Children.Add(OverlayHelper.MakeTitle("FORCE G"));

            _gCanvas = new Canvas
            {
                Width = CanvasSize, Height = CanvasSize,
                HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = true
            };

            // Background circles (1G, 2G, 3G)
            for (int g = 1; g <= 3; g++)
            {
                double r = (g / MaxG) * (CanvasSize / 2);
                var circle = new Ellipse
                {
                    Width = r * 2, Height = r * 2,
                    Stroke = new SolidColorBrush(Color.FromArgb((byte)(20 + g * 10), 255, 255, 255)),
                    StrokeThickness = 0.5
                };
                Canvas.SetLeft(circle, CanvasSize / 2 - r);
                Canvas.SetTop(circle, CanvasSize / 2 - r);
                _gCanvas.Children.Add(circle);
            }

            // Crosshair
            _gCanvas.Children.Add(new Line { X1 = CanvasSize / 2, Y1 = 0, X2 = CanvasSize / 2, Y2 = CanvasSize, Stroke = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), StrokeThickness = 0.5 });
            _gCanvas.Children.Add(new Line { X1 = 0, Y1 = CanvasSize / 2, X2 = CanvasSize, Y2 = CanvasSize / 2, Stroke = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), StrokeThickness = 0.5 });

            // G dot
            _gDot = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = BrushCache.Get(ThemeManager.Current.StateDanger)
            };
            _gCanvas.Children.Add(_gDot);

            sp.Children.Add(_gCanvas);

            // Values
            var valGrid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            valGrid.ColumnDefinitions.Add(new ColumnDefinition());
            valGrid.ColumnDefinitions.Add(new ColumnDefinition());
            valGrid.ColumnDefinitions.Add(new ColumnDefinition());

            _latText = MakeVal(valGrid, "LAT", 0);
            _lonText = MakeVal(valGrid, "LON", 1);
            _combinedText = MakeVal(valGrid, "TOTAL", 2);

            sp.Children.Add(valGrid);
            border.Child = sp;
            Content = border;
        }

        private static TextBlock MakeVal(Grid g, string label, int col)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 7, Foreground = BrushCache.Get(ThemeManager.Current.TextMuted), HorizontalAlignment = HorizontalAlignment.Center });
            var val = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, Foreground = BrushCache.Get(ThemeManager.Current.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(val);
            Grid.SetColumn(sp, col); g.Children.Add(sp);
            return val;
        }

        public override void UpdateData()
        {
            var gf = DataService.GetGForceData();

            double half = CanvasSize / 2;
            double px = half + (gf.Lateral / MaxG) * half;
            double py = half - (gf.Longitudinal / MaxG) * half;
            px = Math.Clamp(px, 5, CanvasSize - 5);
            py = Math.Clamp(py, 5, CanvasSize - 5);

            Canvas.SetLeft(_gDot, px - 5);
            Canvas.SetTop(_gDot, py - 5);

            // Trail
            _trail.Enqueue((px, py));
            if (_trail.Count > 30) _trail.Dequeue();

            // Clear old trail dots
            foreach (var d in _trailDots) _gCanvas.Children.Remove(d);
            _trailDots.Clear();

            int idx = 0;
            foreach (var (tx, ty) in _trail)
            {
                double op = (double)idx / _trail.Count * 0.4;
                var tm = ThemeManager.Current;
                var dot = new Ellipse
                {
                    Width = 3, Height = 3,
                    Fill = BrushCache.Get(Color.FromArgb((byte)(op * 255), tm.StateDanger.R, 100, 100))
                };
                Canvas.SetLeft(dot, tx - 1.5);
                Canvas.SetTop(dot, ty - 1.5);
                _gCanvas.Children.Add(dot);
                _trailDots.Add(dot);
                idx++;
            }

            // Ensure main dot is on top
            _gCanvas.Children.Remove(_gDot);
            _gCanvas.Children.Add(_gDot);

            // Color based on intensity
            var tmr = ThemeManager.Current;
            Color dotCol = gf.Combined > 3 ? tmr.StateDanger :
                           gf.Combined > 2 ? tmr.StateWarn :
                           tmr.ClassLmp2;
            _gDot.Fill = BrushCache.Get(dotCol);

            _latText.Text = $"{gf.Lateral:F2}G";
            _lonText.Text = $"{gf.Longitudinal:F2}G";
            _combinedText.Text = $"{gf.Combined:F2}G";
        }
    }
}
