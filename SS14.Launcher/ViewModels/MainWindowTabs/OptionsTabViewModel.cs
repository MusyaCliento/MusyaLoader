using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using DynamicData;
using Marsey;
using Marsey.Config;
using Marsey.Game.Patches;
using Marsey.Misc;
using Marsey.Stealthsey;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Serilog;
using Splat;
using SS14.Launcher.Marseyverse;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Models;
using SS14.Launcher.Theme;
using SS14.Launcher.Utility;
using SS14.Launcher.Localization;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class OptionsTabViewModel : MainWindowTabViewModel, INotifyPropertyChanged
{
    public const int ProxySettingsTabIndex = 6;
    public sealed record ThemeFontOption(string Name, string Descriptor);
    public sealed record LauncherVersionFilterOption(LauncherVersionFilter Value, string Text);

    public enum LauncherVersionFilter
    {
        All,
        ReleaseOnly,
        PreReleaseOnly
    }

    private const string DefaultCustomThemeBackground = "#25252A";
    private const string DefaultCustomThemeAccent = "#3E6C45";
    private const string DefaultCustomThemeForeground = "#EEEEEE";
    private const string DefaultCustomThemePopup = "#202025";
    private const string DefaultCustomThemeGradientStart = "#25252A";
    private const string DefaultCustomThemeGradientEnd = "#2E3746";

    private DataManager Cfg { get; }
    private readonly LoginManager _loginManager;
    private readonly DataManager _dataManager;
    private readonly IEngineManager _engineManager;
    private readonly ContentManager _contentManager;
    private readonly LauncherSelfUpdateService _selfUpdateService;
    private readonly LocalizationManager _loc;
    private readonly SemaphoreSlim _launcherVersionsSemaphore = new(1, 1);

    public ICommand SetHWIdCommand { get; }
    public ICommand GenHWIdCommand { get; }
    public ICommand DumpConfigCommand { get; }
    public ICommand SetUsernameCommand { get; }
    public ICommand SetRPCUsernameCommand { get; }
    public ICommand SetGuestUsernameCommand { get; }
    public ICommand OpenDumpDirectoryCommand { get; }
    public ICommand RefreshLauncherVersionsCommand { get; }
    public ICommand InstallSelectedLauncherVersionCommand { get; }
    public ICommand OpenSelectedLauncherVersionCommand { get; }
    public IEnumerable<HideLevel> HideLevels { get; } = Enum.GetValues(typeof(HideLevel)).Cast<HideLevel>();
    public IEnumerable<LauncherTheme> Themes { get; } = new[]
    {
        LauncherTheme.Dark,
        LauncherTheme.DarkRed,
        LauncherTheme.DarkPurple,
        LauncherTheme.MidnightBlue,
        LauncherTheme.EmeraldDusk,
        LauncherTheme.CopperNight,
        LauncherTheme.Light,
        LauncherTheme.Custom
    };
    public IReadOnlyList<ThemeFontOption> ThemeFonts { get; } = new List<ThemeFontOption>
    {
        new("Noto Sans (Default)", AppThemeManager.DefaultFontDescriptor),
        new("Segoe UI", "Segoe UI"),
        new("Arial", "Arial"),
        new("Verdana", "Verdana"),
        new("Tahoma", "Tahoma"),
        new("Consolas", "Consolas")
    };
    public LanguageSelectorViewModel Language { get; } = new();
    public ProxyTabViewModel ProxyTab { get; } = new();
    public ObservableCollection<LauncherReleaseEntry> LauncherAvailableVersions { get; } = new();
    public IReadOnlyList<LauncherVersionFilterOption> LauncherVersionFilters { get; }

    private readonly List<LauncherReleaseEntry> _launcherAvailableVersionsAll = new();

    



    public OptionsTabViewModel()
    {
        Cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginManager = Locator.Current.GetRequiredService<LoginManager>();
        _dataManager = Locator.Current.GetRequiredService<DataManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _contentManager = Locator.Current.GetRequiredService<ContentManager>();
        _selfUpdateService = Locator.Current.GetRequiredService<LauncherSelfUpdateService>();
        _loc = LocalizationManager.Instance;

        LauncherVersionFilters = new List<LauncherVersionFilterOption>
        {
            new(LauncherVersionFilter.All, _loc.GetString("launcher-updates-filter-all")),
            new(LauncherVersionFilter.ReleaseOnly, _loc.GetString("launcher-updates-filter-release-only")),
            new(LauncherVersionFilter.PreReleaseOnly, _loc.GetString("launcher-updates-filter-prerelease-only"))
        };
        _selectedLauncherVersionFilter = LauncherVersionFilters[0];

        SetHWIdCommand = new RelayCommand(OnSetHWIdClick);
        SetRPCUsernameCommand = new RelayCommand(OnSetRPCUsernameClick);
        GenHWIdCommand = new RelayCommand(OnGenHWIdClick);
        SetUsernameCommand = new RelayCommand(OnSetUsernameClick);
        SetGuestUsernameCommand = new RelayCommand(OnSetGuestUsernameClick);
        DumpConfigCommand = new RelayCommand(DumpConfig.Dump);
        OpenDumpDirectoryCommand = new RelayCommand(OpenDumpDirectory);
        RefreshLauncherVersionsCommand = new RelayCommand(async () => await RefreshLauncherVersionsAsync());
        InstallSelectedLauncherVersionCommand = new RelayCommand(InstallSelectedLauncherVersion);
        OpenSelectedLauncherVersionCommand = new RelayCommand(OpenSelectedLauncherVersion);

        Persist.UpdateLauncherConfig();
        SetTempHwid();
        SetTempGuestUsername();

        var configuredFont = AppThemeManager.NormalizeFont(Cfg.GetCVar(CVars.ThemeFont));
        _selectedThemeFont = ThemeFonts.FirstOrDefault(f => f.Descriptor == configuredFont) ?? ThemeFonts[0];
        _customFontInfo = configuredFont == _selectedThemeFont.Descriptor
            ? ""
            : configuredFont;

        _customThemeBackground = ParseColorOrDefault(Cfg.GetCVar(CVars.ThemeCustomBackground), DefaultCustomThemeBackground);
        _customThemeAccent = ParseColorOrDefault(Cfg.GetCVar(CVars.ThemeCustomAccent), DefaultCustomThemeAccent);
        _customThemeForeground = ParseColorOrDefault(Cfg.GetCVar(CVars.ThemeCustomForeground), DefaultCustomThemeForeground);
        _customThemePopup = ParseColorOrDefault(Cfg.GetCVar(CVars.ThemeCustomPopup), DefaultCustomThemePopup);
        _customThemeGradientStart = ParseColorOrDefault(Cfg.GetCVar(CVars.ThemeCustomGradientStart), DefaultCustomThemeGradientStart);
        _customThemeGradientEnd = ParseColorOrDefault(Cfg.GetCVar(CVars.ThemeCustomGradientEnd), DefaultCustomThemeGradientEnd);
    }

    public override void Selected()
    {
        _ = RefreshLauncherVersionsAsync();
    }

    private int _selectedSettingsTabIndex;
    public int SelectedSettingsTabIndex
    {
        get => _selectedSettingsTabIndex;
        set
        {
            _selectedSettingsTabIndex = value;
            OnPropertyChanged(nameof(SelectedSettingsTabIndex));
        }
    }

    public void SelectProxySettingsTab()
    {
        SelectedSettingsTabIndex = ProxySettingsTabIndex;
    }

#if RELEASE
        public bool HideDebugKnobs => true;
#else
    public bool HideDebugKnobs => false;
#endif

    public bool EngineOverrideEnabled
    {
        get => Cfg.GetCVar(CVars.EngineOverrideEnabled);
        set
        {
            Cfg.SetCVar(CVars.EngineOverrideEnabled, value);
            Cfg.CommitConfig();
        }
    }

    public string EngineOverridePath
    {
        get => Cfg.GetCVar(CVars.EngineOverridePath) ?? "";
        set
        {
            Cfg.SetCVar(CVars.EngineOverridePath, value);
            Cfg.CommitConfig();
        }
    }




    public override string Name => _loc.GetString("tab-options-title");

    public LauncherTheme SelectedTheme
    {
        get => AppThemeManager.Normalize(Cfg.GetCVar(CVars.Theme));
        set
        {
            var normalized = AppThemeManager.Normalize((int)value);
            Cfg.SetCVar(CVars.Theme, (int)normalized);
            Cfg.CommitConfig();
            ApplyThemeSettings();

            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(IsCustomThemeSelected));
        }
    }

    public bool IsCustomThemeSelected => SelectedTheme == LauncherTheme.Custom;

    public bool ThemeGradient
    {
        get => Cfg.GetCVar(CVars.ThemeGradient);
        set
        {
            Cfg.SetCVar(CVars.ThemeGradient, value);
            Cfg.CommitConfig();
            ApplyThemeSettings();
        }
    }

    public bool ThemeDecor
    {
        get => Cfg.GetCVar(CVars.ThemeDecor);
        set
        {
            Cfg.SetCVar(CVars.ThemeDecor, value);
            Cfg.CommitConfig();
            ApplyThemeSettings();
        }
    }


    private ThemeFontOption _selectedThemeFont;
    private string _customFontInfo = "";
    private Color _customThemeBackground;
    private Color _customThemeAccent;
    private Color _customThemeForeground;
    private Color _customThemePopup;
    private Color _customThemeGradientStart;
    private Color _customThemeGradientEnd;

    public ThemeFontOption SelectedThemeFont
    {
        get => _selectedThemeFont;
        set
        {
            _selectedThemeFont = value;
            Cfg.SetCVar(CVars.ThemeFont, value.Descriptor);
            Cfg.CommitConfig();

            if (Application.Current != null)
                AppThemeManager.ApplyFont(Application.Current, value.Descriptor);

            _customFontInfo = "";
            OnPropertyChanged(nameof(SelectedThemeFont));
            OnPropertyChanged(nameof(CustomFontInfo));
        }
    }

    public string CustomFontInfo => _customFontInfo;

    public Color CustomThemeBackground
    {
        get => _customThemeBackground;
        set
        {
            if (_customThemeBackground == value)
                return;

            _customThemeBackground = value;
            Cfg.SetCVar(CVars.ThemeCustomBackground, FormatColor(value));
            Cfg.CommitConfig();
            ApplyThemeSettingsIfCustom();
            OnPropertyChanged(nameof(CustomThemeBackground));
        }
    }

    public Color CustomThemeAccent
    {
        get => _customThemeAccent;
        set
        {
            if (_customThemeAccent == value)
                return;

            _customThemeAccent = value;
            Cfg.SetCVar(CVars.ThemeCustomAccent, FormatColor(value));
            Cfg.CommitConfig();
            ApplyThemeSettingsIfCustom();
            OnPropertyChanged(nameof(CustomThemeAccent));
        }
    }

    public Color CustomThemeForeground
    {
        get => _customThemeForeground;
        set
        {
            if (_customThemeForeground == value)
                return;

            _customThemeForeground = value;
            Cfg.SetCVar(CVars.ThemeCustomForeground, FormatColor(value));
            Cfg.CommitConfig();
            ApplyThemeSettingsIfCustom();
            OnPropertyChanged(nameof(CustomThemeForeground));
        }
    }

    public Color CustomThemePopup
    {
        get => _customThemePopup;
        set
        {
            if (_customThemePopup == value)
                return;

            _customThemePopup = value;
            Cfg.SetCVar(CVars.ThemeCustomPopup, FormatColor(value));
            Cfg.CommitConfig();
            ApplyThemeSettingsIfCustom();
            OnPropertyChanged(nameof(CustomThemePopup));
        }
    }

    public Color CustomThemeGradientStart
    {
        get => _customThemeGradientStart;
        set
        {
            if (_customThemeGradientStart == value)
                return;

            _customThemeGradientStart = value;
            Cfg.SetCVar(CVars.ThemeCustomGradientStart, FormatColor(value));
            Cfg.CommitConfig();
            ApplyThemeSettingsIfCustom();
            OnPropertyChanged(nameof(CustomThemeGradientStart));
        }
    }

    public Color CustomThemeGradientEnd
    {
        get => _customThemeGradientEnd;
        set
        {
            if (_customThemeGradientEnd == value)
                return;

            _customThemeGradientEnd = value;
            Cfg.SetCVar(CVars.ThemeCustomGradientEnd, FormatColor(value));
            Cfg.CommitConfig();
            ApplyThemeSettingsIfCustom();
            OnPropertyChanged(nameof(CustomThemeGradientEnd));
        }
    }

    public void ApplyCustomFontFile(string filePath)
    {
        var familyGuess = Path.GetFileNameWithoutExtension(filePath);
        var uri = new Uri(filePath).AbsoluteUri;
        var descriptor = $"{uri}#{familyGuess}";

        Cfg.SetCVar(CVars.ThemeFont, descriptor);
        Cfg.CommitConfig();

        if (Application.Current != null)
            AppThemeManager.ApplyFont(Application.Current, descriptor);

        _customFontInfo = Path.GetFileName(filePath);
        OnPropertyChanged(nameof(CustomFontInfo));
    }

    public string ExportCustomThemeJson()
    {
        var preset = new CustomThemePreset
        {
            Version = 1,
            Background = FormatColor(CustomThemeBackground),
            Accent = FormatColor(CustomThemeAccent),
            Foreground = FormatColor(CustomThemeForeground),
            Popup = FormatColor(CustomThemePopup),
            GradientStart = FormatColor(CustomThemeGradientStart),
            GradientEnd = FormatColor(CustomThemeGradientEnd),
            GradientEnabled = ThemeGradient,
            DecorEnabled = ThemeDecor
        };

        return JsonSerializer.Serialize(preset, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public bool TryImportCustomThemeJson(string json, out string? error)
    {
        try
        {
            var preset = JsonSerializer.Deserialize<CustomThemePreset>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (preset == null)
            {
                error = "Preset is empty.";
                return false;
            }

            if (!TryParsePresetColor(preset.Background, CustomThemeBackground, out var background) ||
                !TryParsePresetColor(preset.Accent, CustomThemeAccent, out var accent) ||
                !TryParsePresetColor(preset.Foreground, CustomThemeForeground, out var foreground) ||
                !TryParsePresetColor(preset.Popup, CustomThemePopup, out var popup) ||
                !TryParsePresetColor(preset.GradientStart, CustomThemeGradientStart, out var gradientStart) ||
                !TryParsePresetColor(preset.GradientEnd, CustomThemeGradientEnd, out var gradientEnd))
            {
                error = "Invalid color value in preset.";
                return false;
            }

            ThemeGradient = preset.GradientEnabled ?? ThemeGradient;
            ThemeDecor = preset.DecorEnabled ?? ThemeDecor;
            CustomThemeBackground = background;
            CustomThemeAccent = accent;
            CustomThemeForeground = foreground;
            CustomThemePopup = popup;
            CustomThemeGradientStart = gradientStart;
            CustomThemeGradientEnd = gradientEnd;
            SelectedTheme = LauncherTheme.Custom;

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void ResetCustomTheme()
    {
        ThemeGradient = true;
        ThemeDecor = true;
        CustomThemeBackground = Color.Parse(DefaultCustomThemeBackground);
        CustomThemeAccent = Color.Parse(DefaultCustomThemeAccent);
        CustomThemeForeground = Color.Parse(DefaultCustomThemeForeground);
        CustomThemePopup = Color.Parse(DefaultCustomThemePopup);
        CustomThemeGradientStart = Color.Parse(DefaultCustomThemeGradientStart);
        CustomThemeGradientEnd = Color.Parse(DefaultCustomThemeGradientEnd);
        SelectedTheme = LauncherTheme.Custom;
    }

    public bool CompatMode
    {
        get => Cfg.GetCVar(CVars.CompatMode);
        set
        {
            Cfg.SetCVar(CVars.CompatMode, value);
            Cfg.CommitConfig();
        }
    }

    public bool DynamicPgo
    {
        get => Cfg.GetCVar(CVars.DynamicPgo);
        set
        {
            Cfg.SetCVar(CVars.DynamicPgo, value);
            Cfg.CommitConfig();
        }
    }

    public bool ServerListShowRoundTime
    {
        get => Cfg.GetCVar(CVars.ServerListShowRoundTime);
        set
        {
            Cfg.SetCVar(CVars.ServerListShowRoundTime, value);
            Cfg.CommitConfig();
            NotifyServerListDisplaySettingsChanged();
        }
    }

    public bool ServerListShowPlayers
    {
        get => Cfg.GetCVar(CVars.ServerListShowPlayers);
        set
        {
            Cfg.SetCVar(CVars.ServerListShowPlayers, value);
            Cfg.CommitConfig();
            NotifyServerListDisplaySettingsChanged();
        }
    }

    public bool ServerListShowMap
    {
        get => Cfg.GetCVar(CVars.ServerListShowMap);
        set
        {
            Cfg.SetCVar(CVars.ServerListShowMap, value);
            Cfg.CommitConfig();
            NotifyServerListDisplaySettingsChanged();
        }
    }

    public bool ServerListShowMode
    {
        get => Cfg.GetCVar(CVars.ServerListShowMode);
        set
        {
            Cfg.SetCVar(CVars.ServerListShowMode, value);
            Cfg.CommitConfig();
            NotifyServerListDisplaySettingsChanged();
        }
    }

    public bool ServerListShowPing
    {
        get => Cfg.GetCVar(CVars.ServerListShowPing);
        set
        {
            Cfg.SetCVar(CVars.ServerListShowPing, value);
            Cfg.CommitConfig();
            NotifyServerListDisplaySettingsChanged();
        }
    }

    public bool LogClient
    {
        get => Cfg.GetCVar(CVars.LogClient);
        set
        {
            Cfg.SetCVar(CVars.LogClient, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLauncher
    {
        get => Cfg.GetCVar(CVars.LogLauncher);
        set
        {
            Cfg.SetCVar(CVars.LogLauncher, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLauncherVerbose
    {
        get => Cfg.GetCVar(CVars.LogLauncherVerbose);
        set
        {
            Cfg.SetCVar(CVars.LogLauncherVerbose, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogPatches
    {
        get => Cfg.GetCVar(CVars.LogPatcher);
        set
        {
            Cfg.SetCVar(CVars.LogPatcher, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLauncherPatcher
    {
        get => Cfg.GetCVar(CVars.LogLauncherPatcher);
        set
        {
            Cfg.SetCVar(CVars.LogLauncherPatcher, value);
            Cfg.CommitConfig();

            Persist.UpdateLauncherConfig();
        }
    }

    public bool LogLoaderDebug
    {
        get => Cfg.GetCVar(CVars.LogLoaderDebug);
        set
        {
            Cfg.SetCVar(CVars.LogLoaderDebug, value);
            Cfg.CommitConfig();

            Persist.UpdateLauncherConfig();
        }
    }

    public bool LogTrace
    {
        get => Cfg.GetCVar(CVars.LogLoaderTrace);
        set
        {
            Cfg.SetCVar(CVars.LogLoaderTrace, value);
            Cfg.CommitConfig();

            Persist.UpdateLauncherConfig();
        }
    }

    public bool ThrowPatchFail
    {
        get => Cfg.GetCVar(CVars.ThrowPatchFail);
        set
        {
            Cfg.SetCVar(CVars.ThrowPatchFail, value);
            Cfg.CommitConfig();
        }
    }

    public bool SeparateLogging
    {
        get => Cfg.GetCVar(CVars.SeparateLogging);
        set
        {
            Cfg.SetCVar(CVars.SeparateLogging, value);
            Cfg.CommitConfig();
        }
    }

    public HideLevel HideLevel
    {
        get => (HideLevel)Cfg.GetCVar(CVars.MarseyHide);
        set
        {
            Cfg.SetCVar(CVars.MarseyHide, (int)value);
            OnPropertyChanged(nameof(HideLevel));
            Cfg.CommitConfig();
        }
    }

    public bool DisableSigning
    {
        get => Cfg.GetCVar(CVars.DisableSigning);
        set
        {
            Cfg.SetCVar(CVars.DisableSigning, value);
            Cfg.CommitConfig();
        }
    }
    public bool NoActiveInit
    {
        get => Cfg.GetCVar(CVars.NoActiveInit);
        set
        {
            Cfg.SetCVar(CVars.NoActiveInit, value);
            Cfg.CommitConfig();
        }
    }

    public bool DisableRPC
    {
        get => Cfg.GetCVar(CVars.DisableRPC);
        set
        {
            Cfg.SetCVar(CVars.DisableRPC, value);
            Cfg.CommitConfig();
        }
    }

    public bool FakeRPC
    {
        get => Cfg.GetCVar(CVars.FakeRPC);
        set
        {
            Cfg.SetCVar(CVars.FakeRPC, value);
            OnPropertyChanged(nameof(FakeRPC));
            Cfg.CommitConfig();
        }
    }

    public string RPCUsername
    {
        get => Cfg.GetCVar(CVars.RPCUsername);
        set
        {
            Cfg.SetCVar(CVars.RPCUsername, value);
            Cfg.CommitConfig();
        }
    }

    public bool ForcingHWID
    {
        get => Cfg.GetCVar(CVars.ForcingHWId);
        set
        {
            Cfg.SetCVar(CVars.ForcingHWId, value);
            OnPropertyChanged(nameof(ForcingHWID));
            Cfg.CommitConfig();
        }
    }

    public bool LIHWIDBind
    {
        get => Cfg.GetCVar(CVars.LIHWIDBind);
        set
        {
            Cfg.SetCVar(CVars.LIHWIDBind, value);
            Cfg.CommitConfig();
        }
    }

    public bool AutoDeleteHWID
    {
        get => Cfg.GetCVar(CVars.AutoDeleteHWID);
        set
        {
            Cfg.SetCVar(CVars.AutoDeleteHWID, value);
            Cfg.CommitConfig();
        }
    }

    private void SetTempHwid()
    {
        if (!LIHWIDBind)
        {
            _hwidString = Cfg.GetCVar(CVars.ForcedHWId);
            return;
        }

        _hwidString = _loginManager.ActiveAccount != null ? _loginManager.ActiveAccount.LoginInfo.HWID : "";
    }

    private void SetTempGuestUsername()
    {
        _guestUname = Cfg.GetCVar(CVars.GuestUsername);
    }

    private string _hwidString = "";
    public string HWIdString
    {
        get => _hwidString;
        set => _hwidString = value;
    }


    // This is cancer, quite literally
    // I hate this
    private Brush? _hwidTextBoxBorderBrush = new SolidColorBrush(Color.Parse("#FF888888"));
    public Brush? HWIDTextBoxBorderBrush
    {
        get => _hwidTextBoxBorderBrush;
        set
        {
            _hwidTextBoxBorderBrush = value;
            OnPropertyChanged(nameof(HWIDTextBoxBorderBrush));
        }
    }

    private string _guestUname = "";
    public string GuestName
    {
        get => _guestUname;
        set => _guestUname = value;
    }

    public bool MarseySlightOutOfDate
    {
        get
        {
            if (Latest == null) return false;
            int dist = MarseyVars.MarseyVersion.CompareTo(Marsey.API.MarseyApi.GetLatestVersion());
            return dist < 0;
        }
    }

    public bool MarseyJam
    {
        get => Cfg.GetCVar(CVars.JamDials);
        set
        {
            Cfg.SetCVar(CVars.JamDials, value);
            Cfg.CommitConfig();
        }
    }

    public bool MarseyHole
    {
        get => Cfg.GetCVar(CVars.Blackhole);
        set
        {
            Cfg.SetCVar(CVars.Blackhole, value);
            Cfg.CommitConfig();
        }
    }

    public bool Backports
    {
        get => Cfg.GetCVar(CVars.Backports);
        set
        {
            Cfg.SetCVar(CVars.Backports, value);
            Cfg.CommitConfig();
        }
    }

    public bool DisableAnyEngineBackports
    {
        get => Cfg.GetCVar(CVars.DisableAnyEngineBackports);
        set
        {
            Cfg.SetCVar(CVars.DisableAnyEngineBackports, value);
            Cfg.CommitConfig();
        }
    }

    public bool RandTitle
    {
        get => Cfg.GetCVar(CVars.RandTitle);
        set
        {
            Cfg.SetCVar(CVars.RandTitle, value);
            Cfg.CommitConfig();
        }
    }

    public bool RandHeader
    {
        get => Cfg.GetCVar(CVars.RandHeader);
        set
        {
            Cfg.SetCVar(CVars.RandHeader, value);
            Cfg.CommitConfig();
        }
    }

    public bool RandConnAction
    {
        get => Cfg.GetCVar(CVars.RandConnAction);
        set
        {
            Cfg.SetCVar(CVars.RandConnAction, value);
            Cfg.CommitConfig();
        }
    }

    public string Current => MarseyVars.MarseyVersion.ToString();

    public string? Latest => Marsey.API.MarseyApi.GetLatestVersion()?.ToString();

    public bool OverrideAssets
    {
        get => Cfg.GetCVar(CVars.OverrideAssets);
        set
        {
            Cfg.SetCVar(CVars.OverrideAssets, value);
            Cfg.CommitConfig();
        }
    }

    public string Username
    {
        get => _loginManager.ActiveAccount?.Username!;
        set
        {
            LoginInfo LI = _loginManager.ActiveAccount!.LoginInfo;
            LI.Username = value;
        }
    }

    private void OnSetHWIdClick()
    {
        Cfg.SetCVar(CVars.ForcedHWId, _hwidString);

        if (HWID.CheckHWID(_hwidString))
        {
            HWIDTextBoxBorderBrush = new SolidColorBrush(Color.Parse("#FF888888"));
            Cfg.CommitConfig();
        }
        else
        {
            HWIDTextBoxBorderBrush = new SolidColorBrush(Brushes.Red.Color);
        }

        OnPropertyChanged(nameof(HWIdString));
    }

    private void OnSetRPCUsernameClick()
    {
        Cfg.SetCVar(CVars.RPCUsername, RPCUsername);
        Cfg.CommitConfig();
    }

    private void OnGenHWIdClick()
    {
        string hwid = HWID.GenerateRandom();
        HWIdString = hwid;

        OnSetHWIdClick();
    }

    public bool DumpAssemblies
    {
        get => Cfg.GetCVar(CVars.DumpAssemblies);
        set
        {
            Cfg.SetCVar(CVars.DumpAssemblies, value);
            Cfg.CommitConfig();
            MarseyConf.Dumper = value;
        }
    }

    public bool HWID2OptOut
    {
        get => Cfg.GetCVar(CVars.DisallowHwid);
        set
        {
            Cfg.SetCVar(CVars.DisallowHwid, value);
            Cfg.CommitConfig();
        }
    }

    public bool Patchless
    {
        get => Cfg.GetCVar(CVars.Patchless);
        set
        {
            Cfg.SetCVar(CVars.Patchless, value);
            Cfg.CommitConfig();
        }
    }

    public bool SkipPrivacyPolicy
    {
        get => Cfg.GetCVar(CVars.SkipPrivacyPolicy);
        set
        {
            Cfg.SetCVar(CVars.SkipPrivacyPolicy, value);
            Cfg.CommitConfig();
        }
    }

    public bool ResourceOverride
    {
        get => Cfg.GetCVar(CVars.DisableStrict);
        set
        {
            Cfg.SetCVar(CVars.DisableStrict, value);
            Cfg.CommitConfig();
        }
    }

    private void OnSetUsernameClick()
    {
        _dataManager.ChangeLogin(ChangeReason.Update, _loginManager.ActiveAccount?.LoginInfo!);
        _dataManager.CommitConfig();
    }

    private void OnSetGuestUsernameClick()
    {
        Cfg.SetCVar(CVars.GuestUsername, _guestUname);
        Cfg.CommitConfig();
    }

    public void ClearEngines()
    {
        _engineManager.ClearAllEngines();
    }

    public async Task<bool> ClearServerContent()
    {
        return await _contentManager.ClearAll();
    }

    public void OpenLogDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = LauncherPaths.DirLogs
        });
    }

    public void OpenDumpDirectory()
    {
        string dumpPath = Path.Combine(MarseyVars.MarseyFolder, "Dumper", "Dumps");
        if (!Directory.Exists(dumpPath))
        {
            Directory.CreateDirectory(dumpPath);
        }

        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = dumpPath
        });
    }

    public void OpenAccountSettings()
    {
        Helpers.OpenUri(ConfigConstants.AccountManagementUrl);
    }

    private static void NotifyServerListDisplaySettingsChanged()
    {
        WeakReferenceMessenger.Default.Send(new ServerListDisplaySettingsChanged());
    }

    private static void ResetProxyRuntimeState()
    {
        LauncherProxyRuntimeState.ResetDynamicProxyState();
    }

    public bool LauncherAutoUpdate
    {
        get => Cfg.GetCVar(CVars.LauncherAutoUpdate);
        set
        {
            Cfg.SetCVar(CVars.LauncherAutoUpdate, value);
            Cfg.CommitConfig();
        }
    }

    public bool LauncherUpdateNotify
    {
        get => Cfg.GetCVar(CVars.LauncherUpdateNotify);
        set
        {
            Cfg.SetCVar(CVars.LauncherUpdateNotify, value);
            Cfg.CommitConfig();
        }
    }

    public string LauncherUpdateRepo
    {
        get => Cfg.GetCVar(CVars.LauncherUpdateRepo);
        set
        {
            Cfg.SetCVar(CVars.LauncherUpdateRepo, value?.Trim() ?? "");
            Cfg.CommitConfig();
            OnPropertyChanged(nameof(LauncherUpdateRepo));
        }
    }

    public bool LauncherUpdateAllowPreRelease
    {
        get => Cfg.GetCVar(CVars.LauncherUpdateAllowPreRelease);
        set
        {
            Cfg.SetCVar(CVars.LauncherUpdateAllowPreRelease, value);
            Cfg.CommitConfig();
        }
    }

    public bool LauncherProxyEnabled
    {
        get => Cfg.GetCVar(CVars.LauncherProxyEnabled);
        set
        {
            Cfg.SetCVar(CVars.LauncherProxyEnabled, value);
            Cfg.CommitConfig();
            ResetProxyRuntimeState();
        }
    }

    public string LauncherProxyHost
    {
        get => Cfg.GetCVar(CVars.LauncherProxyHost);
        set
        {
            Cfg.SetCVar(CVars.LauncherProxyHost, value?.Trim() ?? "");
            Cfg.SetCVar(CVars.LauncherProxySelectedProfileId, "");
            Cfg.CommitConfig();
            ResetProxyRuntimeState();
        }
    }

    public int LauncherProxyPort
    {
        get => Cfg.GetCVar(CVars.LauncherProxyPort);
        set
        {
            var clamped = Math.Clamp(value, 1, 65535);
            Cfg.SetCVar(CVars.LauncherProxyPort, clamped);
            Cfg.SetCVar(CVars.LauncherProxySelectedProfileId, "");
            Cfg.CommitConfig();
            ResetProxyRuntimeState();
        }
    }

    public string LauncherProxyUsername
    {
        get => Cfg.GetCVar(CVars.LauncherProxyUsername);
        set
        {
            Cfg.SetCVar(CVars.LauncherProxyUsername, value ?? "");
            Cfg.SetCVar(CVars.LauncherProxySelectedProfileId, "");
            Cfg.CommitConfig();
            ResetProxyRuntimeState();
        }
    }

    public string LauncherProxyPassword
    {
        get => Cfg.GetCVar(CVars.LauncherProxyPassword);
        set
        {
            Cfg.SetCVar(CVars.LauncherProxyPassword, value ?? "");
            Cfg.SetCVar(CVars.LauncherProxySelectedProfileId, "");
            Cfg.CommitConfig();
            ResetProxyRuntimeState();
        }
    }

    public bool LauncherProxyApplyToLoader
    {
        get => Cfg.GetCVar(CVars.LauncherProxyApplyToLoader);
        set
        {
            Cfg.SetCVar(CVars.LauncherProxyApplyToLoader, value);
            Cfg.CommitConfig();
            ResetProxyRuntimeState();
        }
    }

    public bool LauncherProxyUseUdpRelay
    {
        get => Cfg.GetCVar(CVars.LauncherProxyUseUdpRelay);
        set
        {
            Cfg.SetCVar(CVars.LauncherProxyUseUdpRelay, value);
            Cfg.SetCVar(CVars.LauncherProxyServiceIndependent, true);
            Cfg.CommitConfig();
            ResetProxyRuntimeState();
        }
    }

    private LauncherReleaseEntry? _selectedLauncherVersion;
    public LauncherReleaseEntry? SelectedLauncherVersion
    {
        get => _selectedLauncherVersion;
        set
        {
            _selectedLauncherVersion = value;
            OnPropertyChanged(nameof(SelectedLauncherVersion));
            OnPropertyChanged(nameof(CanInstallSelectedLauncherVersion));
            OnPropertyChanged(nameof(CanOpenSelectedLauncherVersion));
        }
    }

    private bool _launcherVersionsLoading;
    public bool LauncherVersionsLoading
    {
        get => _launcherVersionsLoading;
        private set
        {
            _launcherVersionsLoading = value;
            OnPropertyChanged(nameof(LauncherVersionsLoading));
        }
    }

    private string _launcherVersionsStatus = "";
    public string LauncherVersionsStatus
    {
        get => _launcherVersionsStatus;
        private set
        {
            _launcherVersionsStatus = value;
            OnPropertyChanged(nameof(LauncherVersionsStatus));
        }
    }

    public bool CanInstallSelectedLauncherVersion => SelectedLauncherVersion?.UpdateInfo.InstallSupported == true;
    public bool CanOpenSelectedLauncherVersion => SelectedLauncherVersion?.UpdateInfo is not null;

    private LauncherVersionFilterOption _selectedLauncherVersionFilter;
    public LauncherVersionFilterOption SelectedLauncherVersionFilter
    {
        get => _selectedLauncherVersionFilter;
        set
        {
            if (Equals(_selectedLauncherVersionFilter, value))
                return;

            _selectedLauncherVersionFilter = value;
            OnPropertyChanged(nameof(SelectedLauncherVersionFilter));
            ApplyLauncherVersionFilter();
        }
    }
    
    public new event PropertyChangedEventHandler? PropertyChanged;

    private async Task RefreshLauncherVersionsAsync()
    {
        if (!await _launcherVersionsSemaphore.WaitAsync(0))
            return;

        try
        {
            LauncherVersionsLoading = true;
            LauncherVersionsStatus = _loc.GetString("launcher-updates-list-loading");

            var repo = LauncherUpdateRepo;
            var releases = await _selfUpdateService.GetAvailableAsync(repo, CancellationToken.None);

            _launcherAvailableVersionsAll.Clear();
            foreach (var rel in releases)
            {
                _launcherAvailableVersionsAll.Add(new LauncherReleaseEntry(
                    rel,
                    rel.IsPreRelease
                        ? _loc.GetString("launcher-updates-list-channel-prerelease")
                        : _loc.GetString("launcher-updates-list-channel-release")));
            }

            ApplyLauncherVersionFilter();
        }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            Log.Warning("Failed to refresh launcher version list: GitHub rate limit exceeded.");
            LauncherVersionsStatus = _loc.GetString("launcher-updates-rate-limit");
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to refresh launcher version list.");
            LauncherVersionsStatus = e.Message;
        }
        finally
        {
            LauncherVersionsLoading = false;
            _launcherVersionsSemaphore.Release();
        }
    }

    private void InstallSelectedLauncherVersion()
    {
        if (SelectedLauncherVersion?.UpdateInfo == null)
            return;

        WeakReferenceMessenger.Default.Send(new LauncherInstallRequested(SelectedLauncherVersion.UpdateInfo));
    }

    private void OpenSelectedLauncherVersion()
    {
        if (SelectedLauncherVersion?.UpdateInfo == null)
            return;

        try
        {
            Helpers.OpenUri(new Uri(SelectedLauncherVersion.UpdateInfo.ReleasePageUrl));
        }
        catch
        {
        }
    }

    private void ApplyLauncherVersionFilter()
    {
        var previousDownloadUrl = SelectedLauncherVersion?.UpdateInfo.DownloadUrl;
        IEnumerable<LauncherReleaseEntry> filtered = _launcherAvailableVersionsAll;

        filtered = SelectedLauncherVersionFilter.Value switch
        {
            LauncherVersionFilter.ReleaseOnly => filtered.Where(v => !v.IsPreRelease),
            LauncherVersionFilter.PreReleaseOnly => filtered.Where(v => v.IsPreRelease),
            _ => filtered
        };

        LauncherAvailableVersions.Clear();
        foreach (var version in filtered)
        {
            LauncherAvailableVersions.Add(version);
        }

        SelectedLauncherVersion = LauncherAvailableVersions.FirstOrDefault(v => v.UpdateInfo.DownloadUrl == previousDownloadUrl)
                                  ?? LauncherAvailableVersions.FirstOrDefault();

        LauncherVersionsStatus = LauncherAvailableVersions.Count == 0
            ? _loc.GetString("launcher-updates-list-empty")
            : _loc.GetString("launcher-updates-list-count", ("count", LauncherAvailableVersions.Count));
    }

    private void ApplyThemeSettings()
    {
        if (Application.Current == null)
            return;

        AppThemeManager.ApplyTheme(
            Application.Current,
            SelectedTheme,
            ThemeGradient,
            ThemeDecor,
            new AppThemeManager.CustomThemeColors(
                CustomThemeBackground,
                CustomThemeAccent,
                CustomThemeForeground,
                CustomThemePopup,
                CustomThemeGradientStart,
                CustomThemeGradientEnd));
    }

    private void ApplyThemeSettingsIfCustom()
    {
        if (SelectedTheme == LauncherTheme.Custom)
            ApplyThemeSettings();
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

    private static string FormatColor(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParsePresetColor(string? raw, Color fallback, out Color color)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            color = fallback;
            return true;
        }

        try
        {
            color = Color.Parse(raw);
            return true;
        }
        catch
        {
            color = fallback;
            return false;
        }
    }

    private sealed class CustomThemePreset
    {
        public int Version { get; set; } = 1;
        public string? Background { get; set; }
        public string? Accent { get; set; }
        public string? Foreground { get; set; }
        public string? Popup { get; set; }
        public string? GradientStart { get; set; }
        public string? GradientEnd { get; set; }
        public bool? GradientEnabled { get; set; }
        public bool? DecorEnabled { get; set; }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class LauncherReleaseEntry
    {
        public LauncherSelfUpdateInfo UpdateInfo { get; }
        public string ChannelText { get; }
        public bool IsPreRelease => UpdateInfo.IsPreRelease;
        public IBrush ChannelBadgeBackground => IsPreRelease
            ? new SolidColorBrush(Color.Parse("#D08B20"))
            : new SolidColorBrush(Color.Parse("#2F8F4E"));
        public IBrush ChannelBadgeForeground => IsPreRelease
            ? new SolidColorBrush(Color.Parse("#1F1F1F"))
            : new SolidColorBrush(Color.Parse("#F0F6F0"));

        public LauncherReleaseEntry(LauncherSelfUpdateInfo updateInfo, string channelText)
        {
            UpdateInfo = updateInfo;
            ChannelText = channelText;
        }

        public string Title => UpdateInfo.VersionText;
        public string Subtitle => $"{UpdateInfo.ReleaseTag} | {UpdateInfo.PublishedAt:yyyy-MM-dd}";
        public string Tooltip => string.IsNullOrWhiteSpace(UpdateInfo.ReleaseNotes) ? "-" : UpdateInfo.ReleaseNotes;
        public string NotesPreview
        {
            get
            {
                if (string.IsNullOrWhiteSpace(UpdateInfo.ReleaseNotes))
                    return "-";

                var firstLine = UpdateInfo.ReleaseNotes
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault() ?? "-";

                return firstLine.Length > 96 ? firstLine[..96] + "..." : firstLine;
            }
        }
    }
}

public class HideLevelDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (HideLevel)(value ?? HideLevel.Normal) switch
        {
            HideLevel.Disabled => LocalizationManager.Instance.GetString("marsey-HideLevel-Disabled"),
            HideLevel.Duplicit => LocalizationManager.Instance.GetString("marsey-HideLevel-Dublicit"),
            HideLevel.Normal => LocalizationManager.Instance.GetString("marsey-HideLevel-Normal"),
            HideLevel.Explicit => LocalizationManager.Instance.GetString("marsey-HideLevel-Explicit"),
            HideLevel.Unconditional => LocalizationManager.Instance.GetString("marsey-HideLevel-Unconditional"),
            _ => "Unknown hide level."
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}

public class ThemeDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (LauncherTheme)(value ?? LauncherTheme.Dark) switch
        {
            LauncherTheme.Light => LocalizationManager.Instance.GetString("marsey-Theme-Light"),
            LauncherTheme.DarkRed => LocalizationManager.Instance.GetString("marsey-Theme-DarkRed"),
            LauncherTheme.DarkPurple => LocalizationManager.Instance.GetString("marsey-Theme-DarkPurple"),
            LauncherTheme.MidnightBlue => LocalizationManager.Instance.GetString("marsey-Theme-MidnightBlue"),
            LauncherTheme.EmeraldDusk => LocalizationManager.Instance.GetString("marsey-Theme-EmeraldDusk"),
            LauncherTheme.CopperNight => LocalizationManager.Instance.GetString("marsey-Theme-CopperNight"),
            LauncherTheme.Custom => LocalizationManager.Instance.GetString("marsey-Theme-Custom"),
            _ => LocalizationManager.Instance.GetString("marsey-Theme-Dark")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}


