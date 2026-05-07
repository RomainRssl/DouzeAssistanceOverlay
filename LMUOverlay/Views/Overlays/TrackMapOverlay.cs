using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Track map overlay. The track layout is recorded while driving and persisted to disk.
    /// Performance: vehicle dots are pooled (no per-frame allocation), transform values cached.
    /// </summary>
    public class TrackMapOverlay : BaseOverlayWindow
    {
        private readonly Canvas    _mapCanvas;
        private readonly Canvas    _dotsCanvas;   // separate layer — cleared cheaply
        private readonly Polyline  _trackOutline;
        private readonly Polyline  _trackCenter;
        private readonly TextBlock _statusLabel;

        private const double MapW = 280, MapH = 280;
        private const double Pad  = 14;

        // Cached transform (only recomputed on track rebuild)
        private double _scaleX, _scaleZ, _drawOffX, _drawOffY;
        private double _minX, _minZ;
        private bool   _transformValid;

        // Track change detection
        private int    _renderedPointCount;
        private string _currentTrack = "";

        // ── Pooled vehicle visuals (zero per-frame allocation) ──────────
        private static readonly FontFamily _consolas    = new("Consolas");
        private static readonly FontFamily _segoeUI     = new("Segoe UI");

        // Player visuals (always 1)
        private readonly Ellipse    _playerGlow;
        private readonly Ellipse    _playerDot;
        private readonly Border     _playerLabel;
        private readonly TextBlock  _playerLabelText;

        // Opponent dot pool (grows if needed, never shrinks to avoid thrashing)
        private readonly List<Ellipse> _opponentPool = new();

        public TrackMapOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var border = new Border { Padding = new Thickness(2) };

            // ── Dot layer (clear all at once each frame) ─────────────────
            _dotsCanvas = new Canvas
            {
                Width  = MapW,
                Height = MapH,
                IsHitTestVisible = false
            };

            _mapCanvas = new Canvas
            {
                Width  = MapW,
                Height = MapH,
                ClipToBounds = true,
                Background = Brushes.Transparent
            };

            // Read settings
            double outlineT = s.CustomOptions.TryGetValue("OutlineThickness", out var ov) ? Convert.ToDouble(ov) : 20.0;
            double centerT  = s.CustomOptions.TryGetValue("CenterThickness",  out var cv) ? Convert.ToDouble(cv) : 10.0;
            var outlineColor = ParseColor(s.CustomOptions.TryGetValue("OutlineColor", out var ocv) ? ocv?.ToString() : null, Colors.Black);
            var centerColor  = ParseColor(s.CustomOptions.TryGetValue("CenterColor",  out var ccv) ? ccv?.ToString() : null, Colors.White);

            // ── Track polylines ──────────────────────────────────────────
            _trackOutline = new Polyline
            {
                Stroke             = new SolidColorBrush(outlineColor),
                StrokeThickness    = outlineT,
                StrokeLineJoin     = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round
            };
            _trackCenter = new Polyline
            {
                Stroke             = new SolidColorBrush(centerColor),
                StrokeThickness    = centerT,
                StrokeLineJoin     = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round
            };

            // ── Status label ─────────────────────────────────────────────
            _statusLabel = new TextBlock
            {
                Foreground  = new SolidColorBrush(Color.FromArgb(180, 255, 200, 80)),
                FontSize    = 9,
                FontFamily  = _consolas,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_statusLabel, 4);
            Canvas.SetTop(_statusLabel, MapH - 18);
            Canvas.SetZIndex(_statusLabel, 100);

            // ── Pooled player visuals ────────────────────────────────────
            _playerGlow = new Ellipse
            {
                Width = 20, Height = 20,
                Fill  = Brushes.Transparent,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed
            };
            _playerDot = new Ellipse
            {
                Width = 12, Height = 12,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                Visibility = Visibility.Collapsed
            };
            _playerLabelText = new TextBlock
            {
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                FontFamily = _consolas,
                Foreground = Brushes.White
            };
            _playerLabel = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                CornerRadius = new CornerRadius(2),
                Padding      = new Thickness(3, 0, 3, 0),
                Child        = _playerLabelText,
                Visibility   = Visibility.Collapsed
            };
            Canvas.SetZIndex(_playerLabel, 50);

            // Add static elements to canvases
            _mapCanvas.Children.Add(_trackOutline);
            _mapCanvas.Children.Add(_trackCenter);
            _mapCanvas.Children.Add(_dotsCanvas);
            _mapCanvas.Children.Add(_statusLabel);

            // Player visuals live on the dots canvas
            _dotsCanvas.Children.Add(_playerGlow);
            _dotsCanvas.Children.Add(_playerDot);
            _dotsCanvas.Children.Add(_playerLabel);

            border.Child = _mapCanvas;
            Content      = border;
        }

        // ================================================================
        // UPDATE (hot path — zero allocation)
        // ================================================================

        public override void UpdateData()
        {
            var d = DataService.GetTrackMapData();
            if (d.Vehicles.Count == 0) return;

            // Rebuild track geometry only when something changed
            if (d.PointCount != _renderedPointCount || d.TrackName != _currentTrack)
            {
                RebuildTrackPolylines(d.TrackPoints);
                _renderedPointCount = d.PointCount;
                _currentTrack       = d.TrackName;
            }

            // Status label (only update text when recording in progress)
            _statusLabel.Text = d.TrackRecorded ? "" : $"ENREG. {d.PointCount} pts";

            if (!_transformValid) return;

            // ── Reuse pooled opponent dots ────────────────────────────────
            // Ensure pool is large enough
            int opponentCount = 0;
            foreach (var v in d.Vehicles) if (!v.IsPlayer) opponentCount++;

            while (_opponentPool.Count < opponentCount)
            {
                var e = new Ellipse { StrokeThickness = 0 };
                Canvas.SetZIndex(e, 10);
                _dotsCanvas.Children.Add(e);
                _opponentPool.Add(e);
            }

            // Hide all opponent dots first, then reuse
            for (int i = 0; i < _opponentPool.Count; i++)
                _opponentPool[i].Visibility = Visibility.Collapsed;

            int oIdx = 0;
            bool playerFound = false;

            foreach (var v in d.Vehicles)
            {
                var (cx, cy) = WorldToCanvas(v.PosX, v.PosZ);
                Color classColor = OverlayHelper.GetClassColor(v.VehicleClass);

                if (v.IsPlayer)
                {
                    PlacePlayer(cx, cy, classColor, v.Position);
                    playerFound = true;
                }
                else
                {
                    PlaceOpponent(_opponentPool[oIdx++], cx, cy, classColor, v.InPits);
                }
            }

            if (!playerFound)
            {
                _playerGlow.Visibility  = Visibility.Collapsed;
                _playerDot.Visibility   = Visibility.Collapsed;
                _playerLabel.Visibility = Visibility.Collapsed;
            }
        }

        // ================================================================
        // PUBLIC API — settings sliders / pickers
        // ================================================================

        public void UpdateThickness(double outlineT, double centerT)
        {
            _trackOutline.StrokeThickness = outlineT;
            _trackCenter.StrokeThickness  = centerT;
        }

        public void UpdateColors(Color outlineColor, Color centerColor)
        {
            _trackOutline.Stroke = new SolidColorBrush(outlineColor);
            _trackCenter.Stroke  = new SolidColorBrush(centerColor);
        }

        // ================================================================
        // PLAYER / OPPONENT PLACEMENT (no allocation — reuse pooled objects)
        // ================================================================

        private void PlacePlayer(double cx, double cy, Color classColor, int position)
        {
            _playerGlow.Stroke     = BrushCache.Get(Color.FromArgb(90, classColor.R, classColor.G, classColor.B));
            _playerGlow.Visibility = Visibility.Visible;
            SetPos(_playerGlow, cx, cy, 20);

            _playerDot.Fill       = BrushCache.Get(classColor);
            _playerDot.Visibility = Visibility.Visible;
            SetPos(_playerDot, cx, cy, 12);

            _playerLabelText.Text   = $"P{position}";
            _playerLabel.Visibility = Visibility.Visible;
            Canvas.SetLeft(_playerLabel, cx + 9);
            Canvas.SetTop(_playerLabel,  cy - 8);
        }

        private static void PlaceOpponent(Ellipse dot, double cx, double cy, Color classColor, bool inPits)
        {
            double size    = inPits ? 4 : 7;
            dot.Width      = size;
            dot.Height     = size;
            dot.Fill       = BrushCache.Get(classColor);
            dot.Opacity    = inPits ? 0.25 : 0.85;
            dot.Visibility = Visibility.Visible;
            SetPos(dot, cx, cy, size);
        }

        private static void SetPos(UIElement el, double cx, double cy, double size)
        {
            Canvas.SetLeft(el, cx - size * 0.5);
            Canvas.SetTop(el,  cy - size * 0.5);
        }

        // ================================================================
        // TRACK POLYLINE REBUILD + TRANSFORM CACHE
        // ================================================================

        private void RebuildTrackPolylines(List<(float X, float Z)> pts)
        {
            _trackOutline.Points.Clear();
            _trackCenter.Points.Clear();
            _transformValid = false;

            if (pts.Count < 5) return;

            // Compute bounds
            float minX = pts[0].X, maxX = pts[0].X;
            float minZ = pts[0].Z, maxZ = pts[0].Z;
            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i].X < minX) minX = pts[i].X;
                if (pts[i].X > maxX) maxX = pts[i].X;
                if (pts[i].Z < minZ) minZ = pts[i].Z;
                if (pts[i].Z > maxZ) maxZ = pts[i].Z;
            }

            double rangeX = Math.Max(1, maxX - minX);
            double rangeZ = Math.Max(1, maxZ - minZ);
            double usableW = MapW - 2 * Pad;
            double usableH = MapH - 2 * Pad;
            double scale   = Math.Min(usableW / rangeX, usableH / rangeZ);

            // Cache transform for WorldToCanvas (zero-cost per-frame)
            _minX    = minX;
            _minZ    = minZ;
            _scaleX  = scale;
            _scaleZ  = scale;
            _drawOffX = (MapW - rangeX * scale) * 0.5;
            _drawOffY = (MapH - rangeZ * scale) * 0.5;
            _transformValid = true;

            var canvasPoints = new PointCollection(pts.Count + 1);
            for (int i = 0; i < pts.Count; i++)
            {
                var (cx, cy) = WorldToCanvas(pts[i].X, pts[i].Z);
                canvasPoints.Add(new Point(cx, cy));
            }
            if (pts.Count > 100)
                canvasPoints.Add(canvasPoints[0]);

            _trackOutline.Points = canvasPoints;
            _trackCenter.Points  = canvasPoints;
        }

        // ================================================================
        // COORDINATE MAPPING (uses cached values — no division each call)
        // ================================================================

        private (double x, double y) WorldToCanvas(double wx, double wz)
        {
            return (_drawOffX + (wx - _minX) * _scaleX,
                    _drawOffY + (wz - _minZ) * _scaleZ);
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private static Color ParseColor(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try { return (Color)ColorConverter.ConvertFromString(hex)!; }
            catch { return fallback; }
        }
    }
}
