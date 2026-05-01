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
    /// Blind spot indicator: two HUD-style square panels (LEFT / RIGHT).
    /// Size and gap are controlled by sliders and applied instantly.
    /// Manual resize is disabled — the window always auto-fits its content.
    /// </summary>
    public class BlindSpotOverlay : BaseOverlayWindow
    {
        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly Color CBackground = Color.FromRgb(0x16, 0x16, 0x16);
        private static readonly Color CBracket    = Color.FromRgb(0xA0, 0xA0, 0xA0);
        private static readonly Color CLabel      = Color.FromRgb(0xCC, 0xCC, 0xCC);
        private static readonly Color CSqOff      = Color.FromRgb(0x20, 0x16, 0x06);

        // ── Natural panel dimensions (canvas coordinate space) ────────────────
        private const double NatW  = 130;
        private const double NatH  = 148;
        private const double NatSq = 84;
        private const double NatR  = 20;

        // ── Live-update refs ──────────────────────────────────────────────────
        private readonly Viewbox          _leftVb,   _rightVb;
        private readonly Border           _leftSq,   _rightSq;
        private readonly DropShadowEffect _leftGlow, _rightGlow;

        // Root canvas that holds both Viewboxes + the transparent gap
        private readonly Canvas  _root;
        private double _currentScale;
        private double _currentGap;

        public BlindSpotOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            // Prevent manual resize: this overlay auto-sizes from slider values only
            DisableManualResize();

            _currentScale = s.CustomOptions.TryGetValue("Scale", out var sv) ? Convert.ToDouble(sv) : 1.0;
            _currentGap   = s.CustomOptions.TryGetValue("Gap",   out var gv) ? Convert.ToDouble(gv) : 10;

            var (lCanvas, lSq, lFx) = MakePanel("LEFT");
            var (rCanvas, rSq, rFx) = MakePanel("RIGHT");
            _leftSq  = lSq;  _leftGlow  = lFx;
            _rightSq = rSq;  _rightGlow = rFx;

            // Each panel in its own Viewbox — proportional scaling, no distortion
            _leftVb  = new Viewbox { Stretch = Stretch.Uniform, Child = lCanvas };
            _rightVb = new Viewbox { Stretch = Stretch.Uniform, Child = rCanvas };

            // Root canvas: absolute layout so the gap never deforms the panels
            _root = new Canvas { Background = Brushes.Transparent };

            _root.Children.Add(_leftVb);
            _root.Children.Add(_rightVb);

            Content = _root;

            // Apply initial sizes once layout is ready
            Loaded += (_, _) => ApplyLayout(_currentScale, _currentGap);
        }

        // ── Layout helpers ────────────────────────────────────────────────────

        /// <summary>Called live from the settings sliders.</summary>
        public void UpdatePanelLayout(double scale, double gap)
        {
            _currentScale = scale;
            _currentGap   = gap;
            ApplyLayout(scale, gap);
        }

        private void ApplyLayout(double scale, double gap)
        {
            double pw = NatW * scale;
            double ph = NatH * scale;

            // Size each Viewbox
            _leftVb.Width  = pw;  _leftVb.Height  = ph;
            _rightVb.Width = pw;  _rightVb.Height = ph;

            // Position on root canvas
            Canvas.SetLeft(_leftVb,  0);
            Canvas.SetTop(_leftVb,   0);
            Canvas.SetLeft(_rightVb, pw + gap);
            Canvas.SetTop(_rightVb,  0);

            // Size the root canvas so SizeToContent sees the correct dimensions
            _root.Width  = pw * 2 + gap;
            _root.Height = ph;
        }

        // ── Panel builder ─────────────────────────────────────────────────────
        private static (Canvas, Border, DropShadowEffect) MakePanel(string side)
        {
            var cv = new Canvas { Width = NatW, Height = NatH };

            // Background
            cv.Children.Add(new Border
            {
                Width        = NatW,
                Height       = NatH,
                Background   = new SolidColorBrush(CBackground),
                CornerRadius = new CornerRadius(8)
            });

            // Corner brackets
            const double M  = 7;
            const double BL = 14;
            const double BW = 1.5;
            Ln(cv, M,         M,      M+BL,    M,      BW);
            Ln(cv, M,         M,      M,       M+BL,   BW);
            Ln(cv, NatW-M-BL, M,      NatW-M,  M,      BW);
            Ln(cv, NatW-M,    M,      NatW-M,  M+BL,   BW);
            Ln(cv, M,         NatH-M, M+BL,    NatH-M, BW);
            Ln(cv, M,         NatH-M-BL, M,    NatH-M, BW);
            Ln(cv, NatW-M-BL, NatH-M, NatW-M,  NatH-M, BW);
            Ln(cv, NatW-M,    NatH-M-BL, NatW-M, NatH-M, BW);

            // Labels
            Lbl(cv, "BLIND SPOT", 14);
            Lbl(cv, side,          NatH - 22);

            // Glowing square
            var glow = new DropShadowEffect
            {
                Color = Colors.Orange, ShadowDepth = 0,
                BlurRadius = 0, Opacity = 0,
                RenderingBias = RenderingBias.Quality
            };
            var sq = new Border
            {
                Width = NatSq, Height = NatSq,
                CornerRadius = new CornerRadius(NatR),
                Background   = new SolidColorBrush(CSqOff),
                Effect       = glow
            };
            Canvas.SetLeft(sq, (NatW - NatSq) / 2);
            Canvas.SetTop(sq,  (NatH - NatSq) / 2 - 4);
            cv.Children.Add(sq);

            return (cv, sq, glow);
        }

        // ── Drawing helpers ───────────────────────────────────────────────────
        private static void Ln(Canvas c, double x1, double y1, double x2, double y2, double t)
            => c.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush(CBracket), StrokeThickness = t,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            });

        private static void Lbl(Canvas c, string text, double y)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 8, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(CLabel),
                Width = NatW, TextAlignment = TextAlignment.Center
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
            byte g = intensity > 0.7 ? (byte)65 : (byte)(int)(165 - intensity * 130);
            var col = Color.FromRgb(255, g, 0);
            sq.Background = new SolidColorBrush(col);
            fx.Color      = col;
            fx.Opacity    = 0.35 + intensity * 0.65;
            fx.BlurRadius = 8 + intensity * 28;
        }
    }
}
