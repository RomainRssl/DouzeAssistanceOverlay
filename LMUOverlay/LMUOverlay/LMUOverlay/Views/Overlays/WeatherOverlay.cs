using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
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
            main.Children.Add(new Border { Height = 1, Background = BrushCache.Get(ThemeManager.Current.Border), Margin = new Thickness(0, 4, 0, 4) });
            main.Children.Add(new TextBlock
            {
                Text = "PRÉVISIONS", FontSize = 8, FontWeight = FontWeights.SemiBold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });

            // Forecast text
            _forecastText = new TextBlock
            {
                FontSize = 12, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            main.Children.Add(_forecastText);

            // Trend arrows row
            var trendGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            trendGrid.ColumnDefinitions.Add(new ColumnDefinition());
            trendGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var rainTrendPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal };
            rainTrendPanel.Children.Add(new TextBlock { Text = "Pluie ", FontSize = 8, FontFamily = OverlayHelper.FontConsolas, Foreground = BrushCache.Get(ThemeManager.Current.TextMuted), VerticalAlignment = VerticalAlignment.Center });
            _rainTrendArrow = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, VerticalAlignment = VerticalAlignment.Center };
            rainTrendPanel.Children.Add(_rainTrendArrow);
            Grid.SetColumn(rainTrendPanel, 0);
            trendGrid.Children.Add(rainTrendPanel);

            var cloudTrendPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal };
            cloudTrendPanel.Children.Add(new TextBlock { Text = "Nuages ", FontSize = 8, FontFamily = OverlayHelper.FontConsolas, Foreground = BrushCache.Get(ThemeManager.Current.TextMuted), VerticalAlignment = VerticalAlignment.Center });
            _cloudTrendArrow = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = OverlayHelper.FontConsolas, VerticalAlignment = VerticalAlignment.Center };
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
            var tm = ThemeManager.Current;

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
            if (w.Raining > 0.5) { iColor = tm.ClassLmp2; icon = "🌧"; }
            else if (w.Raining > 0.1) { iColor = Color.FromArgb(180, tm.ClassLmp2.R, tm.ClassLmp2.G, tm.ClassLmp2.B); icon = "🌦"; }
            else if (w.CloudCover > 0.5) { iColor = tm.TextMuted; icon = "☁"; }
            else { iColor = tm.StateWarn; icon = "☀"; }

            _rainIndicator.Background = BrushCache.Get(Color.FromArgb(40, iColor.R, iColor.G, iColor.B));
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
                    ? tm.ClassLmp2
                    : w.ForecastText.Contains("Sec") || w.ForecastText.Contains("Dégagé")
                        ? tm.StateGood
                        : tm.TextPrimary;
                _forecastText.Foreground = BrushCache.Get(fColor);
            }
            else
            {
                _forecastText.Text = "En analyse...";
                _forecastText.Foreground = BrushCache.Get(tm.TextMuted);
            }

            // Trend arrows
            SetTrendArrow(_rainTrendArrow, w.RainTrend, 0.02, tm);
            SetTrendArrow(_cloudTrendArrow, w.CloudTrend, 0.03, tm);
        }

        private static void SetTrendArrow(TextBlock tb, double trend, double threshold, ThemeManager tm)
        {
            if (trend > threshold)
            {
                tb.Text = "▲";
                tb.Foreground = BrushCache.Get(tm.StateDanger);
            }
            else if (trend < -threshold)
            {
                tb.Text = "▼";
                tb.Foreground = BrushCache.Get(tm.StateGood);
            }
            else
            {
                tb.Text = "—";
                tb.Foreground = BrushCache.Get(tm.TextMuted);
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
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            value = new TextBlock
            {
                FontSize = 14, FontWeight = FontWeights.Bold,
                FontFamily = OverlayHelper.FontConsolas,
                Foreground = BrushCache.Get(ThemeManager.Current.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(value);
            Grid.SetColumn(sp, col);
            grid.Children.Add(sp);
        }
    }
}
