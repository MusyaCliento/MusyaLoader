using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia;

namespace SS14.Launcher.Converters
{
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Avalonia.Media.FontWeight.Bold;
            return Avalonia.Media.FontWeight.Normal;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
