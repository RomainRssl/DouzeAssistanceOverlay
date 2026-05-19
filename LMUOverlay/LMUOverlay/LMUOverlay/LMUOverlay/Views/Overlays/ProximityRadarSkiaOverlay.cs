using System.Linq;
using System.Windows;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// VR-01 PoC: SkiaSharp SKElement version of the proximity radar.
    /// Draws the same content as ProximityRadarOverlay using imperative Skia canvas calls
    /// instead of WPF Canvas children manipulation.
    /// Frame-time is measured and logged to compare against the WPF baseline.
    /// </summary>
    public class ProximityRadarSkiaOverlay : BaseOverlayWindow
    {
        private const float RadarRange = 30f;
        private const float CW = 170f, CH = 220f;
        private const float CAR_W = 5f, CAR_H = 16f;

        private readonly SKElement _skElement;

        // Data snapshot — written by UpdateData(), read by OnPaintSurface
        private List<(VehicleData Vehicle, double RelX, double RelZ)> _nearby = new();
        private bool _colorByClass;
        private bool _showPosition;

        // Frame-time measurement (VR-01 PoC)
        private readonly Queue<long> _renderTimes = new(500);
        private static readonly long _ticksPerUs = System.Diagnostics.Stopwatch.Frequency / 1_000_000L;
        private int _sampleCount;

        public ProximityRadarSkiaOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            _skElement = new SKElement { Width = CW, Height = CH };
            _skElement.PaintSurface += OnPaintSurface;
            Content = _skElement;
        }

        public override void UpdateData()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

            _colorByClass = Settings.CustomOptions.TryGetValue("ColorByClass", out var cbv)
                            && Convert.ToBoolean(cbv);
            _showPosition = !Settings.CustomOptions.TryGetValue("ShowPosition", out var spv)
                            || Convert.ToBoolean(spv);
            _nearby = DataService.GetNearbyVehicles(RadarRange);
            _skElement.InvalidateVisual();

            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            long us = (t1 - t0) / _ticksPerUs;
            _renderTimes.Enqueue(us);
            if (_renderTimes.Count > 500) _renderTimes.Dequeue();

            _sampleCount++;
            if (_sampleCount >= 500)
            {
                _sampleCount = 0;
                var arr = _renderTimes.ToArray();
                Array.Sort(arr);
                long min = arr[0];
                long avg = (long)arr.Average();
                long p95 = arr[(int)(arr.Length * 0.95)];
                long max = arr[arr.Length - 1];
                System.Diagnostics.Debug.WriteLine(
                    $"[ProximityRadar SKIA] min={min}us avg={avg}us p95={p95}us max={max}us (500 samples)");
            }
        }

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            var tm = ThemeManager.Current;

            // Range rings (dashed ellipses — outer and inner)
            using var ringPaint = new SKPaint
            {
                Style       = SKPaintStyle.Stroke,
                Color       = new SKColor(255, 255, 255, 32),
                StrokeWidth = 0.5f,
                PathEffect  = SKPathEffect.CreateDash(new float[] { 4f, 2f }, 0f),
                IsAntialias = true
            };
            canvas.DrawOval(new SKRect(3f, 3f, 3f + 164f, 3f + 214f), ringPaint);

            using var ringPaint2 = new SKPaint
            {
                Style       = SKPaintStyle.Stroke,
                Color       = new SKColor(255, 255, 255, 21),
                StrokeWidth = 0.5f,
                PathEffect  = SKPathEffect.CreateDash(new float[] { 4f, 2f }, 0f),
                IsAntialias = true
            };
            canvas.DrawOval(new SKRect(35f, 45f, 35f + 100f, 45f + 130f), ringPaint2);

            // Crosshair
            using var crossPaint = new SKPaint
            {
                Color       = new SKColor(255, 255, 255, 16),
                StrokeWidth = 0.5f,
                IsAntialias = false
            };
            canvas.DrawLine(85f, 0f, 85f, CH, crossPaint);
            canvas.DrawLine(0f, 110f, CW, 110f, crossPaint);

            // Player car (center)
            var playerColor = ToSKColor(tm.ClassLmp2);
            using var playerPaint = new SKPaint { Color = playerColor, IsAntialias = true };
            float px = (CW / 2f) - (CAR_W + 1f) / 2f;
            float py = (CH / 2f) - (CAR_H + 2f) / 2f;
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(px, py, px + CAR_W + 1f, py + CAR_H + 2f), 1f), playerPaint);

            // Nearby vehicles
            foreach (var (vehicle, relX, relZ) in _nearby)
            {
                float cx = (CW / 2f) + ((float)relX / RadarRange) * (CW / 2f) - CAR_W / 2f;
                float cy = (CH / 2f) - ((float)relZ / RadarRange) * (CH / 2f) - CAR_H / 2f;
                cx = Math.Clamp(cx, 0f, CW - CAR_W);
                cy = Math.Clamp(cy, 0f, CH - CAR_H);

                double dist    = Math.Sqrt(relX * relX + relZ * relZ);
                float  opacity = (float)Math.Max(0.4, 1.0 - dist / RadarRange);
                bool   dangerBorder = false;

                SKColor fill;
                if (_colorByClass)
                {
                    var wc = OverlayHelper.GetClassColor(vehicle.VehicleClass);
                    fill = ToSKColor(wc);
                    dangerBorder = dist < 5;
                }
                else
                {
                    fill = dist < 5  ? ToSKColor(tm.StateDanger) :
                           dist < 15 ? ToSKColor(tm.StateWarn)   :
                                       ToSKColor(tm.StateGood);
                }

                using var carPaint = new SKPaint
                {
                    Color       = fill.WithAlpha((byte)(opacity * 255)),
                    IsAntialias = true
                };
                var carRect = new SKRect(cx, cy, cx + CAR_W, cy + CAR_H);
                canvas.DrawRoundRect(new SKRoundRect(carRect, 1f), carPaint);

                if (dangerBorder)
                {
                    using var borderPaint = new SKPaint
                    {
                        Style       = SKPaintStyle.Stroke,
                        Color       = ToSKColor(tm.StateDanger),
                        StrokeWidth = 1.5f,
                        IsAntialias = true
                    };
                    canvas.DrawRoundRect(new SKRoundRect(carRect, 1f), borderPaint);
                }

                if (_showPosition && vehicle.Position > 0)
                {
                    using var textPaint = new SKPaint
                    {
                        Color        = ToSKColor(tm.TextPrimary).WithAlpha((byte)(opacity * 255)),
                        TextSize     = 7f,
                        IsAntialias  = true,
                        FakeBoldText = true
                    };
                    canvas.DrawText(vehicle.Position.ToString(),
                                    cx + CAR_W + 2f,
                                    cy + (CAR_H - 9f) / 2f + 7f,  // +7: Skia text Y is baseline, not top-left
                                    textPaint);
                }
            }
        }

        private static SKColor ToSKColor(System.Windows.Media.Color c)
            => new SKColor(c.R, c.G, c.B, c.A);
    }
}
