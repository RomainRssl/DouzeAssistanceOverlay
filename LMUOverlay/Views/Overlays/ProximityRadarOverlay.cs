using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Proximity radar overlay - shows nearby vehicles around the player.
    /// Pure code class (no XAML) like all other overlays.
    /// </summary>
    public class ProximityRadarOverlay : BaseOverlayWindow
    {
        private const double RadarRange = 30.0;
        private const double CW = 170, CH = 220;
        private readonly Canvas _radarCanvas;
        private readonly List<Shape> _dynamicShapes = new();

        public ProximityRadarOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {


            var border = new Border { Padding = new Thickness(3) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var title = OverlayHelper.MakeTitle("RADAR");
            Grid.SetRow(title, 0);
            grid.Children.Add(title);

            _radarCanvas = new Canvas { Width = CW, Height = CH, ClipToBounds = true };

            // Range rings
            _radarCanvas.Children.Add(new Ellipse
            {
                Width = 164, Height = 214,
                Stroke = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            });
            Canvas.SetLeft(_radarCanvas.Children[0], 3);
            Canvas.SetTop(_radarCanvas.Children[0], 3);

            _radarCanvas.Children.Add(new Ellipse
            {
                Width = 100, Height = 130,
                Stroke = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255)),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            });
            Canvas.SetLeft(_radarCanvas.Children[1], 35);
            Canvas.SetTop(_radarCanvas.Children[1], 45);

            // Crosshair
            _radarCanvas.Children.Add(new Line
            {
                X1 = 85, Y1 = 0, X2 = 85, Y2 = CH,
                Stroke = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
                StrokeThickness = 0.5
            });
            _radarCanvas.Children.Add(new Line
            {
                X1 = 0, Y1 = 110, X2 = CW, Y2 = 110,
                Stroke = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
                StrokeThickness = 0.5
            });

            // Player car (center)
            var playerCar = new Rectangle
            {
                Width = 14, Height = 26,
                Fill = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                Stroke = new SolidColorBrush(Color.FromRgb(121, 192, 255)),
                StrokeThickness = 1,
                RadiusX = 3, RadiusY = 3
            };
            Canvas.SetLeft(playerCar, 78);
            Canvas.SetTop(playerCar, 97);
            _radarCanvas.Children.Add(playerCar);

            Grid.SetRow(_radarCanvas, 1);
            grid.Children.Add(_radarCanvas);

            border.Child = grid;
            Content = border;
        }

        public override void UpdateData()
        {
            foreach (var shape in _dynamicShapes)
                _radarCanvas.Children.Remove(shape);
            _dynamicShapes.Clear();

            var nearby = DataService.GetNearbyVehicles(RadarRange);
            foreach (var (vehicle, relX, relZ) in nearby)
            {
                double cx = (CW / 2) + (relX / RadarRange) * (CW / 2) - 6;
                double cy = (CH / 2) - (relZ / RadarRange) * (CH / 2) - 10;
                cx = Math.Clamp(cx, 0, CW - 12);
                cy = Math.Clamp(cy, 0, CH - 20);

                double dist = Math.Sqrt(relX * relX + relZ * relZ);
                Color c = dist < 5 ? Color.FromRgb(255, 59, 48) :
                          dist < 15 ? Color.FromRgb(255, 204, 0) :
                          Color.FromRgb(76, 217, 100);

                var rect = new Rectangle
                {
                    Width = 12, Height = 20,
                    Fill = new SolidColorBrush(c),
                    RadiusX = 2, RadiusY = 2,
                    Opacity = Math.Max(0.4, 1.0 - dist / RadarRange)
                };
                Canvas.SetLeft(rect, cx);
                Canvas.SetTop(rect, cy);
                _radarCanvas.Children.Add(rect);
                _dynamicShapes.Add(rect);
            }
        }
    }
}
