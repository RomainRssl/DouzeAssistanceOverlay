using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class TrackLimitsOverlay : BaseOverlayWindow
    {
        private readonly TextBlock _warningCount, _penaltyCount, _offTrackCount;
        private readonly TextBlock _statusText;
        private readonly Rectangle[] _wheelIndicators = new Rectangle[4];
        private readonly TextBlock[] _wheelLabels = new TextBlock[4];
        private readonly StackPanel _eventList;
        private readonly Border _flashBorder;
        private readonly Border _carBody;

        // Car diagram dimensions
        private const double DiagW = 130, DiagH = 100;
        private const double CarW = 36, CarH = 60;
        private const double CarX = (DiagW - CarW) / 2;  // center horizontally
        private const double CarY = (DiagH - CarH) / 2;  // center vertically
        private const double WheelW = 28, WheelH = 12;

        public TrackLimitsOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var border = OverlayHelper.MakeBorder();
            var sp = new StackPanel();
            sp.Children.Add(OverlayHelper.MakeTitle("TRACK LIMITS"));

            // Flash border
            _flashBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 2)
            };

            // === Counters ===
            var counters = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            counters.ColumnDefinitions.Add(new ColumnDefinition());
            counters.ColumnDefinitions.Add(new ColumnDefinition());
            counters.ColumnDefinitions.Add(new ColumnDefinition());

            var warnPanel = MakeCounter("WARN", out _warningCount, Color.FromRgb(255, 204, 0));
            var penPanel = MakeCounter("PEN", out _penaltyCount, Color.FromRgb(255, 59, 48));
            var offPanel = MakeCounter("OFF", out _offTrackCount, Color.FromRgb(255, 149, 0));
            Grid.SetColumn(warnPanel, 0); Grid.SetColumn(penPanel, 1); Grid.SetColumn(offPanel, 2);
            counters.Children.Add(warnPanel); counters.Children.Add(penPanel); counters.Children.Add(offPanel);

            _flashBorder.Child = counters;
            sp.Children.Add(_flashBorder);

            // === Car + wheels diagram (Canvas for precise placement) ===
            var diagCanvas = new Canvas
            {
                Width = DiagW, Height = DiagH,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };

            // Car body (centered)
            _carBody = new Border
            {
                Width = CarW, Height = CarH,
                CornerRadius = new CornerRadius(8, 8, 4, 4),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 200, 220, 220)),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255))
            };
            Canvas.SetLeft(_carBody, CarX);
            Canvas.SetTop(_carBody, CarY);
            diagCanvas.Children.Add(_carBody);

            // Wheels: FL(0), FR(1), RL(2), RR(3)
            // Positioned just outside the car body edges
            double wheelOffsetX = 6;  // gap between wheel and car side
            double frontY = CarY + 2;
            double rearY = CarY + CarH - WheelH - 2;
            double leftX = CarX - WheelW - wheelOffsetX;
            double rightX = CarX + CarW + wheelOffsetX;

            double[,] wheelPos = {
                { leftX,  frontY },  // FL
                { rightX, frontY },  // FR
                { leftX,  rearY },   // RL
                { rightX, rearY },   // RR
            };

            string[] labels = { "Sec", "Sec", "Sec", "Sec" };

            for (int i = 0; i < 4; i++)
            {
                // Wheel rectangle
                _wheelIndicators[i] = new Rectangle
                {
                    Width = WheelW, Height = WheelH,
                    RadiusX = 3, RadiusY = 3,
                    Fill = new SolidColorBrush(Color.FromRgb(76, 217, 100))
                };
                Canvas.SetLeft(_wheelIndicators[i], wheelPos[i, 0]);
                Canvas.SetTop(_wheelIndicators[i], wheelPos[i, 1]);
                diagCanvas.Children.Add(_wheelIndicators[i]);

                // Label below wheel
                _wheelLabels[i] = new TextBlock
                {
                    Text = labels[i], FontSize = 7,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 120)),
                    TextAlignment = TextAlignment.Center,
                    Width = WheelW
                };
                Canvas.SetLeft(_wheelLabels[i], wheelPos[i, 0]);
                Canvas.SetTop(_wheelLabels[i], wheelPos[i, 1] + WheelH + 1);
                diagCanvas.Children.Add(_wheelLabels[i]);
            }

            sp.Children.Add(diagCanvas);

            // === Status ===
            _statusText = new TextBlock
            {
                FontSize = 12, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            sp.Children.Add(_statusText);

            // === Event history ===
            sp.Children.Add(new TextBlock
            {
                Text = "HISTORIQUE", FontSize = 7,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 80, 80)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            });

            _eventList = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = _eventList,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                MaxHeight = 80
            };
            sp.Children.Add(scroll);

            border.Child = sp;
            Content = border;
        }

        private static StackPanel MakeCounter(string label, out TextBlock value, Color color)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = label, FontSize = 8, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 120)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            value = new TextBlock
            {
                Text = "0", FontSize = 22, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(value);
            return sp;
        }

        private int _lastEventCount;

        public override void UpdateData()
        {
            var tl = DataService.GetTrackLimitsData();

            // Counters
            _warningCount.Text = tl.TrackLimitWarnings.ToString();
            _penaltyCount.Text = tl.PenaltyCount.ToString();
            _offTrackCount.Text = tl.OffTrackCount.ToString();

            _penaltyCount.Foreground = new SolidColorBrush(
                tl.PenaltyCount > 0 ? Color.FromRgb(255, 59, 48) : Color.FromRgb(76, 217, 100));

            // Flash
            _flashBorder.Background = tl.IsOffTrackNow
                ? new SolidColorBrush(Color.FromArgb(35, 255, 59, 48))
                : Brushes.Transparent;

            // Car body glow when off track
            _carBody.BorderBrush = new SolidColorBrush(tl.IsOffTrackNow
                ? Color.FromArgb(150, 255, 59, 48)
                : Color.FromArgb(60, 200, 220, 220));

            // Wheels
            for (int i = 0; i < 4; i++)
            {
                Color c = tl.WheelOffTrack[i]
                    ? Color.FromRgb(255, 59, 48)
                    : Color.FromRgb(76, 217, 100);
                _wheelIndicators[i].Fill = new SolidColorBrush(c);

                string surface = tl.WheelSurface[i] ?? "?";
                _wheelLabels[i].Text = surface.Length > 4 ? surface[..4] : surface;
                _wheelLabels[i].Foreground = new SolidColorBrush(tl.WheelOffTrack[i]
                    ? Color.FromRgb(255, 120, 100)
                    : Color.FromRgb(100, 120, 120));
            }

            // Status
            if (tl.IsOffTrackNow)
            {
                _statusText.Text = "HORS PISTE";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
            }
            else if (!string.IsNullOrEmpty(tl.LastLSIMessage))
            {
                _statusText.Text = tl.LastLSIMessage.Length > 25 ? tl.LastLSIMessage[..25] : tl.LastLSIMessage;
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
            }
            else if (tl.PenaltyCount > 0)
            {
                _statusText.Text = $"{tl.PenaltyCount} pénalité(s)";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
            }
            else
            {
                _statusText.Text = "Piste OK";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 217, 100));
            }

            // Event list
            if (tl.RecentEvents.Count != _lastEventCount)
            {
                _lastEventCount = tl.RecentEvents.Count;
                _eventList.Children.Clear();

                foreach (var evt in tl.RecentEvents.AsEnumerable().Reverse().Take(8))
                {
                    Color typeCol = evt.Type switch
                    {
                        "Pénalité" => Color.FromRgb(255, 59, 48),
                        "Hors-piste" => Color.FromRgb(255, 149, 0),
                        "Message" => Color.FromRgb(88, 166, 255),
                        _ => Color.FromRgb(100, 120, 120)
                    };

                    var row = new TextBlock
                    {
                        FontSize = 8, FontFamily = new FontFamily("Consolas"),
                        Foreground = new SolidColorBrush(typeCol),
                        Margin = new Thickness(0, 0, 0, 1),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    row.Inlines.Add(new System.Windows.Documents.Run($"T{evt.LapNumber} ")
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(80, 100, 100))
                    });
                    row.Inlines.Add(new System.Windows.Documents.Run($"{evt.Type} ")
                    {
                        FontWeight = FontWeights.SemiBold
                    });
                    row.Inlines.Add(new System.Windows.Documents.Run(evt.Detail)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(130, 150, 150))
                    });

                    _eventList.Children.Add(row);
                }
            }
        }
    }
}
