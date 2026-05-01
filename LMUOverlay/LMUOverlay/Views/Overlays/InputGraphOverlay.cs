using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class InputGraphOverlay : BaseOverlayWindow
    {
        private readonly InputDisplayConfig _cfg;

        // Bars
        private readonly Border _throttleBar, _brakeBar, _clutchBar;
        private readonly StackPanel _barsPanel;
        // Ghost markers (marqueur doré = inputs du meilleur tour à la position actuelle)
        private Border _ghostThrottleMark = null!, _ghostBrakeMark = null!, _ghostClutchMark = null!;

        // Steering
        private readonly RotateTransform _wheelRotation;
        private readonly TextBlock _steeringText;
        private readonly StackPanel _steeringPanel;
        private readonly Border _steerBarLeft, _steerBarRight;
        private readonly Grid _steerBarContainer;
        private const double MaxSteerDeg = 450;

        // Info
        private readonly TextBlock _gearText, _speedText, _rpmText;
        private readonly StackPanel _gearPanel, _speedPanel, _rpmPanel;

        // Graph
        private readonly Canvas _graphCanvas;
        private readonly Border _graphContainer;
        private readonly Queue<InputData> _history = new();
        private const int MaxHistory = 120;
        private const double GW = 180, GH = 60;

        // Trail brake alert
        private Border? _outerBorder;
        private int _trailBrakeFlash;

        // Dynamic calibration — tracks observed max to correct controllers that don't reach 1.0
        private double _maxThrottle = 1.0;
        private double _maxBrake    = 1.0;
        private double _maxClutch   = 1.0;

        public InputGraphOverlay(DataService ds, OverlaySettings s, InputDisplayConfig cfg) : base(ds, s)
        {
            _cfg = cfg;

            var border = OverlayHelper.MakeBorder();
            _outerBorder = border;
            var mainStack = new StackPanel();

            // === TOP ROW: Gear | Steering | Speed | RPM (horizontal) ===
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            // Gear
            _gearPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
            _gearText = new TextBlock
            {
                Text = "N", FontSize = 30, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _gearPanel.Children.Add(_gearText);
            topRow.Children.Add(_gearPanel);

            // Steering wheel
            _steeringPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
            _wheelRotation = new RotateTransform(0);
            var wheelContainer = new Grid
            {
                Width = 54, Height = 54,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _wheelRotation
            };
            wheelContainer.Children.Add(new Ellipse
            {
                Width = 48, Height = 48,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                StrokeThickness = 2.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            wheelContainer.Children.Add(new Border
            {
                Width = 4, Height = 14, CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(255, 204, 0)),
                VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            });
            wheelContainer.Children.Add(new Ellipse
            {
                Width = 8, Height = 8,
                Fill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            _steeringPanel.Children.Add(wheelContainer);
            _steeringText = new TextBlock
            {
                FontSize = 9, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _steeringPanel.Children.Add(_steeringText);
            topRow.Children.Add(_steeringPanel);

            // Speed
            _speedPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
            _speedText = new TextBlock
            {
                Text = "0", FontSize = 18, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _speedPanel.Children.Add(_speedText);
            _speedPanel.Children.Add(new TextBlock { Text = "km/h", FontSize = 7, Foreground = Br(100, 106, 115), HorizontalAlignment = HorizontalAlignment.Center });
            topRow.Children.Add(_speedPanel);

            // RPM
            _rpmPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
            _rpmText = new TextBlock
            {
                FontSize = 10, FontFamily = new FontFamily("Consolas"),
                Foreground = Br(139, 148, 158),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _rpmPanel.Children.Add(_rpmText);
            topRow.Children.Add(_rpmPanel);

            mainStack.Children.Add(topRow);

            // === STEERING BAR (horizontal, below top row) ===
            _steerBarContainer = new Grid { Height = 6, Margin = new Thickness(2, 2, 2, 2) };
            _steerBarContainer.ColumnDefinitions.Add(new ColumnDefinition());
            _steerBarContainer.ColumnDefinitions.Add(new ColumnDefinition());

            var barBg = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                CornerRadius = new CornerRadius(3)
            };
            Grid.SetColumnSpan(barBg, 2);
            _steerBarContainer.Children.Add(barBg);

            _steerBarLeft = new Border { CornerRadius = new CornerRadius(3, 0, 0, 3), Background = Br(255, 204, 0), HorizontalAlignment = HorizontalAlignment.Right, Width = 0 };
            Grid.SetColumn(_steerBarLeft, 0);
            _steerBarContainer.Children.Add(_steerBarLeft);

            _steerBarRight = new Border { CornerRadius = new CornerRadius(0, 3, 3, 0), Background = Br(255, 204, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            Grid.SetColumn(_steerBarRight, 1);
            _steerBarContainer.Children.Add(_steerBarRight);

            mainStack.Children.Add(_steerBarContainer);

            // === BOTTOM ROW: Bars (T/B/C) LEFT | Graph RIGHT ===
            var bottomGrid = new Grid { Margin = new Thickness(2, 0, 2, 2) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // bars
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition()); // graph

            // Bars
            _barsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 3, 0) };
            _throttleBar = CreateInputBar(InputDisplayConfig.ParseColor(_cfg.ThrottleColor));
            _brakeBar = CreateInputBar(InputDisplayConfig.ParseColor(_cfg.BrakeColor));
            _clutchBar = CreateInputBar(InputDisplayConfig.ParseColor(_cfg.ClutchColor));
            _barsPanel.Children.Add(WrapBar(_throttleBar, "T", out _ghostThrottleMark));
            _barsPanel.Children.Add(WrapBar(_brakeBar,    "B", out _ghostBrakeMark));
            _barsPanel.Children.Add(WrapBar(_clutchBar,   "C", out _ghostClutchMark));
            Grid.SetColumn(_barsPanel, 0);
            bottomGrid.Children.Add(_barsPanel);

            // Graph
            _graphCanvas = new Canvas { Width = GW, Height = GH, ClipToBounds = true, Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)) };
            _graphContainer = new Border { Child = _graphCanvas, CornerRadius = new CornerRadius(4) };
            Grid.SetColumn(_graphContainer, 1);
            bottomGrid.Children.Add(_graphContainer);

            mainStack.Children.Add(bottomGrid);

            border.Child = mainStack;
            Content = border;

            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            _gearPanel.Visibility = _cfg.ShowGear ? Visibility.Visible : Visibility.Collapsed;
            _barsPanel.Visibility = (_cfg.ShowThrottle || _cfg.ShowBrake || _cfg.ShowClutch) ? Visibility.Visible : Visibility.Collapsed;
            _steeringPanel.Visibility = _cfg.ShowSteering ? Visibility.Visible : Visibility.Collapsed;
            _speedPanel.Visibility = _cfg.ShowSpeed ? Visibility.Visible : Visibility.Collapsed;
            _rpmPanel.Visibility = _cfg.ShowRPM ? Visibility.Visible : Visibility.Collapsed;
            _steerBarContainer.Visibility = _cfg.ShowSteeringBar ? Visibility.Visible : Visibility.Collapsed;
            _graphContainer.Visibility = _cfg.ShowGraph ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================================================================
        // UPDATE
        // ================================================================

        public override void UpdateData()
        {
            var input = DataService.GetInputData();
            _history.Enqueue(input);
            if (_history.Count > MaxHistory) _history.Dequeue();

            ApplyVisibility();

            // Gear
            if (_cfg.ShowGear)
                _gearText.Text = input.Gear switch { -1 => "R", 0 => "N", _ => input.Gear.ToString() };

            // Dynamic calibration: track observed max per session (min 1.0 so uncalibrated = unchanged)
            double absThrottle = Math.Abs(input.Throttle);
            double absBrake    = Math.Abs(input.Brake);
            double absClutch   = Math.Abs(input.Clutch);
            if (absThrottle > _maxThrottle) _maxThrottle = absThrottle;
            if (absBrake    > _maxBrake)    _maxBrake    = absBrake;
            if (absClutch   > _maxClutch)   _maxClutch   = absClutch;

            // Bars — scaled to observed max so 100% always fills the bar
            if (_cfg.ShowThrottle) _throttleBar.Height = Math.Max(0, Math.Min(60, (absThrottle / _maxThrottle) * 60));
            if (_cfg.ShowBrake)    _brakeBar.Height    = Math.Max(0, Math.Min(60, (absBrake    / _maxBrake)    * 60));
            if (_cfg.ShowClutch)   _clutchBar.Height   = Math.Max(0, Math.Min(60, (absClutch   / _maxClutch)   * 60));

            // Steering wheel
            if (_cfg.ShowSteering)
            {
                double steer = Math.Clamp(input.Steering, -1.0, 1.0);
                _wheelRotation.Angle = steer * MaxSteerDeg;
                _steeringText.Text = $"{steer * MaxSteerDeg:F0}°";
            }

            // Steering bar
            if (_cfg.ShowSteeringBar)
            {
                double steer = Math.Clamp(input.Steering, -1.0, 1.0);
                double barMax = 120;
                _steerBarLeft.Width = steer < 0 ? Math.Abs(steer) * barMax : 0;
                _steerBarRight.Width = steer > 0 ? steer * barMax : 0;
            }

            // Speed / RPM
            if (_cfg.ShowSpeed) _speedText.Text = $"{input.Speed:F0}";
            if (_cfg.ShowRPM)
            {
                _rpmText.Text = $"{input.RPM:F0}";
                double rpmPct = input.MaxRPM > 0 ? input.RPM / input.MaxRPM : 0;
                _rpmText.Foreground = new SolidColorBrush(
                    rpmPct > 0.95 ? Color.FromRgb(255, 59, 48) :
                    rpmPct > 0.85 ? Color.FromRgb(255, 204, 0) :
                    Color.FromRgb(139, 148, 158));
            }

            // Graph
            if (_cfg.ShowGraph) DrawGraph(input);

            // Ghost bar markers hidden — ghost traces shown in graph canvas only
            _ghostThrottleMark.Visibility = Visibility.Collapsed;
            _ghostBrakeMark.Visibility    = Visibility.Collapsed;
            _ghostClutchMark.Visibility   = Visibility.Collapsed;

            // Trail brake alert: flash border when throttle AND brake pressed simultaneously
            if (_cfg.TrailBrakeAlert && input.Throttle > 0.1 && input.Brake > 0.1 && _outerBorder != null)
            {
                _trailBrakeFlash++;
                bool on = (_trailBrakeFlash / 2) % 2 == 0;
                _outerBorder.BorderBrush = new SolidColorBrush(on
                    ? Color.FromRgb(255, 165, 0) : Color.FromRgb(180, 50, 0));
                _outerBorder.BorderThickness = new Thickness(2);
            }
            else if (_outerBorder != null)
            {
                _trailBrakeFlash = 0;
                _outerBorder.BorderBrush = new SolidColorBrush(OverlayHelper.BorderColor);
                _outerBorder.BorderThickness = new Thickness(1);
            }
        }

        private void DrawGraph(InputData current)
        {
            _graphCanvas.Children.Clear();
            var data = _history.ToArray();
            if (data.Length < 2) return;

            // Center line
            _graphCanvas.Children.Add(new Line
            {
                X1 = 0, Y1 = GH / 2, X2 = GW, Y2 = GH / 2,
                Stroke = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), StrokeThickness = 0.5
            });

            // ── Traces ghost (meilleur tour, alignées par position piste) ──
            if (_cfg.ShowGhostInputs)
            {
                var ghostColor = Color.FromRgb(255, 215, 0); // or
                if (_cfg.ShowThrottle) DrawGhostTrace(data, p => p.Throttle, ghostColor, false);
                if (_cfg.ShowBrake)    DrawGhostTrace(data, p => p.Brake,    ghostColor, false);
                if (_cfg.ShowSteering) DrawGhostTrace(data, p => p.Steering, ghostColor, true);
            }

            // ── Traces actuelles ───────────────────────────────────────────
            if (_cfg.ShowThrottle) DrawTrace(data, d => d.Throttle, InputDisplayConfig.ParseColor(_cfg.ThrottleColor), false);
            if (_cfg.ShowBrake)    DrawTrace(data, d => d.Brake,    InputDisplayConfig.ParseColor(_cfg.BrakeColor),    false);
            if (_cfg.ShowSteering) DrawTrace(data, d => d.Steering, InputDisplayConfig.ParseColor(_cfg.SteeringColor), true);
        }

        private void DrawTrace(InputData[] data, Func<InputData, double> sel, Color color, bool centered)
        {
            var line = new Polyline { Stroke = new SolidColorBrush(color), StrokeThickness = _cfg.LineThickness, Opacity = 0.8 };
            double step = GW / MaxHistory;
            for (int i = 0; i < data.Length; i++)
            {
                double v = Math.Clamp(sel(data[i]), -1, 1);
                double y = centered ? (GH / 2) - (v * GH / 2) : GH - Math.Abs(v) * GH;
                line.Points.Add(new Point(i * step, Math.Clamp(y, 0, GH)));
            }
            _graphCanvas.Children.Add(line);
        }

        /// <summary>
        /// Trace ghost : pour chaque point de l'historique, on cherche les inputs
        /// du meilleur tour à la même position sur la piste.
        /// </summary>
        private void DrawGhostTrace(InputData[] data, Func<TelemetryPoint, double> sel, Color color, bool centered)
        {
            var line = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(160, color.R, color.G, color.B)),
                StrokeThickness = _cfg.LineThickness
            };
            double step = GW / MaxHistory;
            for (int i = 0; i < data.Length; i++)
            {
                var ghost = DataService.GetGhostInputsAt(data[i].TrackPos);
                if (ghost == null) continue;
                double v = Math.Clamp(sel(ghost), -1, 1);
                double y = centered ? (GH / 2) - (v * GH / 2) : GH - Math.Abs(v) * GH;
                line.Points.Add(new Point(i * step, Math.Clamp(y, 0, GH)));
            }
            if (line.Points.Count >= 2)
                _graphCanvas.Children.Add(line);
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private static Border CreateInputBar(Color color) => new Border
        {
            Width = 8, Height = 0, CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Bottom
        };

        /// <summary>
        /// Crée le panneau de barre avec son marqueur ghost doré (out).
        /// Le marqueur ghost est un trait horizontal positionné par marge-bas.
        /// </summary>
        private static StackPanel WrapBar(Border bar, string label, out Border ghostMark)
        {
            // Marqueur ghost : fin trait horizontal or
            ghostMark = new Border
            {
                Width = 12, Height = 2,
                Background = new SolidColorBrush(Color.FromArgb(210, 255, 215, 0)),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed,
                CornerRadius = new CornerRadius(1)
            };

            var container = new Grid { Width = 14, Height = 65, Margin = new Thickness(1) };
            container.Children.Add(new Border
            {
                Width = 8, Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Stretch
            });
            container.Children.Add(bar);
            container.Children.Add(ghostMark);

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(container);
            sp.Children.Add(new TextBlock
            {
                Text = label, FontSize = 7, Foreground = new SolidColorBrush(Color.FromRgb(80, 85, 95)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return sp;
        }

        /// <summary>
        /// Positionne le marqueur ghost à la hauteur correspondant à la valeur (0-1).
        /// </summary>
        private static void UpdateGhostMark(Border mark, double? value)
        {
            if (value == null)
            {
                mark.Visibility = Visibility.Collapsed;
                return;
            }
            const double barHeight = 60.0;
            double pixelsFromBottom = Math.Clamp(value.Value, 0, 1) * barHeight;
            mark.Margin     = new Thickness(0, 0, 0, pixelsFromBottom);
            mark.Visibility = Visibility.Visible;
        }

        private static SolidColorBrush Br(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
