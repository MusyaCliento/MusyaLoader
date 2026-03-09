using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace SS14.Launcher.Converters
{
    public class PathToFileNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var path = value as string;
            return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
