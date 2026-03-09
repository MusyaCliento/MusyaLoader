using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SS14.Launcher.Converters
{
    public class BooleanToPreloadConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? "(preload)" : string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
