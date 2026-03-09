using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

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
        ["ThemeControlHighColor"] = Color.Parse("#3E6C45"),
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
        ["ThemeListAltRowColor"] = Color.Parse("#26262C")
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

    public static void ApplyFont(Application app, string? descriptor)
    {
        var normalized = NormalizeFont(descriptor);
        try
        {
            app.Resources["ThemeFontFamily"] = new FontFamily(normalized);
        }
        catch
        {
            app.Resources["ThemeFontFamily"] = new FontFamily(DefaultFontDescriptor);
        }
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
