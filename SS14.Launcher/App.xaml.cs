using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
#if DEBUG
using HotAvalonia;
#endif
using JetBrains.Annotations;
using Serilog;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.OverrideAssets;
using SS14.Launcher.Theme;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels;
using SS14.Launcher.Views;

namespace SS14.Launcher;

public class App : Application
{
    private static readonly Dictionary<string, AssetDef> AssetDefs = new()
    {
        ["WindowIcon"] = new AssetDef("icon.ico", AssetType.WindowIcon),
        ["LogoLong"] = new AssetDef("logo-long.png", AssetType.Bitmap),
    };

    private readonly OverrideAssetsManager _overrideAssets;

    private readonly Dictionary<string, object> _baseAssets = new();

    // XAML insists on a parameterless constructor existing, despite this never being used.
    [UsedImplicitly]
    public App()
    {
        throw new InvalidOperationException();
    }

    public App(OverrideAssetsManager overrideAssets)
    {
        _overrideAssets = overrideAssets;
    }

    public override void Initialize()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("HOTAVALONIA") == "1")
            this.EnableHotReload();
#endif
        AvaloniaXamlLoader.Load(this);

        LoadBaseAssets();
        IconsLoader.Load(this);

        _overrideAssets.AssetsChanged += OnAssetsChanged;
    }

    private void LoadBaseAssets()
    {
        bool randHeader = IsRandHeaderEnabled();

        foreach (var (name, (path, type)) in AssetDefs)
        {
            Uri assetUri = name == "LogoLong"
                ? ResolveLogoUri(randHeader)
                : new Uri($"avares://SS14.Launcher/Assets/{path}");

            using Stream dataStream = AssetLoader.Open(assetUri);
            object asset = LoadAsset(type, dataStream);

            _baseAssets.Add(name, asset);
            Resources.Add(name, asset);
        }
    }

    public void RefreshLogoLong(bool randomize)
    {
        if (!AssetDefs.TryGetValue("LogoLong", out var def))
            return;

        var assetUri = ResolveLogoUri(randomize);
        using Stream dataStream = AssetLoader.Open(assetUri);
        object asset = LoadAsset(def.Type, dataStream);

        _baseAssets["LogoLong"] = asset;
        Resources["LogoLong"] = asset;
    }

    private static bool IsRandHeaderEnabled()
    {
        try
        {
            var cfg = Locator.Current.GetRequiredService<DataManager>();
            return cfg.GetCVar(CVars.RandHeader);
        }
        catch
        {
            return true;
        }
    }

    private static Uri ResolveLogoUri(bool randomize)
    {
        if (!randomize)
            return new Uri("avares://SS14.Launcher/Assets/logo-long.png");

        var logos = new List<Uri>(AssetLoader.GetAssets(new Uri("avares://SS14.Launcher/Assets/logos"), null));
        if (logos.Count == 0)
            return new Uri("avares://SS14.Launcher/Assets/logo-long.png");

        var randomIndex = Random.Shared.Next(logos.Count);
        return logos[randomIndex];
    }

    private void OnAssetsChanged(OverrideAssetsChanged obj)
    {
        foreach (var (name, data) in obj.Files)
        {
            if (!AssetDefs.TryGetValue(name, out var def))
            {
                Log.Warning("Unable to find asset def for asset: '{AssetName}'", name);
                continue;
            }

            var ms = new MemoryStream(data, writable: false);
            var asset = LoadAsset(def.Type, ms);

            Resources[name] = asset;
        }

        // Clear assets not given to base data.
        foreach (var (name, asset) in _baseAssets)
        {
            if (!obj.Files.ContainsKey(name))
                Resources[name] = asset;
        }
    }

    private static object LoadAsset(AssetType type, Stream data)
    {
        return type switch
        {
            AssetType.Bitmap => new Bitmap(data),
            AssetType.WindowIcon => new WindowIcon(data),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private sealed record AssetDef(string DefaultPath, AssetType Type);

    private enum AssetType
    {
        Bitmap,
        WindowIcon
    }

    // Called when Avalonia init is done
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Startup += OnStartup;
            desktop.Exit += OnExit;
        }
    }

    private void OnStartup(object? s, ControlledApplicationLifetimeStartupEventArgs e)
    {
        var loc = Locator.Current.GetRequiredService<LocalizationManager>();
        var cfg = Locator.Current.GetRequiredService<DataManager>();
        var msgr = Locator.Current.GetRequiredService<LauncherMessaging>();
        var contentManager = Locator.Current.GetRequiredService<ContentManager>();
        var overrideAssets = Locator.Current.GetRequiredService<OverrideAssetsManager>();
        var launcherInfo = Locator.Current.GetRequiredService<LauncherInfoManager>();

        AppThemeManager.ApplyTheme(
            this,
            AppThemeManager.Normalize(cfg.GetCVar(CVars.Theme)),
            cfg.GetCVar(CVars.ThemeGradient),
            cfg.GetCVar(CVars.ThemeDecor),
            ReadCustomThemeColors(cfg));
        AppThemeManager.ApplyFont(this, cfg.GetCVar(CVars.ThemeFont));
        loc.Initialize();
        launcherInfo.Initialize();
        contentManager.Initialize();
        overrideAssets.Initialize();

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow
        {
            DataContext = viewModel
        };

        loc.LanguageSwitched += () =>
        {
            window.ReloadContent();

            // Reloading content isn't a smooth process anyway, so let's do some housekeeping while we're at it.
            GC.Collect();
        };

        var lc = new LauncherCommands(viewModel);
        lc.RunCommandTask();
        Locator.CurrentMutable.RegisterConstant(lc);
        msgr.StartServerTask(lc);

        window.Show();
        viewModel.OnWindowInitialized();

        AppThemeManager.ApplyTheme(
            this,
            AppThemeManager.Normalize(cfg.GetCVar(CVars.Theme)),
            cfg.GetCVar(CVars.ThemeGradient),
            cfg.GetCVar(CVars.ThemeDecor),
            ReadCustomThemeColors(cfg));
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var msgr = Locator.Current.GetRequiredService<LauncherMessaging>();
        msgr.StopAndWait();
    }

    private static AppThemeManager.CustomThemeColors ReadCustomThemeColors(DataManager cfg)
    {
        return new AppThemeManager.CustomThemeColors(
            ParseColorOrDefault(cfg.GetCVar(CVars.ThemeCustomBackground), "#25252A"),
            ParseColorOrDefault(cfg.GetCVar(CVars.ThemeCustomAccent), "#3E6C45"),
            ParseColorOrDefault(cfg.GetCVar(CVars.ThemeCustomForeground), "#EEEEEE"),
            ParseColorOrDefault(cfg.GetCVar(CVars.ThemeCustomPopup), "#202025"),
            ParseColorOrDefault(cfg.GetCVar(CVars.ThemeCustomGradientStart), "#25252A"),
            ParseColorOrDefault(cfg.GetCVar(CVars.ThemeCustomGradientEnd), "#2E3746"));
    }

    private static Color ParseColorOrDefault(string? raw, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                return Color.Parse(raw);
            }
            catch
            {
            }
        }

        return Color.Parse(fallback);
    }
}
