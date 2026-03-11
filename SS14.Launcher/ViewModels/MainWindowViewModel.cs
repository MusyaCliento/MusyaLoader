using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform.Storage;
using DynamicData;
using HarmonyLib;
using Microsoft.Toolkit.Mvvm.Messaging;
using Marsey.Config;
using Marsey.Game.Managers;
using Marsey.Stealthsey;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
using SS14.Launcher.Api;
using SS14.Launcher.Localization;
using SS14.Launcher.Marseyverse;
using SS14.Launcher.Models;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.Login;
using SS14.Launcher.ViewModels.MainWindowTabs;
using SS14.Launcher.Views;

namespace SS14.Launcher.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IErrorOverlayOwner
{
    private readonly DataManager _cfg;
    private readonly LoginManager _loginMgr;
    private readonly HttpClient _http;
    private readonly LauncherInfoManager _infoManager;
    private readonly LauncherSelfUpdateService _selfUpdater;
    private readonly LocalizationManager _loc;

    private int _selectedIndex;

    public DataManager Cfg => _cfg;
    [Reactive] public bool OutOfDate { get; private set; }
    [Reactive] public bool LauncherUpdateAvailable { get; private set; }
    [Reactive] public bool LauncherUpdateInProgress { get; private set; }
    [Reactive] public string LauncherUpdateVersionText { get; private set; } = "";
    [Reactive] public string LauncherUpdateVersionLine { get; private set; } = "";
    [Reactive] public string LauncherUpdateChannelText { get; private set; } = "";
    [Reactive] public string LauncherUpdateChannelBadgeBackground { get; private set; } = "#2A5FA8";
    [Reactive] public string LauncherUpdateChannelBadgeForeground { get; private set; } = "#FFFFFF";
    [Reactive] public string LauncherUpdateNotes { get; private set; } = "";
    [Reactive] public bool LauncherUpdateInstallSupported { get; private set; } = true;
    [Reactive] public string? LauncherUpdateError { get; private set; }
    [Reactive] public double LauncherUpdateProgress { get; private set; }
    [Reactive] public string LauncherUpdateProgressText { get; private set; } = "";
    private LauncherSelfUpdateInfo? _pendingLauncherUpdate;

    public HomePageViewModel HomeTab { get; }
    public ServerListTabViewModel ServersTab { get; }
    public NewsTabViewModel NewsTab { get; }
    public OptionsTabViewModel OptionsTab { get; }
    public PatchesTabViewModel PatchesTab { get; }

    public MainWindowViewModel()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginMgr = Locator.Current.GetRequiredService<LoginManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
        _infoManager = Locator.Current.GetRequiredService<LauncherInfoManager>();
        _selfUpdater = Locator.Current.GetRequiredService<LauncherSelfUpdateService>();
        _loc = LocalizationManager.Instance;

        HarmonyManager.Init(new Harmony(MarseyVars.Identifier));
        Hidesey.Initialize();

        ServersTab = new ServerListTabViewModel(this);
        NewsTab = new NewsTabViewModel();
        HomeTab = new HomePageViewModel(this);
        OptionsTab = new OptionsTabViewModel();
        PatchesTab = new PatchesTabViewModel();

        var tabs = new List<MainWindowTabViewModel>();
        tabs.Add(HomeTab);
        tabs.Add(ServersTab);
        tabs.Add(NewsTab);
        tabs.Add(OptionsTab);
        tabs.Add(PatchesTab);
#if DEVELOPMENT
        tabs.Add(new DevelopmentTabViewModel());
#endif
        Tabs = tabs;

        AccountDropDown = new AccountDropDownViewModel(this);
        LoginViewModel = new MainWindowLoginViewModel();

        this.WhenAnyValue(x => x._loginMgr.ActiveAccount)
            .Subscribe(s =>
            {
                this.RaisePropertyChanged(nameof(Username));
                this.RaisePropertyChanged(nameof(LoggedIn));
            });

        WeakReferenceMessenger.Default.Register<TitleRandomizationChanged>(this, (_, _) => RefreshTitle());
        WeakReferenceMessenger.Default.Register<HeaderRandomizationChanged>(this, (_, _) => RefreshHeaderLogo());
        WeakReferenceMessenger.Default.Register<LauncherInstallRequested>(this, async (_, msg) =>
        {
            await InstallSpecificLauncherUpdate(msg.UpdateInfo);
        });

        _cfg.Logins.Connect()
            .Subscribe(_ => { this.RaisePropertyChanged(nameof(AccountDropDownVisible)); });

        // If we leave the login view model (by an account getting selected)
        // we reset it to login state
        this.WhenAnyValue(x => x.LoggedIn)
            .DistinctUntilChanged() // Only when change.
            .Subscribe(x =>
            {
                if (x)
                {
                    // "Switch" to main window.
                    RunSelectedOnTab();
                }
                else
                {
                    LoginViewModel.SwitchToLogin();
                }
            });

        RefreshTitle();
    }

    public MainWindow? Control { get; set; }

    public IReadOnlyList<MainWindowTabViewModel> Tabs { get; }

    public bool LoggedIn => _loginMgr.ActiveAccount != null;
    private string? Username => _loginMgr.ActiveAccount?.Username;
    public bool AccountDropDownVisible => _loginMgr.Logins.Count != 0;

    public AccountDropDownViewModel AccountDropDown { get; }

    public MainWindowLoginViewModel LoginViewModel { get; }

    [Reactive] public ConnectingViewModel? ConnectingVM { get; set; }

    [Reactive] public string? BusyTask { get; private set; }
    [Reactive] public ViewModelBase? OverlayViewModel { get; private set; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var previous = Tabs[_selectedIndex];
            previous.IsSelected = false;

            this.RaiseAndSetIfChanged(ref _selectedIndex, value);

            RunSelectedOnTab();
        }
    }

    private void RunSelectedOnTab()
    {
        var tab = Tabs[_selectedIndex];
        tab.IsSelected = true;
        tab.Selected();
    }

    public ICVarEntry<bool> HasDismissedEarlyAccessWarning => Cfg.GetCVarEntry(CVars.HasDismissedEarlyAccessWarning);
    public bool ShouldShowIntelDegradationWarning => IsVulnerableToIntelDegradation(_cfg);
    public bool ShouldShowRosettaWarning => IsAppleSiliconInRosetta(_cfg);

    public string Version => $"v{LauncherVersion.Version}";

    public async void OnWindowInitialized()
    {
        await HandleUnavailableProxyOnStartup();

        BusyTask = _loc.GetString("main-window-busy-checking-update");
        await CheckLauncherUpdate();
        await CheckLauncherSelfUpdate();
        BusyTask = _loc.GetString("main-window-busy-checking-login-status");
        await CheckAccounts();
        BusyTask = null;

        if (_cfg.SelectedLoginId is { } g && _loginMgr.Logins.TryLookup(g, out var login))
        {
            TrySwitchToAccount(login);
        }

        // We should now start reacting to commands.
    }

    private async Task HandleUnavailableProxyOnStartup()
    {
        if (LauncherProxyRuntimeState.UnavailableProxyDialogShown)
            return;

        if (!string.IsNullOrWhiteSpace(LauncherProxyRuntimeState.UnavailableProxyMessage))
        {
            LauncherProxyRuntimeState.UnavailableProxyDialogShown = true;
            if (Control == null || !Control.IsVisible)
                return;

            var startupDialog = new ProxyUnavailableDialog(LauncherProxyRuntimeState.UnavailableProxyMessage);
            var startupGoToSettings = await startupDialog.ShowDialog<bool?>(Control);
            if (startupGoToSettings == true)
            {
                SelectedIndex = Tabs.IndexOf(OptionsTab);
                OptionsTab.SelectProxySettingsTab();
            }
            return;
        }

        var launcherProxyEnabled = _cfg.GetCVar(CVars.LauncherProxyEnabled);
        var updatesProxyEnabled = _cfg.GetCVar(CVars.LauncherProxyUpdatesEnabled);
        var bypassProxyEnabled = _cfg.GetCVar(CVars.LauncherProxyBypassRegionEnabled);
        if (!launcherProxyEnabled && !updatesProxyEnabled && !bypassProxyEnabled)
            return;

        if (!Socks5ProxyHelper.TryReadProxyValues(_cfg, out var proxyCfg, out var error))
            return;

        using var startupProxyCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var startupProxyProbe = await Socks5Probe.ProbeHandshakeAsync(proxyCfg, startupProxyCts.Token);
        if (startupProxyProbe.Ok)
            return;

        LauncherProxyRuntimeState.DisableLauncherProxyForSession = true;
        LauncherProxyRuntimeState.DisableUpdateProxyForSession = true;
        LauncherProxyRuntimeState.DisableBypassProxyForSession = true;

        var message = _loc.GetString(
            "proxy-unavailable-message",
            ("host", proxyCfg.Host),
            ("port", proxyCfg.Port.ToString()),
            ("error", string.IsNullOrWhiteSpace(startupProxyProbe.Error) ? (string.IsNullOrWhiteSpace(error) ? "-" : error) : startupProxyProbe.Error));

        if (Control == null || !Control.IsVisible)
            return;

        LauncherProxyRuntimeState.UnavailableProxyDialogShown = true;
        var unavailableDialog = new ProxyUnavailableDialog(message);
        var unavailableGoToSettings = await unavailableDialog.ShowDialog<bool?>(Control);
        if (unavailableGoToSettings == true)
        {
            SelectedIndex = Tabs.IndexOf(OptionsTab);
            OptionsTab.SelectProxySettingsTab();
        }
    }

    private async Task CheckAccounts()
    {
        // Check if accounts are still valid and refresh their tokens if necessary.
        await _loginMgr.Initialize();
    }

    public void OnDiscordButtonPressed()
    {
        Helpers.OpenUri(new Uri(ConfigConstants.DiscordUrl));
    }

    public void OnWebsiteButtonPressed()
    {
        Helpers.OpenUri(new Uri(ConfigConstants.WebsiteUrl));
    }

    private async Task CheckLauncherUpdate()
    {
        // await Task.Delay(1000);
        if (!ConfigConstants.DoVersionCheck)
        {
            return;
        }

        try
        {
            await _infoManager.LoadTask.WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (TimeoutException)
        {
            Log.Warning("Launcher update check timed out after 20s.");
            OutOfDate = false;
            return;
        }

        if (_infoManager.Model == null)
        {
            // Error while loading.
            Log.Warning("Unable to check for launcher update due to error, assuming up-to-date.");
            OutOfDate = false;
            return;
        }

        OutOfDate = Array.IndexOf(_infoManager.Model.AllowedVersions, ConfigConstants.CurrentLauncherVersion) == -1;
        Log.Debug("Launcher out of date? {Value}", OutOfDate);
    }

    private async Task CheckLauncherSelfUpdate()
    {
        var repo = _cfg.GetCVar(CVars.LauncherUpdateRepo);
        var auto = _cfg.GetCVar(CVars.LauncherAutoUpdate);
        var notify = _cfg.GetCVar(CVars.LauncherUpdateNotify);
        var allowPreRelease = _cfg.GetCVar(CVars.LauncherUpdateAllowPreRelease);
        if (!auto && !notify)
            return;

        try
        {
            using var updateCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var update = await _selfUpdater.CheckAsync(repo, allowPreRelease, updateCts.Token);
            if (update == null)
                return;

            _pendingLauncherUpdate = update;
            LauncherUpdateVersionText = update.VersionText;
            LauncherUpdateVersionLine = _loc.GetString("launcher-update-overlay-version", ("version", update.VersionText));
            LauncherUpdateChannelText = update.ReleaseTag;
            LauncherUpdateChannelBadgeBackground = update.IsPreRelease ? "#8A6C2E" : "#2A5FA8";
            LauncherUpdateChannelBadgeForeground = "#FFFFFF";
            LauncherUpdateNotes = update.ReleaseNotes;
            LauncherUpdateInstallSupported = update.InstallSupported;
            LauncherUpdateAvailable = true;
            LauncherUpdateError = update.InstallSupported
                ? null
                : _loc.GetString("launcher-update-error-macos-manual");

            if (auto && update.InstallSupported)
            {
                await InstallLauncherUpdate();
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to check launcher self-update.");
        }
    }

    public void SkipLauncherUpdatePressed()
    {
        LauncherUpdateAvailable = false;
        _pendingLauncherUpdate = null;
        LauncherUpdateVersionLine = "";
        LauncherUpdateChannelText = "";
        LauncherUpdateNotes = "";
    }

    public async void InstallLauncherUpdatePressed()
    {
        await InstallLauncherUpdate();
    }

    public void OpenLauncherUpdatePagePressed()
    {
        if (_pendingLauncherUpdate == null || string.IsNullOrWhiteSpace(_pendingLauncherUpdate.DownloadUrl))
            return;

        try
        {
            Helpers.OpenUri(new Uri(_pendingLauncherUpdate.DownloadUrl));
        }
        catch
        {
        }
    }

    private async Task InstallLauncherUpdate()
    {
        if (_pendingLauncherUpdate == null || LauncherUpdateInProgress)
            return;

        if (!_pendingLauncherUpdate.InstallSupported)
        {
            LauncherUpdateError = _loc.GetString("launcher-update-error-unsupported-platform");
            LauncherUpdateAvailable = true;
            return;
        }

        try
        {
            LauncherUpdateInProgress = true;
            LauncherUpdateError = null;
            LauncherUpdateProgress = 0;
            LauncherUpdateProgressText = _loc.GetString("launcher-update-progress-preparing");

            var targetFile = Path.Combine(Path.GetTempPath(), $"MusyaLoaderUpdate_{Guid.NewGuid():N}.zip");
            using var request = new HttpRequestMessage(HttpMethod.Get, _pendingLauncherUpdate.DownloadUrl);
            request.Headers.UserAgent.ParseAdd("MusyaLoader-Updater");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? 0;
            await using var src = await response.Content.ReadAsStreamAsync();
            await using var dst = File.Create(targetFile);

            var buffer = new byte[81920];
            long readTotal = 0;
            var sw = Stopwatch.StartNew();
            while (true)
            {
                var read = await src.ReadAsync(buffer);
                if (read == 0)
                    break;

                await dst.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;
                if (total > 0)
                    LauncherUpdateProgress = Math.Clamp((double)readTotal / total, 0, 1);

                var speed = readTotal / Math.Max(sw.Elapsed.TotalSeconds, 0.01);
                LauncherUpdateProgressText = $"{Helpers.FormatBytes(readTotal)} / {(total > 0 ? Helpers.FormatBytes(total) : "?")} ({Helpers.FormatBytes((long)speed)}/s)";
            }

            var updaterPath = GetUpdaterExecutablePath();
            if (string.IsNullOrWhiteSpace(updaterPath) || !File.Exists(updaterPath))
            {
                throw new FileNotFoundException($"Updater executable was not found: {updaterPath}", updaterPath);
            }

            var launcherExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var launcherDir = Path.GetDirectoryName(launcherExe) ?? "";
            var launcherName = Path.GetFileName(launcherExe);
            if (string.IsNullOrWhiteSpace(launcherDir) || string.IsNullOrWhiteSpace(launcherName))
                throw new InvalidOperationException("Unable to resolve launcher executable path.");

            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("--zip");
            psi.ArgumentList.Add(targetFile);
            psi.ArgumentList.Add("--target");
            psi.ArgumentList.Add(launcherDir);
            psi.ArgumentList.Add("--wait-pid");
            psi.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
            psi.ArgumentList.Add("--launcher");
            psi.ArgumentList.Add(launcherName);

            Process.Start(psi);
            ExitPressed();
        }
        catch (Exception e)
        {
            Log.Error(e, "Self-update installation failed.");
            LauncherUpdateError = e.Message;
            LauncherUpdateAvailable = true;
        }
        finally
        {
            LauncherUpdateInProgress = false;
        }
    }

    private async Task InstallSpecificLauncherUpdate(LauncherSelfUpdateInfo update)
    {
        _pendingLauncherUpdate = update;
        LauncherUpdateVersionText = update.VersionText;
        LauncherUpdateVersionLine = _loc.GetString("launcher-update-overlay-version", ("version", update.VersionText));
        LauncherUpdateChannelText = update.ReleaseTag;
        LauncherUpdateChannelBadgeBackground = update.IsPreRelease ? "#8A6C2E" : "#2A5FA8";
        LauncherUpdateChannelBadgeForeground = "#FFFFFF";
        LauncherUpdateNotes = update.ReleaseNotes;
        LauncherUpdateInstallSupported = update.InstallSupported;
        LauncherUpdateError = update.InstallSupported
            ? null
            : _loc.GetString("launcher-update-error-unsupported-platform");
        LauncherUpdateAvailable = true;

        await InstallLauncherUpdate();
    }

    private static string GetUpdaterExecutablePath()
    {
#if FULL_RELEASE
        var dir = LauncherPaths.DirLauncherInstall;
        if (OperatingSystem.IsWindows())
            return Path.Combine(dir, "SS14.Updater.exe");
        return Path.Combine(dir, "SS14.Updater");
#else

#if RELEASE
        const string buildConfiguration = "Release";
#else
        const string buildConfiguration = "Debug";
#endif
        var basePath = Path.GetFullPath(Path.Combine(
            LauncherPaths.DirLauncherInstall,
            "..", "..", "..", "..",
            "SS14.Updater", "bin", buildConfiguration, "net10.0"));

        if (OperatingSystem.IsWindows())
            return Path.Combine(basePath, "SS14.Updater.exe");
        return Path.Combine(basePath, "SS14.Updater");
#endif
    }

    public void ExitPressed()
    {
        Control?.Close();
    }

    public void DownloadPressed()
    {
        Helpers.OpenUri(new Uri(ConfigConstants.DownloadUrl));
    }

    public void DismissEarlyAccessPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedEarlyAccessWarning, true);
        Cfg.CommitConfig();
    }

    public void DismissIntelDegradationPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedIntelDegradation, true);
        Cfg.CommitConfig();
        this.RaisePropertyChanged(nameof(ShouldShowIntelDegradationWarning));
    }

    public void DismissAppleSiliconRosettaPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedRosettaWarning, true);
        Cfg.CommitConfig();
        this.RaisePropertyChanged(nameof(ShouldShowRosettaWarning));
    }

    public void SelectTabServers()
    {
        SelectedIndex = Tabs.IndexOf(ServersTab);
    }

    public void TrySwitchToAccount(LoggedInAccount account)
    {
        switch (account.Status)
        {
            case AccountLoginStatus.Unsure:
                TrySelectUnsureAccount(account);
                break;

            case AccountLoginStatus.Available:
                _loginMgr.ActiveAccount = account;
                break;

            case AccountLoginStatus.Expired:
                _loginMgr.ActiveAccount = null;
                LoginViewModel.SwitchToExpiredLogin(account);
                break;
        }
    }

    private async void TrySelectUnsureAccount(LoggedInAccount account)
    {
        BusyTask = _loc.GetString("main-window-busy-checking-account-status");
        try
        {
            await _loginMgr.UpdateSingleAccountStatus(account);

            // Can't be unsure, that'd have thrown.
            Debug.Assert(account.Status != AccountLoginStatus.Unsure);
            TrySwitchToAccount(account);
        }
        catch (AuthApiException e)
        {
            Log.Warning(e, "AuthApiException while trying to refresh account {login}", account.LoginInfo);
            OverlayViewModel = new AuthErrorsOverlayViewModel(this, _loc.GetString("main-window-error-connecting-auth-server"),
                new[]
                {
                    e.InnerException?.Message ?? _loc.GetString("main-window-error-unknown")
                });
        }
        finally
        {
            BusyTask = null;
        }
    }

    public void OverlayOk()
    {
        OverlayViewModel = null;
    }

    public bool IsContentBundleDropValid(IStorageFile file)
    {
        // Can only load content bundles if logged in, in some capacity.
        if (!LoggedIn)
            return false;

        // Disallow if currently connecting to a server.
        if (ConnectingVM != null)
            return false;

        return Path.GetExtension(file.Name) == ".zip";
    }

    public void Dropped(IStorageFile file)
    {
        // Trust view validated this.
        Debug.Assert(IsContentBundleDropValid(file));

        ConnectingViewModel.StartContentBundle(this, file);
    }

    private static bool IsVulnerableToIntelDegradation(DataManager cfg)
    {
        var processor = LauncherDiagnostics.GetProcessorModel();

        // No Intel processor, or already dismissed the warning.
        if (!processor.Contains("Intel") || cfg.GetCVar(CVars.HasDismissedIntelDegradation))
            return false;

        // Get the i#-#### from the processor string.
        var match = Regex.Match(processor, @"i\d+-\d+(?:[A-Z]+)?(?=\s|$)");
        if (!match.Success)
            return false;

        var affectedGenerations = new[] { "i3-13", "i5-13", "i7-13", "i9-13", "i3-14", "i5-14", "i7-14", "i9-14" };
        var excludedSuffixes = new[] { "HX", "H", "P", "U" };

        return affectedGenerations.Any(match.Value.Contains) && !excludedSuffixes.Any(match.Value.EndsWith);
    }

    private static bool IsAppleSiliconInRosetta(DataManager cfg)
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        var processor = LauncherDiagnostics.GetProcessorModel();

        return processor.Contains("VirtualApple") && !cfg.GetCVar(CVars.HasDismissedRosettaWarning);
    }

    private string _randomTitle = LauncherVersion.Name;
    public string RandomTitle => _randomTitle;

    private void RefreshTitle()
    {
        _randomTitle = new TitleManager().RandomTitle;
        this.RaisePropertyChanged(nameof(RandomTitle));
    }

    private void RefreshHeaderLogo()
    {
        if (Application.Current is App app)
            app.RefreshLogoLong(_cfg.GetCVar(CVars.RandHeader));
    }
}
