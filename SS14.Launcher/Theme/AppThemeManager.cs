using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using Avalonia;
using Avalonia.Media;
using Serilog;

namespace SS14.Launcher.Theme;

public enum LauncherTheme
{
    Dark = 0,
    Light = 1,
    DarkRed = 2,
    DarkPurple = 3,
    MidnightBlue = 4,
    EmeraldDusk = 5,
    CopperNight = 6,
    Custom = 7
}

public static class AppThemeManager
{
    public sealed record CustomThemeColors(
        Color Background,
        Color Accent,
        Color Foreground,
        Color PopupBackground,
        Color GradientStart,
        Color GradientEnd);

    public const string DefaultFontDescriptor = "avares://SS14.Launcher/Assets/Fonts/noto_sans/*.ttf#Noto Sans";
    private static readonly IReadOnlyDictionary<string, Color> DarkColors = new Dictionary<string, Color>
    {
        ["ThemeBackgroundColor"] = Color.Parse("#25252A"),
        ["ThemePopupBackgroundColor"] = Color.Parse("#202025"),
        ["ThemeForegroundColor"] = Color.Parse("#EEEEEE"),
        ["ThemeForegroundMutedColor"] = Color.Parse("#666666"),
        ["ThemeControlMidColor"] = Color.Parse("#464966"),
        ["ThemeControlHighColor"] = Color.Parse("#525259"),
        ["ThemeNanoGoldColor"] = Color.Parse("#ADA24B"),
        ["ThemeSubTextColor"] = Color.Parse("#AAAAAA"),
        ["ThemeStripebackEdgeColor"] = Color.Parse("#525252"),
        ["ThemeButtonHoveredColor"] = Color.Parse("#575B7F"),
        ["ThemeTabItemSelectedColor"] = Color.Parse("#CC3E6C45"),
        ["ThemeTabItemHoveredColor"] = Color.Parse("#CC575B7F"),
        ["ThemeListSeparatorColor"] = Color.Parse("#AA575B7F"),
        ["ThemeListSeparatorColorTransparent"] = Color.Parse("#00575B7F"),
        ["ThemeBorderMidColor"] = Color.Parse("#6B6F8F"),
        ["ThemeBorderHighColor"] = Color.Parse("#8E94B8"),
        ["ThemeListAltRowColor"] = Color.Parse("#26262C"),
        ["SenseLineHorizontalColor"] = Color.Parse("#ADA24B"),
        ["ThemeScrollViewerSepColor"] = Color.Parse("#2E2E35")
    };

    private static readonly IReadOnlyDictionary<string, Color> LightColors = new Dictionary<string, Color>
    {
        ["ThemeBackgroundColor"] = Color.Parse("#F6F8FC"),
        ["ThemePopupBackgroundColor"] = Color.Parse("#FFFFFF"),
        ["ThemeForegroundColor"] = Color.Parse("#1B2230"),
        ["ThemeForegroundMutedColor"] = Color.Parse("#6F7A8D"),
        ["ThemeControlMidColor"] = Color.Parse("#E6ECF5"),
        ["ThemeControlHighColor"] = Color.Parse("#2E7A66"),
        ["ThemeNanoGoldColor"] = Color.Parse("#2E7A66"),
        ["ThemeSubTextColor"] = Color.Parse("#6B778A"),
        ["ThemeStripebackEdgeColor"] = Color.Parse("#D8E1EE"),
        ["ThemeButtonHoveredColor"] = Color.Parse("#DCE5F2"),
        ["ThemeTabItemSelectedColor"] = Color.Parse("#C02E7A66"),
        ["ThemeTabItemHoveredColor"] = Color.Parse("#AADCE5F2"),
        ["ThemeListSeparatorColor"] = Color.Parse("#AABAC9DF"),
        ["ThemeListSeparatorColorTransparent"] = Color.Parse("#00BAC9DF"),
        ["ThemeBorderMidColor"] = Color.Parse("#B7C4D9"),
        ["ThemeBorderHighColor"] = Color.Parse("#9CAECC"),
        ["ThemeListAltRowColor"] = Color.Parse("#EEF3FA")
    };

    private static readonly IReadOnlyDictionary<string, Color> DarkRedColors = new Dictionary<string, Color>
    {
        ["ThemeBackgroundColor"] = Color.Parse("#1E1719"),
        ["ThemePopupBackgroundColor"] = Color.Parse("#231A1D"),
        ["ThemeForegroundColor"] = Color.Parse("#F0E8EA"),
        ["ThemeForegroundMutedColor"] = Color.Parse("#A49297"),
        ["ThemeControlMidColor"] = Color.Parse("#4A3136"),
        ["ThemeControlHighColor"] = Color.Parse("#8A3B48"),
        ["ThemeNanoGoldColor"] = Color.Parse("#D06B4D"),
        ["ThemeSubTextColor"] = Color.Parse("#B9A5AA"),
        ["ThemeStripebackEdgeColor"] = Color.Parse("#5C3C43"),
        ["ThemeButtonHoveredColor"] = Color.Parse("#66444B"),
        ["ThemeTabItemSelectedColor"] = Color.Parse("#CC8A3B48"),
        ["ThemeTabItemHoveredColor"] = Color.Parse("#AA66444B"),
        ["ThemeListSeparatorColor"] = Color.Parse("#AA775059"),
        ["ThemeListSeparatorColorTransparent"] = Color.Parse("#00775059"),
        ["ThemeBorderMidColor"] = Color.Parse("#7B565F"),
        ["ThemeBorderHighColor"] = Color.Parse("#9A6A76"),
        ["ThemeListAltRowColor"] = Color.Parse("#241B1E")
    };

    private static readonly IReadOnlyDictionary<string, Color> DarkPurpleColors = new Dictionary<string, Color>
    {
        ["ThemeBackgroundColor"] = Color.Parse("#1C1824"),
        ["ThemePopupBackgroundColor"] = Color.Parse("#211C2C"),
        ["ThemeForegroundColor"] = Color.Parse("#EEE9F7"),
        ["ThemeForegroundMutedColor"] = Color.Parse("#9E93B3"),
        ["ThemeControlMidColor"] = Color.Parse("#41355A"),
        ["ThemeControlHighColor"] = Color.Parse("#6F4EA1"),
        ["ThemeNanoGoldColor"] = Color.Parse("#B486D8"),
        ["ThemeSubTextColor"] = Color.Parse("#B9AFCD"),
        ["ThemeStripebackEdgeColor"] = Color.Parse("#544872"),
        ["ThemeButtonHoveredColor"] = Color.Parse("#5B4D79"),
        ["ThemeTabItemSelectedColor"] = Color.Parse("#CC6F4EA1"),
        ["ThemeTabItemHoveredColor"] = Color.Parse("#AA5B4D79"),
        ["ThemeListSeparatorColor"] = Color.Parse("#AA6B5A8E"),
        ["ThemeListSeparatorColorTransparent"] = Color.Parse("#006B5A8E"),
        ["ThemeBorderMidColor"] = Color.Parse("#75659C"),
        ["ThemeBorderHighColor"] = Color.Parse("#9383BC"),
        ["ThemeListAltRowColor"] = Color.Parse("#231E2F")
    };

    private static readonly IReadOnlyDictionary<string, Color> MidnightBlueColors = new Dictionary<string, Color>
    {
        ["ThemeBackgroundColor"] = Color.Parse("#141B24"),
        ["ThemePopupBackgroundColor"] = Color.Parse("#18212C"),
        ["ThemeForegroundColor"] = Color.Parse("#E9F1F9"),
        ["ThemeForegroundMutedColor"] = Color.Parse("#91A4B8"),
        ["ThemeControlMidColor"] = Color.Parse("#2C4158"),
        ["ThemeControlHighColor"] = Color.Parse("#3C7FA6"),
        ["ThemeNanoGoldColor"] = Color.Parse("#65B0C8"),
        ["ThemeSubTextColor"] = Color.Parse("#A9BED1"),
        ["ThemeStripebackEdgeColor"] = Color.Parse("#3C536B"),
        ["ThemeButtonHoveredColor"] = Color.Parse("#35506A"),
        ["ThemeTabItemSelectedColor"] = Color.Parse("#CC3C7FA6"),
        ["ThemeTabItemHoveredColor"] = Color.Parse("#AA35506A"),
        ["ThemeListSeparatorColor"] = Color.Parse("#AA44617D"),
        ["ThemeListSeparatorColorTransparent"] = Color.Parse("#0044617D"),
        ["ThemeBorderMidColor"] = Color.Parse("#4D6986"),
        ["ThemeBorderHighColor"] = Color.Parse("#6585A7"),
        ["ThemeListAltRowColor"] = Color.Parse("#17202B")
    };

    private static readonly IReadOnlyDictionary<string, Color> EmeraldDuskColors = new Dictionary<string, Color>
    {
        ["ThemeBackgroundColor"] = Color.Parse("#12211D"),
        ["ThemePopupBackgroundColor"] = Color.Parse("#162A25"),
        ["ThemeForegroundColor"] = Color.Parse("#E7F6F0"),
        ["ThemeForegroundMutedColor"] = Color.Parse("#95B3A8"),
        ["ThemeControlMidColor"] = Color.Parse("#27473E"),
        ["ThemeControlHighColor"] = Color.Parse("#2F8F73"),
        ["ThemeNanoGoldColor"] = Color.Parse("#78C9A9"),
        ["ThemeSubTextColor"] = Color.Parse("#A8C6BA"),
        ["ThemeStripebackEdgeColor"] = Color.Parse("#356154"),
        ["ThemeButtonHoveredColor"] = Color.Parse("#2E574B"),
        ["ThemeTabItemSelectedColor"] = Color.Parse("#CC2F8F73"),
        ["ThemeTabItemHoveredColor"] = Color.Parse("#AA2E574B"),
        ["ThemeListSeparatorColor"] = Color.Parse("#AA3A6D5E"),
        ["ThemeListSeparatorColorTransparent"] = Color.Parse("#003A6D5E"),
        ["ThemeBorderMidColor"] = Color.Parse("#487A6A"),
        ["ThemeBorderHighColor"] = Color.Parse("#5D9D88"),
        ["ThemeListAltRowColor"] = Color.Parse("#172C26")
    };

    private static readonly IReadOnlyDictionary<string, Color> CopperNightColors = new Dictionary<string, Color>
    {
        ["ThemeBackgroundColor"] = Color.Parse("#211915"),
        ["ThemePopupBackgroundColor"] = Color.Parse("#2A201A"),
        ["ThemeForegroundColor"] = Color.Parse("#F6ECE5"),
        ["ThemeForegroundMutedColor"] = Color.Parse("#B5A197"),
        ["ThemeControlMidColor"] = Color.Parse("#5B4033"),
        ["ThemeControlHighColor"] = Color.Parse("#B06A45"),
        ["ThemeNanoGoldColor"] = Color.Parse("#D79A62"),
        ["ThemeSubTextColor"] = Color.Parse("#CAB4A8"),
        ["ThemeStripebackEdgeColor"] = Color.Parse("#775242"),
        ["ThemeButtonHoveredColor"] = Color.Parse("#724D3D"),
        ["ThemeTabItemSelectedColor"] = Color.Parse("#CCB06A45"),
        ["ThemeTabItemHoveredColor"] = Color.Parse("#AA724D3D"),
        ["ThemeListSeparatorColor"] = Color.Parse("#AA8A5E49"),
        ["ThemeListSeparatorColorTransparent"] = Color.Parse("#008A5E49"),
        ["ThemeBorderMidColor"] = Color.Parse("#936551"),
        ["ThemeBorderHighColor"] = Color.Parse("#B07E67"),
        ["ThemeListAltRowColor"] = Color.Parse("#2B211C")
    };

    public static LauncherTheme Normalize(int cvarValue)
    {
        return Enum.IsDefined(typeof(LauncherTheme), cvarValue)
            ? (LauncherTheme)cvarValue
            : LauncherTheme.Dark;
    }

    public static void ApplyTheme(
        Application app,
        LauncherTheme theme,
        bool gradientEnabled = true,
        bool decorEnabled = true,
        CustomThemeColors? customColors = null)
    {
        var colors = GetThemeColors(theme, customColors);

        foreach (var (key, color) in colors)
        {
            app.Resources[key] = color;
        }

        SetBrush(app, "ThemeBackgroundBrush", colors["ThemeBackgroundColor"]);
        SetBrush(app, "ThemePopupBackgroundBrush", colors["ThemePopupBackgroundColor"]);
        SetBrush(app, "ThemeForegroundBrush", colors["ThemeForegroundColor"]);
        SetBrush(app, "ThemeForegroundMutedBrush", colors["ThemeForegroundMutedColor"]);
        SetBrush(app, "ThemeControlMidBrush", colors["ThemeControlMidColor"]);
        SetBrush(app, "ThemeControlHighBrush", colors["ThemeControlHighColor"]);
        SetBrush(app, "ThemeNanoGoldBrush", colors["ThemeNanoGoldColor"]);
        SetBrush(app, "ThemeSubTextBrush", colors["ThemeSubTextColor"]);
        SetBrush(app, "ThemeButtonHoveredBrush", colors["ThemeButtonHoveredColor"]);
        SetBrush(app, "ThemeStripebackEdgeBrush", colors["ThemeStripebackEdgeColor"]);
        SetBrush(app, "ThemeTabItemSelectedBrush", colors["ThemeTabItemSelectedColor"]);
        SetBrush(app, "ThemeTabItemHoveredBrush", colors["ThemeTabItemHoveredColor"]);
        SetBrush(app, "ThemeBorderMidBrush", colors["ThemeBorderMidColor"]);
        SetBrush(app, "ThemeBorderHighBrush", colors["ThemeBorderHighColor"]);
        SetBrush(app, "ThemeListAltRowBrush", colors["ThemeListAltRowColor"]);

        var background = colors["ThemeBackgroundColor"];
        var popup = colors["ThemePopupBackgroundColor"];
        var controlHigh = colors["ThemeControlHighColor"];
        var gradientStart = theme == LauncherTheme.Light
            ? Mix(background, Color.Parse("#FFFFFF"), 0.35)
            : Mix(background, popup, 0.18);
        var gradientEnd = theme == LauncherTheme.Light
            ? Mix(background, controlHigh, 0.14)
            : Mix(background, controlHigh, 0.28);

        if (theme == LauncherTheme.Custom && customColors != null)
        {
            gradientStart = Opaque(customColors.GradientStart);
            gradientEnd = Opaque(customColors.GradientEnd);
        }

        if (gradientEnabled)
        {
            var gradientMid = Mix(Mix(gradientStart, gradientEnd, 0.5), popup, theme == LauncherTheme.Custom ? 0.12 : 0.20);
            app.Resources["ThemeBackgroundBrush"] = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(gradientStart, 0.0),
                    new GradientStop(gradientMid, 0.48),
                    new GradientStop(gradientEnd, 1.0)
                }
            };
        }
        else
        {
            SetBrush(app, "ThemeBackgroundBrush", background);
        }

        app.Resources["ThemeStripeBackBrush"] = decorEnabled
            ? CreateStripeBrush(background, colors["ThemeListAltRowColor"])
            : new SolidColorBrush(background);

        app.Resources["WindowOverlayBrush"] = new SolidColorBrush(theme == LauncherTheme.Light
            ? Color.Parse("#66000000")
            : Color.Parse("#AA000000"));
    }

    public static string? LastAppliedFontDescriptor { get; private set; }
    public static bool LastApplyUsedFallback { get; private set; }
    public static string? LastFontErrorMessage { get; private set; }
    public static bool LastFontInstalledForUser { get; private set; }
    public static string? LastFontInstalledFamily { get; private set; }
    private static readonly HashSet<string> FailedFontDescriptors = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PrivateFontFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SessionFontFiles = new(StringComparer.OrdinalIgnoreCase);

    public static void ClearFontFailureCache(string? descriptor = null)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            FailedFontDescriptors.Clear();
            return;
        }

        FailedFontDescriptors.Remove(descriptor);
        if (TryGetFamilyFromDescriptor(descriptor, out var family))
            FailedFontDescriptors.Remove(family);
    }

    public static bool ApplyFont(Application app, string? descriptor)
    {
        var normalized = NormalizeFont(descriptor);
        LastApplyUsedFallback = false;
        LastFontErrorMessage = null;
        LastFontInstalledForUser = false;
        LastFontInstalledFamily = null;

        if (TryApplyFontCached(app, normalized, out var firstError))
        {
            LastAppliedFontDescriptor = normalized;
            return true;
        }

        if (TryFixFileFontDescriptor(normalized, out var fixedDescriptor, out var familyFromFile))
        {
            if (!string.Equals(fixedDescriptor, normalized, StringComparison.OrdinalIgnoreCase) &&
                TryApplyFontCached(app, fixedDescriptor, out _))
            {
                LastAppliedFontDescriptor = fixedDescriptor;
                LastApplyUsedFallback = true;
                return true;
            }

            // If the font is installed system-wide but the file loading fails, try family name only.
            if (!string.IsNullOrWhiteSpace(familyFromFile))
            {
                if (TryApplyFontCached(app, familyFromFile, out _))
                {
                    LastAppliedFontDescriptor = familyFromFile;
                    LastApplyUsedFallback = true;
                    return true;
                }

                if (TryGetFilePathFromDescriptor(fixedDescriptor, out var filePath))
                {
                    if ((TryRegisterPrivateFontWindows(filePath) || TryRegisterSessionFontWindows(filePath)) &&
                        TryApplyFontCached(app, familyFromFile, out _))
                    {
                        LastAppliedFontDescriptor = familyFromFile;
                        LastApplyUsedFallback = true;
                        return true;
                    }
                }
            }
        }

        if (TryGetFamilyFromDescriptor(normalized, out var familyFromDescriptor) &&
            TryApplyFontCached(app, familyFromDescriptor, out _))
        {
            LastAppliedFontDescriptor = familyFromDescriptor;
            LastApplyUsedFallback = true;
            return true;
        }

        if (TryGetFamilyFromDescriptor(normalized, out var fallbackFamily) &&
            TryGetFilePathFromDescriptor(normalized, out var fallbackPath) &&
            (TryRegisterPrivateFontWindows(fallbackPath) || TryRegisterSessionFontWindows(fallbackPath)) &&
            TryApplyFontCached(app, fallbackFamily, out _))
        {
            LastAppliedFontDescriptor = fallbackFamily;
            LastApplyUsedFallback = true;
            return true;
        }

        if (TryGetFilePathFromDescriptor(normalized, out var installPath) &&
            TryInstallFontForCurrentUser(installPath, out var installedFamily, out _))
        {
            if (TryApplyFontCached(app, installedFamily, out _))
            {
                LastAppliedFontDescriptor = installedFamily;
                LastApplyUsedFallback = true;
                LastFontInstalledForUser = true;
                LastFontInstalledFamily = installedFamily;
                return true;
            }
        }

        if (firstError != null)
        {
            LastFontErrorMessage = firstError.Message;
            Log.Warning(firstError, "Failed to apply font '{Font}'. Falling back to default.", normalized);
        }
        else
            Log.Warning("Failed to apply font '{Font}'. Falling back to default.", normalized);
        _ = TryApplyFont(app, DefaultFontDescriptor, out _);
        LastAppliedFontDescriptor = DefaultFontDescriptor;
        LastApplyUsedFallback = true;
        return false;
    }

    public static bool TryBuildDescriptorFromFile(string filePath, out string descriptor)
    {
        descriptor = "";
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            var uri = new Uri(filePath).AbsoluteUri;
            if (!TryReadFontFamilyName(filePath, out var family))
                return false;

            descriptor = $"{uri}#{family}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryApplyFontCached(Application app, string descriptor, out Exception? error)
    {
        error = null;
        if (FailedFontDescriptors.Contains(descriptor))
            return false;

        var ok = TryApplyFont(app, descriptor, out error);
        if (!ok)
            FailedFontDescriptors.Add(descriptor);
        return ok;
    }

    private static bool TryApplyFont(Application app, string descriptor, out Exception? error)
    {
        error = null;
        try
        {
            var family = new FontFamily(descriptor);
            var typeface = new Typeface(family);
            _ = typeface.GlyphTypeface;
            app.Resources["ThemeFontFamily"] = family;
            return true;
        }
        catch (Exception e)
        {
            error = e;
            return false;
        }
    }

    private static bool TryFixFileFontDescriptor(string descriptor, out string fixedDescriptor, out string familyName)
    {
        fixedDescriptor = "";
        familyName = "";
        if (!Uri.TryCreate(descriptor, UriKind.Absolute, out var uri))
            return false;

        if (!uri.IsFile)
            return false;

        var path = uri.LocalPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        if (!TryReadFontFamilyName(path, out var family))
            return false;

        familyName = family;
        var expected = $"{uri.GetLeftPart(UriPartial.Path)}#{family}";
        fixedDescriptor = expected;
        return true;
    }

    private static bool TryReadFontFamilyName(string filePath, out string familyName)
    {
        familyName = "";
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 12)
                return false;

            _ = ReadUInt32BE(reader); // sfnt version
            var numTables = ReadUInt16BE(reader);
            reader.ReadBytes(6); // searchRange, entrySelector, rangeShift

            uint nameOffset = 0;
            uint nameLength = 0;
            for (var i = 0; i < numTables; i++)
            {
                var tagBytes = reader.ReadBytes(4);
                if (tagBytes.Length < 4)
                    return false;

                var tag = Encoding.ASCII.GetString(tagBytes);
                _ = ReadUInt32BE(reader); // checksum
                var offset = ReadUInt32BE(reader);
                var length = ReadUInt32BE(reader);

                if (tag == "name")
                {
                    nameOffset = offset;
                    nameLength = length;
                }
            }

            if (nameOffset == 0 || nameLength == 0)
                return false;

            stream.Seek(nameOffset, SeekOrigin.Begin);
            _ = ReadUInt16BE(reader); // format
            var count = ReadUInt16BE(reader);
            var stringOffset = ReadUInt16BE(reader);

            var recordsStart = nameOffset + 6;
            var storageStart = nameOffset + stringOffset;

            string best = "";
            var bestScore = -1;

            for (var i = 0; i < count; i++)
            {
                stream.Seek(recordsStart + i * 12, SeekOrigin.Begin);
                var platformId = ReadUInt16BE(reader);
                var encodingId = ReadUInt16BE(reader);
                var languageId = ReadUInt16BE(reader);
                var nameId = ReadUInt16BE(reader);
                var length = ReadUInt16BE(reader);
                var offset = ReadUInt16BE(reader);

                if (nameId != 1 || length == 0)
                    continue;

                stream.Seek(storageStart + offset, SeekOrigin.Begin);
                var raw = reader.ReadBytes(length);
                if (raw.Length == 0)
                    continue;

                string value;
                if (platformId == 3)
                {
                    value = Encoding.BigEndianUnicode.GetString(raw);
                }
                else
                {
                    value = TryDecodeFallback(raw);
                }

                value = value.TrimEnd('\0').Trim();
                if (value.Length == 0)
                    continue;

                var score = 0;
                if (platformId == 3)
                    score += 10;
                if (platformId == 3 && languageId == 0x0409)
                    score += 5;
                if (encodingId == 1 || encodingId == 10)
                    score += 1;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = value;
                }
            }

            if (string.IsNullOrWhiteSpace(best))
                return false;

            familyName = best;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string TryDecodeFallback(byte[] raw)
    {
        try
        {
            return Encoding.UTF8.GetString(raw);
        }
        catch
        {
        }

        try
        {
            return Encoding.GetEncoding(28591).GetString(raw); // Latin-1
        }
        catch
        {
            return Encoding.ASCII.GetString(raw);
        }
    }

    private static ushort ReadUInt16BE(BinaryReader reader)
    {
        var b1 = reader.ReadByte();
        var b2 = reader.ReadByte();
        return (ushort)((b1 << 8) | b2);
    }

    private static uint ReadUInt32BE(BinaryReader reader)
    {
        var b1 = reader.ReadByte();
        var b2 = reader.ReadByte();
        var b3 = reader.ReadByte();
        var b4 = reader.ReadByte();
        return (uint)((b1 << 24) | (b2 << 16) | (b3 << 8) | b4);
    }

    private static bool TryGetFamilyFromDescriptor(string descriptor, out string familyName)
    {
        familyName = "";
        if (!Uri.TryCreate(descriptor, UriKind.Absolute, out var uri))
            return false;

        var fragment = uri.Fragment;
        if (string.IsNullOrWhiteSpace(fragment))
            return false;

        if (fragment.StartsWith("#", StringComparison.Ordinal))
            fragment = fragment[1..];

        familyName = Uri.UnescapeDataString(fragment).Trim();
        return !string.IsNullOrWhiteSpace(familyName);
    }

    private static bool TryGetFilePathFromDescriptor(string descriptor, out string filePath)
    {
        filePath = "";
        if (!Uri.TryCreate(descriptor, UriKind.Absolute, out var uri))
            return false;

        if (!uri.IsFile)
            return false;

        filePath = uri.LocalPath;
        return !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
    }

    public static void UnregisterPrivateFonts()
    {
        if (!OperatingSystem.IsWindows())
            return;

        foreach (var path in PrivateFontFiles)
        {
            _ = RemoveFontResourceEx(path, FR_PRIVATE, IntPtr.Zero);
        }

        PrivateFontFiles.Clear();

        foreach (var path in SessionFontFiles)
        {
            _ = RemoveFontResourceEx(path, 0, IntPtr.Zero);
        }

        SessionFontFiles.Clear();
    }

    private static bool TryRegisterPrivateFontWindows(string filePath)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        if (PrivateFontFiles.Contains(filePath))
            return true;

        var added = AddFontResourceEx(filePath, FR_PRIVATE, IntPtr.Zero);
        if (added > 0)
        {
            PrivateFontFiles.Add(filePath);
            return true;
        }

        return false;
    }

    private static bool TryRegisterSessionFontWindows(string filePath)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        if (SessionFontFiles.Contains(filePath))
            return true;

        var added = AddFontResourceEx(filePath, 0, IntPtr.Zero);
        if (added > 0)
        {
            SessionFontFiles.Add(filePath);
            BroadcastFontChange();
            return true;
        }

        return false;
    }

    private static void BroadcastFontChange()
    {
        const uint SMTO_ABORTIFHUNG = 0x0002;
        _ = SendMessageTimeout(new IntPtr(HWND_BROADCAST), WM_FONTCHANGE, UIntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 1000, out _);
    }

    private const uint FR_PRIVATE = 0x10;
    private const uint WM_FONTCHANGE = 0x001D;
    private const int HWND_BROADCAST = 0xFFFF;

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool RemoveFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr ShellExecute(IntPtr hwnd, string? lpOperation, string lpFile, string? lpParameters, string? lpDirectory, int nShowCmd);

    public static void OpenFontInstallUI(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            Log.Warning("Font install UI requested for {FontFile}", filePath);

            if (OperatingSystem.IsWindows())
            {
                if (TryReadFontFamilyName(filePath, out var family) &&
                    IsFontInstalledWindows(family, Path.GetFileName(filePath)))
                {
                    Log.Debug("Font already installed; skipping install UI for {FontFile}", filePath);
                    return;
                }

                if (TryShellExecute(filePath, "install"))
                    return;

                if (TryShellExecute(filePath, "open"))
                    return;

                var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var fontView = Path.Combine(systemDir, "fontview.exe");
                if (File.Exists(fontView) && TryStartProcess(new ProcessStartInfo
                    {
                        FileName = fontView,
                        ArgumentList = { filePath },
                        UseShellExecute = true
                    }, "fontview.exe"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        ArgumentList = { "shell32.dll,ShellExec_RunDLL", filePath },
                        UseShellExecute = true
                    }, "rundll32 ShellExec_RunDLL"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        ArgumentList = { "shell32.dll,OpenAs_RunDLL", filePath },
                        UseShellExecute = true
                    }, "rundll32 OpenAs_RunDLL"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = filePath,
                        Verb = "install",
                        UseShellExecute = true
                    }, "font install verb"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    }, "font open"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        ArgumentList = { "/select," + filePath },
                        UseShellExecute = true
                    }, "explorer open"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        ArgumentList = { "shell:fonts" },
                        UseShellExecute = true
                    }, "explorer shell:fonts"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        ArgumentList = { "/c", "start", "\"\"", "ms-settings:fonts" },
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }, "ms-settings fonts"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        ArgumentList = { "shell32.dll,Control_RunDLL", "fonts" },
                        UseShellExecute = true
                    }, "rundll32 fonts"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        ArgumentList = { filePath },
                        UseShellExecute = true
                    }, "explorer open file"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        ArgumentList = { "/c", "start", "\"\"", filePath },
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }, "cmd start"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "control.exe",
                        ArgumentList = { "/name", "Microsoft.Fonts" },
                        UseShellExecute = true
                    }, "fonts control panel"))
                    return;

                if (TryStartProcess(new ProcessStartInfo
                    {
                        FileName = "control.exe",
                        ArgumentList = { "fonts" },
                        UseShellExecute = true
                    }, "control fonts"))
                    return;

                Log.Warning("All font install UI attempts failed for {FontFile}", filePath);
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { filePath },
                    UseShellExecute = false
                });
                return;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { filePath },
                    UseShellExecute = false
                });
                return;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to open font install UI for {FontFile}", filePath);
        }
    }

    private static bool TryStartProcess(ProcessStartInfo psi, string label)
    {
        try
        {
            var p = Process.Start(psi);
            if (p != null)
            {
                Log.Debug("Started {Label} for font install UI.", label);
                return true;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to start {Label} for font install UI.", label);
        }

        return false;
    }

    private static bool TryShellExecute(string filePath, string verb)
    {
        try
        {
            var result = ShellExecute(IntPtr.Zero, verb, filePath, null, null, 1);
            if ((nint)result > 32)
            {
                Log.Debug("ShellExecute succeeded with verb '{Verb}' for {FontFile}", verb, filePath);
                return true;
            }

            Log.Warning("ShellExecute failed with code {Code} for verb '{Verb}' on {FontFile}", (nint)result, verb, filePath);
            return false;
        }
        catch (Exception e)
        {
            Log.Warning(e, "ShellExecute threw for verb '{Verb}' on {FontFile}", verb, filePath);
            return false;
        }
    }

    private static bool IsFontInstalledWindows(string familyName, string? fileName)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        if (string.IsNullOrWhiteSpace(familyName) && string.IsNullOrWhiteSpace(fileName))
            return false;

        if (IsFontInstalledInRegistry(Registry.CurrentUser, familyName, fileName))
            return true;

        return IsFontInstalledInRegistry(Registry.LocalMachine, familyName, fileName);
    }

    private static bool IsFontInstalledInRegistry(RegistryKey root, string familyName, string? fileName)
    {
        try
        {
            using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", false);
            if (key == null)
                return false;

            foreach (var valueName in key.GetValueNames())
            {
                if (!string.IsNullOrWhiteSpace(familyName) &&
                    valueName.Contains(familyName, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (fileName == null)
                    continue;

                var value = key.GetValue(valueName) as string;
                if (value != null &&
                    value.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryInstallFontForCurrentUser(string filePath, out string familyName, out string error)
    {
        familyName = "";
        error = "";

        if (!OperatingSystem.IsWindows())
        {
            error = "Platform is not Windows.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            error = "Font file not found.";
            return false;
        }

        if (!TryReadFontFamilyName(filePath, out familyName))
        {
            error = "Failed to read font family.";
            return false;
        }

        try
        {
            var fontsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            Directory.CreateDirectory(fontsDir);

            var ext = Path.GetExtension(filePath);
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var targetFileName = $"{baseName}{ext}";
            var targetPath = Path.Combine(fontsDir, targetFileName);

            if (File.Exists(targetPath))
            {
                if (!FilesEqual(filePath, targetPath))
                {
                    var hash = GetShortHash(filePath);
                    targetFileName = $"{baseName}-{hash}{ext}";
                    targetPath = Path.Combine(fontsDir, targetFileName);
                }
            }

            if (!File.Exists(targetPath))
                File.Copy(filePath, targetPath, overwrite: false);

            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");

            var valueName = $"{familyName} (TrueType)";
            key?.SetValue(valueName, targetFileName, Microsoft.Win32.RegistryValueKind.String);

            _ = AddFontResourceEx(targetPath, 0, IntPtr.Zero);
            BroadcastFontChange();
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private static bool FilesEqual(string a, string b)
    {
        var ai = new FileInfo(a);
        var bi = new FileInfo(b);
        if (ai.Length != bi.Length)
            return false;

        const int bufSize = 81920;
        var bufA = new byte[bufSize];
        var bufB = new byte[bufSize];
        using var fa = File.OpenRead(a);
        using var fb = File.OpenRead(b);
        while (true)
        {
            var ra = fa.Read(bufA, 0, bufSize);
            var rb = fb.Read(bufB, 0, bufSize);
            if (ra != rb)
                return false;
            if (ra == 0)
                return true;
            for (var i = 0; i < ra; i++)
            {
                if (bufA[i] != bufB[i])
                    return false;
            }
        }
    }

    private static string GetShortHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash.AsSpan(0, 4));
    }

    public static string NormalizeFont(string? descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
            return DefaultFontDescriptor;

        var normalized = descriptor.Trim();

        if (normalized.Equals("/Assets/Fonts/noto_sans/*.ttf#Noto Sans", StringComparison.Ordinal))
            return DefaultFontDescriptor;

        return normalized;
    }

    private static IReadOnlyDictionary<string, Color> GetThemeColors(LauncherTheme theme, CustomThemeColors? customColors)
    {
        return theme switch
        {
            LauncherTheme.Light => LightColors,
            LauncherTheme.DarkRed => DarkRedColors,
            LauncherTheme.DarkPurple => DarkPurpleColors,
            LauncherTheme.MidnightBlue => MidnightBlueColors,
            LauncherTheme.EmeraldDusk => EmeraldDuskColors,
            LauncherTheme.CopperNight => CopperNightColors,
            LauncherTheme.Custom when customColors != null => BuildCustomThemeColors(customColors),
            _ => DarkColors
        };
    }

    private static IReadOnlyDictionary<string, Color> BuildCustomThemeColors(CustomThemeColors custom)
    {
        var background = Opaque(custom.Background);
        var accent = Opaque(custom.Accent);
        var foreground = Opaque(custom.Foreground);
        var popup = Opaque(custom.PopupBackground);
        var controlMid = Mix(background, popup, 0.62);
        var controlHigh = Mix(controlMid, accent, 0.35);
        var hover = Mix(controlMid, accent, 0.18);
        var muted = Mix(foreground, background, 0.58);
        var subtext = Mix(foreground, background, 0.46);
        var stripeEdge = Mix(background, popup, 0.45);
        var borderMid = Mix(popup, foreground, 0.18);
        var borderHigh = Mix(controlHigh, foreground, 0.28);
        var listSeparator = WithAlpha(Mix(borderHigh, background, 0.40), 0xAA);
        var listSeparatorTransparent = WithAlpha(listSeparator, 0x00);
        var altRow = Mix(background, popup, 0.28);

        return new Dictionary<string, Color>
        {
            ["ThemeBackgroundColor"] = background,
            ["ThemePopupBackgroundColor"] = popup,
            ["ThemeForegroundColor"] = foreground,
            ["ThemeForegroundMutedColor"] = muted,
            ["ThemeControlMidColor"] = controlMid,
            ["ThemeControlHighColor"] = controlHigh,
            ["ThemeNanoGoldColor"] = accent,
            ["ThemeSubTextColor"] = subtext,
            ["ThemeStripebackEdgeColor"] = stripeEdge,
            ["ThemeButtonHoveredColor"] = hover,
            ["ThemeTabItemSelectedColor"] = WithAlpha(accent, 0xCC),
            ["ThemeTabItemHoveredColor"] = WithAlpha(Mix(controlMid, accent, 0.20), 0xAA),
            ["ThemeListSeparatorColor"] = listSeparator,
            ["ThemeListSeparatorColorTransparent"] = listSeparatorTransparent,
            ["ThemeBorderMidColor"] = borderMid,
            ["ThemeBorderHighColor"] = borderHigh,
            ["ThemeListAltRowColor"] = altRow
        };
    }

    private static void SetBrush(Application app, string key, Color color)
    {
        app.Resources[key] = new SolidColorBrush(color);
    }

    private static IBrush CreateStripeBrush(Color background, Color stripe)
    {
        return new VisualBrush
        {
            Visual = new Avalonia.Controls.Panel
            {
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(background),
                Children =
                {
                    new Avalonia.Controls.Shapes.Path
                    {
                        Data = Avalonia.Media.Geometry.Parse("M 0 8 L 24 32 L 8 32 L 0 24 Z"),
                        Fill = new SolidColorBrush(stripe)
                    },
                    new Avalonia.Controls.Shapes.Path
                    {
                        Data = Avalonia.Media.Geometry.Parse("M 8 0 L 24 0 L 32 8 L 32 24 Z"),
                        Fill = new SolidColorBrush(stripe)
                    }
                }
            },
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            SourceRect = new RelativeRect(0, 0, 32, 32, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, 32, 32, RelativeUnit.Absolute)
        };
    }

    private static Color Mix(Color a, Color b, double t)
    {
        var clamped = t < 0 ? 0 : t > 1 ? 1 : t;
        var r = (byte)(a.R + (b.R - a.R) * clamped);
        var g = (byte)(a.G + (b.G - a.G) * clamped);
        var bl = (byte)(a.B + (b.B - a.B) * clamped);
        return new Color(0xFF, r, g, bl);
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return new Color(alpha, color.R, color.G, color.B);
    }

    private static Color Opaque(Color color)
    {
        return new Color(0xFF, color.R, color.G, color.B);
    }
}
