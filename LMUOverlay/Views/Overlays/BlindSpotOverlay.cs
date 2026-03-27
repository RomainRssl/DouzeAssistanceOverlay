using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Blind spot indicator: two HUD-style panels (LEFT / RIGHT).
    /// Each shows a glowing rounded square when a car is alongside.
    /// Size is controlled via Viewbox (clean scaling), gap via a spacer Border.
    /// </summary>
    public class BlindSpotOverlay : BaseOverlayWindow
    {
        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly Color CBackground = Color.FromRgb(0x16, 0x16, 0x16);
        private static readonly Color CBracket    = Color.FromRgb(0xA0, 0xA0, 0xA0);
        private static readonly Color CLabel      = Color.FromRgb(0xCC, 0xCC, 0xCC);
        private static readonly Color CSqOff      = Color.FromRgb(0x20, 0x16, 0x06);

        // ── Base dimensions (canvas space — never change) ─────────────────────
        private const double PanelW   = 130;
        private const double PanelH   = 148;
        private const double SqSize   = 84;
        private const double SqRadius = 20;

        // ── Refs for live update ──────────────────────────────────────────────
        private readonly Border           _leftSq,   _rightSq;
        private readonly DropShadowEffect _leftGlow, _rightGlow;
        private readonly Viewbox          _leftVb,   _rightVb;
        private readonly Border           _gapBorder;

        public BlindSpotOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            double scale = s.CustomOptions.TryGetValue("Scale", out var sv) ? Convert.ToDouble(sv) : 1.0;
            double gap   = s.CustomOptions.TryGetValue("Gap",   out var gv) ? Convert.ToDouble(gv) : 10;

            var (lCanvas, lSq, lFx) = MakePanel("LEFT");
            var (rCanvas, rSq, rFx) = MakePanel("RIGHT");

            _leftSq  = lSq;  _leftGlow  = lFx;
            _rightSq = rSq;  _rightGlow = rFx;

            // Each panel is wrapped in a Viewbox for clean proportional scaling
            _leftVb  = new Viewbox { Child = lCanvas, Stretch = Stretch.Uniform,
                                     Width = PanelW * scale, Height = PanelH * scale };
            _rightVb = new Viewbox { Child = rCanvas, Stretch = Stretch.Uniform,
                                     Width = PanelW * scale, Height = PanelH * scale };

            _gapBorder = new Border { Width = gap, Background = Brushes.Transparent };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background  = Brushes.Transparent
            };
            row.Children.Add(_leftVb);
            row.Children.Add(_gapBorder);
            row.Children.Add(_rightVb);

            Content = row;
        }

        /// <summary>Called live from settings sliders — no layout rebuild needed.</summary>
        public void UpdatePanelLayout(double scale, double gap)
        {
            _leftVb.Width   = PanelW * scale;
            _leftVb.Height  = PanelH * scale;
            _rightVb.Width  = PanelW * scale;
            _rightVb.Height = PanelH * scale;
            _gapBorder.Width = gap;
        }

        // ── Panel builder ─────────────────────────────────────────────────────
        private static (Canvas Canvas, Border Square, DropShadowEffect GlowFx) MakePanel(string side)
        {
            // Canvas at natural resolution — Viewbox handles scaling
            var cv = new Canvas { Width = PanelW, Height = PanelH };

            // Background
            var bg = new Border
            {
                Width        = PanelW,
                Height       = PanelH,
                Background   = new SolidColorBrush(CBackground),
                CornerRadius = new CornerRadius(8)
            };
            cv.Children.Add(bg);

            // ── Corner brackets ───────────────────────────────────────────────
            const double M  = 7;
            const double BL = 14;
            const double BW = 1.5;

            // Top-left
            Ln(cv, M,           M,       M + BL,     M,      BW);
            Ln(cv, M,           M,       M,          M + BL, BW);
            // Top-right
            Ln(cv, PanelW-M-BL, M,       PanelW-M,   M,      BW);
            Ln(cv, PanelW-M,    M,       PanelW-M,   M + BL, BW);
            // Bottom-left
            Ln(cv, M,           PanelH-M, M + BL,    PanelH-M, BW);
            Ln(cv, M,           PanelH-M-BL, M,      PanelH-M, BW);
            // Bottom-right
            Ln(cv, PanelW-M-BL, PanelH-M, PanelW-M,  PanelH-M, BW);
            Ln(cv, PanelW-M,    PanelH-M-BL, PanelW-M, PanelH-M, BW);

            // ── Labels ────────────────────────────────────────────────────────
            AddCentredLabel(cv, "BLIND SPOT", 14);
            AddCentredLabel(cv, side, PanelH - 22);

            // ── Glowing square (centred) ──────────────────────────────────────
            double sqTop = (PanelH - SqSize) / 2 - 4;

            var glow = new DropShadowEffect
            {
                Color         = Colors.Orange,
                ShadowDepth   = 0,
                BlurRadius    = 0,
                Opacity       = 0,
                RenderingBias = RenderingBias.Quality
            };
            var square = new Border
            {
                Width        = SqSize,
                Height       = SqSize,
                CornerRadius = new CornerRadius(SqRadius),
                Background   = new SolidColorBrush(CSqOff),
                Effect       = glow
            };
            Canvas.SetLeft(square, (PanelW - SqSize) / 2);
            Canvas.SetTop(square, sqTop);
            cv.Children.Add(square);

            return (cv, square, glow);
        }

        // ── Drawing helpers ───────────────────────────────────────────────────
        private static void Ln(Canvas c, double x1, double y1, double x2, double y2, double t)
        {
            c.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke             = new SolidColorBrush(CBracket),
                StrokeThickness    = t,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round
            });
        }

        private static void AddCentredLabel(Canvas c, string text, double y)
        {
            var tb = new TextBlock
            {
                Text          = text,
                FontSize      = 8,
                FontWeight    = FontWeights.SemiBold,
                FontFamily    = new FontFamily("Consolas"),
                Foreground    = new SolidColorBrush(CLabel),
                Width         = PanelW,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, 0);
            Canvas.SetTop(tb, y);
            c.Children.Add(tb);
        }

        // ── Data update ───────────────────────────────────────────────────────
        public override void UpdateData()
        {
            var (left, right) = DataService.GetBlindSpots();
            Apply(_leftSq,  _leftGlow,  left);
            Apply(_rightSq, _rightGlow, right);
        }

        private static void Apply(Border sq, DropShadowEffect fx, double intensity)
        {
            if (intensity <= 0)
            {
                sq.Background = new SolidColorBrush(CSqOff);
                fx.Opacity    = 0;
                fx.BlurRadius = 0;
                return;
            }

            // Orange → red-orange at high proximity
            byte g = intensity > 0.7 ? (byte)65 : (byte)(int)(165 - intensity * 130);
            var col = Color.FromRgb(255, g, 0);

            sq.Background = new SolidColorBrush(col);
            fx.Color      = col;
            fx.Opacity    = 0.35 + intensity * 0.65;
            fx.BlurRadius = 8 + intensity * 28;
        }
    }
}
