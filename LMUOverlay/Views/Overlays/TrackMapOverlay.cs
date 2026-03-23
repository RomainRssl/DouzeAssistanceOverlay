using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Track map overlay with circuit image background.
    /// Vehicle positions are auto-calibrated onto the image using min/max coordinates.
    /// Falls back to drawing the track from vehicle positions if no image found.
    /// 
    /// Track images go in Resources/Tracks/ with filename matching the track name.
    /// Example: "Le Mans 24h.png", "Spa-Francorchamps.png"
    /// Supported: .png, .jpg
    /// </summary>
    public class TrackMapOverlay : BaseOverlayWindow
    {
        private readonly Canvas _mapCanvas;
        private readonly Image _trackImage;
        private const double MapW = 280, MapH = 280;
        private const double Pad = 12;

        // Bounds from vehicle positions (auto-calibrate)
        private double _minX = double.MaxValue, _maxX = double.MinValue;
        private double _minZ = double.MaxValue, _maxZ = double.MinValue;
        private bool _boundsStable;
        private int _boundsStableFrames;

        // Fallback: drawn track when no image
        private readonly SortedDictionary<int, (double x, double z)> _trackPoints = new();
        private Polyline? _drawnTrack;
        private bool _hasImage;

        // Current track
        private string _loadedTrack = "";

        // Vehicle visuals (recreated each frame)
        private readonly List<UIElement> _dots = new();

        public TrackMapOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var border = new Border { Padding = new Thickness(2) };

            var grid = new Grid { Width = MapW, Height = MapH };

            // Track image (behind everything)
            _trackImage = new Image
            {
                Stretch = Stretch.Uniform,
                Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(_trackImage);

            // Canvas for dots (on top of image)
            _mapCanvas = new Canvas
            {
                Width = MapW, Height = MapH,
                ClipToBounds = true,
                Background = Brushes.Transparent
            };
            grid.Children.Add(_mapCanvas);

            border.Child = grid;
            Content = border;
        }

        public override void UpdateData()
        {
            var vehicles = DataService.GetAllVehicles();
            if (vehicles.Count == 0) return;

            // ================================================================
            // 1. LOAD TRACK IMAGE (once per track)
            // ================================================================
            string trackName = DataService.GetTrackName();
            if (!string.IsNullOrEmpty(trackName) && trackName != _loadedTrack)
            {
                _loadedTrack = trackName;
                LoadTrackImage(trackName);
                // Reset bounds for new track
                _minX = double.MaxValue; _maxX = double.MinValue;
                _minZ = double.MaxValue; _maxZ = double.MinValue;
                _boundsStable = false;
                _boundsStableFrames = 0;
                _trackPoints.Clear();
            }

            // ================================================================
            // 2. UPDATE BOUNDS from all vehicles
            // ================================================================
            bool boundsChanged = false;
            foreach (var v in vehicles)
            {
                if (v.InPits && _boundsStableFrames < 30) continue; // skip pit positions early on
                if (v.PosX < _minX) { _minX = v.PosX; boundsChanged = true; }
                if (v.PosX > _maxX) { _maxX = v.PosX; boundsChanged = true; }
                if (v.PosZ < _minZ) { _minZ = v.PosZ; boundsChanged = true; }
                if (v.PosZ > _maxZ) { _maxZ = v.PosZ; boundsChanged = true; }
            }

            if (!boundsChanged)
                _boundsStableFrames++;
            else
                _boundsStableFrames = 0;

            _boundsStable = _boundsStableFrames > 60; // ~2 seconds of stability

            // ================================================================
            // 3. FALLBACK: Draw track from positions if no image
            // ================================================================
            if (!_hasImage)
            {
                foreach (var v in vehicles)
                {
                    if (v.InPits || v.LapDistance <= 0) continue;
                    int bucket = (int)(v.LapDistance / 5.0);
                    if (!_trackPoints.ContainsKey(bucket))
                        _trackPoints[bucket] = (v.PosX, v.PosZ);
                }
                DrawFallbackTrack();
            }

            // ================================================================
            // 4. DRAW VEHICLES
            // ================================================================
            foreach (var dot in _dots)
                _mapCanvas.Children.Remove(dot);
            _dots.Clear();

            // Sort: player on top
            var sorted = vehicles.OrderBy(v => v.IsPlayer ? 1 : 0).ToList();

            foreach (var v in sorted)
            {
                var (cx, cy) = WorldToCanvas(v.PosX, v.PosZ);
                Color classColor = OverlayHelper.GetClassColor(v.VehicleClass);

                if (v.IsPlayer)
                    DrawPlayer(cx, cy, classColor, v.Position);
                else
                    DrawVehicle(cx, cy, classColor, v.InPits);
            }
        }

        // ================================================================
        // TRACK IMAGE LOADING
        // ================================================================

        private void LoadTrackImage(string trackName)
        {
            _hasImage = false;
            _trackImage.Source = null;

            // Search for matching image in multiple locations
            string[] searchPaths = GetImageSearchPaths(trackName);

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(path, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        _trackImage.Source = bmp;
                        _hasImage = true;
                        return;
                    }
                    catch { /* skip bad image */ }
                }
            }

            // Try embedded resource
            try
            {
                string resName = SanitizeFileName(trackName);
                var uri = new Uri($"pack://application:,,,/Resources/Tracks/{resName}.png", UriKind.Absolute);
                var bmp = new BitmapImage(uri);
                _trackImage.Source = bmp;
                _hasImage = true;
            }
            catch { /* no embedded resource */ }
        }

        private static string[] GetImageSearchPaths(string trackName)
        {
            string sanitized = SanitizeFileName(trackName);
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string tracksDir = System.IO.Path.Combine(appDir, "Tracks");

            var paths = new List<string>();

            // Exact match
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                paths.Add(System.IO.Path.Combine(tracksDir, sanitized + ext));
                paths.Add(System.IO.Path.Combine(tracksDir, trackName + ext));
                paths.Add(System.IO.Path.Combine(appDir, "Resources", "Tracks", sanitized + ext));
            }

            // Fuzzy match: list files in Tracks folder
            if (Directory.Exists(tracksDir))
            {
                foreach (var file in Directory.GetFiles(tracksDir, "*.png")
                    .Concat(Directory.GetFiles(tracksDir, "*.jpg")))
                {
                    string fn = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (trackName.ToLowerInvariant().Contains(fn) || fn.Contains(trackName.ToLowerInvariant()))
                        paths.Add(file);
                }
            }

            return paths.ToArray();
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        }

        // ================================================================
        // FALLBACK: DRAW TRACK
        // ================================================================

        private void DrawFallbackTrack()
        {
            if (_trackPoints.Count < 10) return;

            if (_drawnTrack != null)
                _mapCanvas.Children.Remove(_drawnTrack);

            _drawnTrack = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(70, 200, 220, 220)),
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round
            };

            foreach (var kvp in _trackPoints)
            {
                var (cx, cy) = WorldToCanvas(kvp.Value.x, kvp.Value.z);
                _drawnTrack.Points.Add(new Point(cx, cy));
            }

            if (_trackPoints.Count > 50)
            {
                var first = _trackPoints.First().Value;
                var (fx, fy) = WorldToCanvas(first.x, first.z);
                _drawnTrack.Points.Add(new Point(fx, fy));
            }

            _mapCanvas.Children.Insert(0, _drawnTrack);
        }

        // ================================================================
        // DRAW VEHICLES
        // ================================================================

        private void DrawPlayer(double cx, double cy, Color classColor, int position)
        {
            // Glow ring
            var glow = new Ellipse
            {
                Width = 20, Height = 20,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromArgb(90, classColor.R, classColor.G, classColor.B)),
                StrokeThickness = 2
            };
            SetPos(glow, cx, cy, 20);
            _mapCanvas.Children.Add(glow);
            _dots.Add(glow);

            // Dot
            var dot = new Ellipse
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(classColor),
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };
            SetPos(dot, cx, cy, 12);
            _mapCanvas.Children.Add(dot);
            _dots.Add(dot);

            // Position label
            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 0, 3, 0)
            };
            label.Child = new TextBlock
            {
                Text = $"P{position}", FontSize = 9, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.White
            };
            Canvas.SetLeft(label, cx + 9);
            Canvas.SetTop(label, cy - 8);
            _mapCanvas.Children.Add(label);
            _dots.Add(label);
        }

        private void DrawVehicle(double cx, double cy, Color classColor, bool inPits)
        {
            double size = inPits ? 4 : 7;
            var dot = new Ellipse
            {
                Width = size, Height = size,
                Fill = new SolidColorBrush(classColor),
                Opacity = inPits ? 0.25 : 0.85
            };
            SetPos(dot, cx, cy, size);
            _mapCanvas.Children.Add(dot);
            _dots.Add(dot);
        }

        private static void SetPos(UIElement el, double cx, double cy, double size)
        {
            Canvas.SetLeft(el, cx - size / 2);
            Canvas.SetTop(el, cy - size / 2);
        }

        // ================================================================
        // COORDINATE MAPPING
        // ================================================================

        private (double x, double y) WorldToCanvas(double wx, double wz)
        {
            double rangeX = Math.Max(1, _maxX - _minX);
            double rangeZ = Math.Max(1, _maxZ - _minZ);

            // Uniform scale (preserve aspect ratio)
            double usableW = MapW - 2 * Pad;
            double usableH = MapH - 2 * Pad;
            double scale = Math.Min(usableW / rangeX, usableH / rangeZ);

            double drawW = rangeX * scale;
            double drawH = rangeZ * scale;

            // Center in canvas
            double cx = (MapW - drawW) / 2 + (wx - _minX) * scale;
            double cy = (MapH - drawH) / 2 + (wz - _minZ) * scale;
            return (cx, cy);
        }
    }
}
