using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;

namespace SS14.Launcher.Converters;

public sealed class IconPathToImageConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, IImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IImage image)
            return image;

        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        if (Cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            IImage loaded;
            if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = AssetLoader.Open(new Uri(path));
                loaded = new Bitmap(stream);
            }
            else
            {
                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                    return null;

                using var stream = File.OpenRead(fullPath);
                loaded = new Bitmap(stream);
            }

            Cache[path] = loaded;
            return loaded;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to convert icon path to image: {Path}", path);
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
