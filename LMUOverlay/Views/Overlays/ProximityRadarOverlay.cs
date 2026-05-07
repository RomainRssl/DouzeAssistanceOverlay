using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class ProximityRadarOverlay : BaseOverlayWindow
    {
        private const double RadarRange = 30.0;
        private const double CW = 170, CH = 220;

        // Vehicle dimensions calibrated to match the game's built-in radar
        private const double CAR_W = 5;
        private const double CAR_H = 16;

        private readonly Canvas _radarCanvas;
        private readonly List<UIElement> _dynamicElements = new();

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

            // Player car (center) — slightly larger than others, same proportion as game
            var playerCar = new Rectangle
            {
                Width = CAR_W + 1, Height = CAR_H + 2,
                Fill = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                Stroke = new SolidColorBrush(Color.FromRgb(121, 192, 255)),
                StrokeThickness = 1,
                RadiusX = 1, RadiusY = 1
            };
            Canvas.SetLeft(playerCar, (CW / 2) - (CAR_W + 1) / 2);
            Canvas.SetTop(playerCar, (CH / 2) - (CAR_H + 2) / 2);
            _radarCanvas.Children.Add(playerCar);

            Grid.SetRow(_radarCanvas, 1);
            grid.Children.Add(_radarCanvas);

            border.Child = grid;
            Content = border;
        }

        public override void UpdateData()
        {
            foreach (var el in _dynamicElements)
                _radarCanvas.Children.Remove(el);
            _dynamicElements.Clear();

            bool colorByClass = Settings.CustomOptions.TryGetValue("ColorByClass",  out var cbv) && Convert.ToBoolean(cbv);
            bool showPosition = !Settings.CustomOptions.TryGetValue("ShowPosition", out var spv) || Convert.ToBoolean(spv);

            var nearby = DataService.GetNearbyVehicles(RadarRange);
            foreach (var (vehicle, relX, relZ) in nearby)
            {
                double cx = (CW / 2) + (relX / RadarRange) * (CW / 2) - CAR_W / 2;
                double cy = (CH / 2) - (relZ / RadarRange) * (CH / 2) - CAR_H / 2;
                cx = Math.Clamp(cx, 0, CW - CAR_W);
                cy = Math.Clamp(cy, 0, CH - CAR_H);

                double dist    = Math.Sqrt(relX * relX + relZ * relZ);
                double opacity = Math.Max(0.4, 1.0 - dist / RadarRange);

                Color fill;
                bool dangerBorder = false;
                if (colorByClass)
                {
                    fill = OverlayHelper.GetClassColor(vehicle.VehicleClass);
                    dangerBorder = dist < 5;
                }
                else
                {
                    fill = dist < 5  ? Color.FromRgb(255, 59,  48) :
                           dist < 15 ? Color.FromRgb(255, 204,  0) :
                                       Color.FromRgb(76,  217, 100);
                }

                var rect = new Rectangle
                {
                    Width  = CAR_W,
                    Height = CAR_H,
                    Fill   = new SolidColorBrush(fill),
                    Stroke = dangerBorder ? new SolidColorBrush(Color.FromRgb(255, 59, 48)) : null,
                    StrokeThickness = dangerBorder ? 1.5 : 0,
                    RadiusX = 1, RadiusY = 1,
                    Opacity = opacity
                };
                Canvas.SetLeft(rect, cx);
                Canvas.SetTop(rect, cy);
                _radarCanvas.Children.Add(rect);
                _dynamicElements.Add(rect);

                // Position number rendered to the right of the car rect (CAR_W too narrow for text inside)
                if (showPosition && vehicle.Position > 0)
                {
                    var label = new TextBlock
                    {
                        Text             = vehicle.Position.ToString(),
                        FontSize         = 7,
                        FontWeight       = FontWeights.Bold,
                        FontFamily       = new FontFamily("Segoe UI"),
                        Foreground       = Brushes.White,
                        IsHitTestVisible = false,
                        Opacity          = opacity
                    };
                    Canvas.SetLeft(label, cx + CAR_W + 2);
                    Canvas.SetTop(label, cy + (CAR_H - 9) / 2.0);
                    _radarCanvas.Children.Add(label);
                    _dynamicElements.Add(label);
                }
            }
        }
    }
}
