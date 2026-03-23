using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LMUOverlay.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility.Visible;
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? $"{d * 100:F0}%" : "0%";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ScaleToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? $"{d * 100:F0}%" : "100%";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class TemperatureToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double temp) return Brushes.Gray;

            // Tire temperature color mapping (Celsius)
            return temp switch
            {
                < 60 => new SolidColorBrush(Color.FromRgb(66, 135, 245)),    // Cold - Blue
                < 80 => new SolidColorBrush(Color.FromRgb(66, 245, 135)),    // Warming - Green
                < 100 => new SolidColorBrush(Color.FromRgb(76, 217, 100)),   // Optimal - Green
                < 115 => new SolidColorBrush(Color.FromRgb(255, 204, 0)),    // Hot - Yellow
                < 130 => new SolidColorBrush(Color.FromRgb(255, 149, 0)),    // Very Hot - Orange
                _ => new SolidColorBrush(Color.FromRgb(255, 59, 48))         // Overheating - Red
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class WearToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double wear) return Brushes.Gray;

            // Wear: 1.0 = new, 0.0 = worn out
            return wear switch
            {
                > 0.7 => new SolidColorBrush(Color.FromRgb(76, 217, 100)),   // Good
                > 0.4 => new SolidColorBrush(Color.FromRgb(255, 204, 0)),    // Medium
                > 0.2 => new SolidColorBrush(Color.FromRgb(255, 149, 0)),    // Low
                _ => new SolidColorBrush(Color.FromRgb(255, 59, 48))         // Critical
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class TimeSpanFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double seconds || seconds <= 0) return "--:--.---";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.Minutes > 0
                ? $"{ts.Minutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}"
                : $"{ts.Seconds}.{ts.Milliseconds:D3}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class GapFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double gap) return "-";
            return gap > 0 ? $"+{gap:F3}" : $"{gap:F3}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToConnectionStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? "Connecté" : "Déconnecté";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToConnectionColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true
                ? new SolidColorBrush(Color.FromRgb(76, 217, 100))
                : new SolidColorBrush(Color.FromRgb(255, 59, 48));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
