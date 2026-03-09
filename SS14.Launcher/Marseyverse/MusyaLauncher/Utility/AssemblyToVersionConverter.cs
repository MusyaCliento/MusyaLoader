using System;
using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;

namespace SS14.Launcher.Converters
{
    public class AssemblyToVersionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Assembly asm)
            {
                return asm.GetName().Version?.ToString() ?? "n/a";
            }
            return "n/a";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
    }
}
