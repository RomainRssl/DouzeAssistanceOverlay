using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class WeatherOverlay : BaseOverlayWindow
    {
        private readonly TextBlock _ambientTemp, _trackTemp, _rainLevel;
        private readonly TextBlock _windSpeed, _wetness, _cloudCover;
        private readonly TextBlock _forecastText, _rainTrendArrow, _cloudTrendArrow;
        private readonly Border _rainIndicator;

        public WeatherOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var border = OverlayHelper.MakeBorder();
            var main = new StackPanel();

            main.Children.Add(OverlayHelper.MakeTitle("MÉTÉO"));

            // Weather icon
            _rainIndicator = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(18),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 4)
            };
            main.Children.Add(_rainIndicator);

            // Temperature row
            var tempGrid = MakeRow();
            AddCell(tempGrid, "AIR", out _ambientTemp, 0);
            AddCell(tempGrid, "PISTE", out _trackTemp, 1);
            main.Children.Add(tempGrid);

            // Rain & Cloud
            var rainGrid = MakeRow();
            AddCell(rainGrid, "PLUIE", out _rainLevel, 0);
            AddCell(rainGrid, "NUAGES", out _cloudCover, 1);
            main.Children.Add(rainGrid);

            // Wind & Wetness
            var windGrid = MakeRow();
            AddCell(windGrid, "VENT", out _windSpeed, 0);
            AddCell(windGrid, "HUMIDITÉ", out _wetness, 1);
            main.Children.Add(windGrid);

            // === FORECAST SECTION ===
            main.Children.Add(new Border { Height = 1, Background = Br(36, 68, 68), Margin = new Thickness(0, 4, 0, 4) });
            main.Children.Add(new TextBlock
            {
                Text = "PRÉVISIONS", FontSize = 8, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Br(80, 110, 110),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });

            // Forecast text
            _forecastText = new TextBlock
            {
                FontSize = 12, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            main.Children.Add(_forecastText);

            // Trend arrows row
            var trendGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            trendGrid.ColumnDefinitions.Add(new ColumnDefinition());
            trendGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var rainTrendPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal };
            rainTrendPanel.Children.Add(new TextBlock { Text = "Pluie ", FontSize = 8, FontFamily = new FontFamily("Consolas"), Foreground = Br(80, 110, 110), VerticalAlignment = VerticalAlignment.Center });
            _rainTrendArrow = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
            rainTrendPanel.Children.Add(_rainTrendArrow);
            Grid.SetColumn(rainTrendPanel, 0);
            trendGrid.Children.Add(rainTrendPanel);

            var cloudTrendPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal };
            cloudTrendPanel.Children.Add(new TextBlock { Text = "Nuages ", FontSize = 8, FontFamily = new FontFamily("Consolas"), Foreground = Br(80, 110, 110), VerticalAlignment = VerticalAlignment.Center });
            _cloudTrendArrow = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
            cloudTrendPanel.Children.Add(_cloudTrendArrow);
            Grid.SetColumn(cloudTrendPanel, 1);
            trendGrid.Children.Add(cloudTrendPanel);

            main.Children.Add(trendGrid);

            border.Child = main;
            Content = border;
        }

        public override void UpdateData()
        {
            var w = DataService.GetWeatherData();

            _ambientTemp.Text = $"{w.AmbientTemp:F1}°";
            _trackTemp.Text = $"{w.TrackTemp:F1}°";
            _rainLevel.Text = $"{w.Raining * 100:F0}%";
            _cloudCover.Text = $"{w.CloudCover * 100:F0}%";

            double windMag = Math.Sqrt(w.WindSpeedX * w.WindSpeedX + w.WindSpeedZ * w.WindSpeedZ);
            _windSpeed.Text = $"{windMag * 3.6:F0}km/h";
            _wetness.Text = $"{w.TrackWetness * 100:F0}%";

            // Rain indicator icon
            Color iColor;
            string icon;
            if (w.Raining > 0.5) { iColor = Color.FromRgb(0, 122, 255); icon = "🌧"; }
            else if (w.Raining > 0.1) { iColor = Color.FromRgb(88, 166, 255); icon = "🌦"; }
            else if (w.CloudCover > 0.5) { iColor = Color.FromRgb(139, 148, 158); icon = "☁"; }
            else { iColor = Color.FromRgb(255, 204, 0); icon = "☀"; }

            _rainIndicator.Background = new SolidColorBrush(Color.FromArgb(40, iColor.R, iColor.G, iColor.B));
            _rainIndicator.Child = new TextBlock
            {
                Text = icon, FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Forecast
            if (!string.IsNullOrEmpty(w.ForecastText))
            {
                _forecastText.Text = w.ForecastText;
                Color fColor = w.ForecastText.Contains("Pluie") || w.ForecastText.Contains("Risque")
                    ? Color.FromRgb(88, 166, 255)
                    : w.ForecastText.Contains("Sec") || w.ForecastText.Contains("Dégagé")
                        ? Color.FromRgb(76, 217, 100)
                        : Color.FromRgb(200, 210, 210);
                _forecastText.Foreground = new SolidColorBrush(fColor);
            }
            else
            {
                _forecastText.Text = "En analyse...";
                _forecastText.Foreground = Br(80, 110, 110);
            }

            // Trend arrows
            SetTrendArrow(_rainTrendArrow, w.RainTrend, 0.02);
            SetTrendArrow(_cloudTrendArrow, w.CloudTrend, 0.03);
        }

        private static void SetTrendArrow(TextBlock tb, double trend, double threshold)
        {
            if (trend > threshold)
            {
                tb.Text = "▲";
                tb.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80));
            }
            else if (trend < -threshold)
            {
                tb.Text = "▼";
                tb.Foreground = new SolidColorBrush(Color.FromRgb(76, 217, 100));
            }
            else
            {
                tb.Text = "—";
                tb.Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 120));
            }
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private static Grid MakeRow()
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            return g;
        }

        private static void AddCell(Grid grid, string label, out TextBlock value, int col)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = label, FontSize = 7, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(80, 110, 110)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            value = new TextBlock
            {
                FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 230, 230)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(value);
            Grid.SetColumn(sp, col);
            grid.Children.Add(sp);
        }

        private static SolidColorBrush Br(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
