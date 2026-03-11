using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.Input;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class ProxyTabViewModel : MainWindowTabViewModel
{
    private readonly DataManager _cfg;
    private readonly LocalizationManager _loc;
    private readonly SemaphoreSlim _testsSemaphore = new(1, 1);

    public ObservableCollection<ProxyProfileItem> Profiles { get; } = new();

    public ICommand RemoveProfileCommand { get; }
    public ICommand SaveProfilesCommand { get; }
    public ICommand TestSelectedCommand { get; }
    public ICommand TestAllCommand { get; }

    public ProxyTabViewModel()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _loc = LocalizationManager.Instance;

        RemoveProfileCommand = new RelayCommand(RemoveSelectedProfile);
        SaveProfilesCommand = new RelayCommand(SaveProfiles);
        TestSelectedCommand = new RelayCommand(async () => await TestSelectedAsync());
        TestAllCommand = new RelayCommand(async () => await TestAllAsync());

        ReloadProfiles();
    }

    public override string Name => _loc.GetString("tab-proxy-title");

    public bool ProxyLauncherEnabled
    {
        get => _cfg.GetCVar(CVars.LauncherProxyEnabled);
        set
        {
            _cfg.SetCVar(CVars.LauncherProxyEnabled, value);
            _cfg.CommitConfig();
            ResetRuntimeProxyBypass();
            OnPropertyChanged(nameof(ProxyLauncherEnabled));
        }
    }

    public bool ProxyGameEnabled
    {
        get => _cfg.GetCVar(CVars.LauncherProxyApplyToLoader) || _cfg.GetCVar(CVars.LauncherProxyUseUdpRelay);
        set
        {
            _cfg.SetCVar(CVars.LauncherProxyApplyToLoader, value);
            _cfg.SetCVar(CVars.LauncherProxyUseUdpRelay, value);
            _cfg.SetCVar(CVars.LauncherProxyServiceIndependent, false);
            _cfg.CommitConfig();
            OnPropertyChanged(nameof(ProxyGameEnabled));
        }
    }

    public bool ProxyUpdatesEnabled
    {
        get => _cfg.GetCVar(CVars.LauncherProxyUpdatesEnabled);
        set
        {
            _cfg.SetCVar(CVars.LauncherProxyUpdatesEnabled, value);
            _cfg.CommitConfig();
            ResetRuntimeProxyBypass();
            OnPropertyChanged(nameof(ProxyUpdatesEnabled));
        }
    }

    public bool ProxyBypassRegionEnabled
    {
        get => _cfg.GetCVar(CVars.LauncherProxyBypassRegionEnabled);
        set
        {
            _cfg.SetCVar(CVars.LauncherProxyBypassRegionEnabled, value);
            _cfg.CommitConfig();
            ResetRuntimeProxyBypass();
            OnPropertyChanged(nameof(ProxyBypassRegionEnabled));
        }
    }

    public bool ProxyGuardGameLaunch
    {
        get => _cfg.GetCVar(CVars.LauncherProxyGuardGameLaunch);
        set
        {
            _cfg.SetCVar(CVars.LauncherProxyGuardGameLaunch, value);
            _cfg.CommitConfig();
            OnPropertyChanged(nameof(ProxyGuardGameLaunch));
        }
    }

    public bool ProxyServiceDebug
    {
        get => _cfg.GetCVar(CVars.LauncherProxyServiceDebug);
        set
        {
            _cfg.SetCVar(CVars.LauncherProxyServiceDebug, value);
            _cfg.CommitConfig();
            OnPropertyChanged(nameof(ProxyServiceDebug));
        }
    }

    public bool ProxyServiceIndependent
    {
        get => _cfg.GetCVar(CVars.LauncherProxyServiceIndependent);
        set
        {
            _cfg.SetCVar(CVars.LauncherProxyServiceIndependent, value);
            _cfg.CommitConfig();
            OnPropertyChanged(nameof(ProxyServiceIndependent));
        }
    }

    private ProxyProfileItem? _selectedProfile;
    public ProxyProfileItem? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            _selectedProfile = value;
            if (value != null)
            {
                _cfg.SetCVar(CVars.LauncherProxySelectedProfileId, value.Id);
                _cfg.CommitConfig();
                ResetRuntimeProxyBypass();
                Status = _loc.GetString("proxy-status-active", ("name", value.Name));
            }
            OnPropertyChanged(nameof(SelectedProfile));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedProfile != null;

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        private set
        {
            _isTesting = value;
            OnPropertyChanged(nameof(IsTesting));
        }
    }

    private string _status = "";
    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public override void Selected()
    {
        ReloadProfiles();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ProxyProfileDraft CreateDefaultDraft()
    {
        return new ProxyProfileDraft(
            null,
            "",
            "127.0.0.1",
            1080,
            "",
            "");
    }

    public ProxyProfileDraft? CreateDraftFromSelected()
    {
        if (SelectedProfile == null)
            return null;

        return new ProxyProfileDraft(
            SelectedProfile.Id,
            SelectedProfile.Name,
            SelectedProfile.Host,
            SelectedProfile.Port,
            SelectedProfile.Username,
            SelectedProfile.Password);
    }

    public void UpsertProfile(ProxyProfileDraft draft)
    {
        var id = string.IsNullOrWhiteSpace(draft.Id)
            ? Guid.NewGuid().ToString("N")
            : draft.Id.Trim();

        var existing = Profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        if (existing == null)
        {
            existing = new ProxyProfileItem { Id = id };
            Profiles.Add(existing);
            Status = _loc.GetString("proxy-status-added");
        }
        else
        {
            Status = _loc.GetString("proxy-status-saved", ("count", Profiles.Count));
        }

        existing.Name = draft.Name.Trim();
        existing.Host = draft.Host.Trim();
        existing.Port = Math.Clamp(draft.Port, 1, 65535);
        existing.Username = draft.Username ?? "";
        existing.Password = draft.Password ?? "";
        existing.LastTcp = "-";
        existing.LastUdp = "-";
        existing.LastTest = "-";

        SelectedProfile = existing;
        SaveProfiles();
    }

    private void RemoveSelectedProfile()
    {
        if (SelectedProfile == null)
            return;

        var removedId = SelectedProfile.Id;
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
        SaveProfiles();

        if (string.Equals(_cfg.GetCVar(CVars.LauncherProxySelectedProfileId), removedId, StringComparison.Ordinal))
        {
            _cfg.SetCVar(CVars.LauncherProxySelectedProfileId, SelectedProfile?.Id ?? "");
            _cfg.CommitConfig();
        }

        Status = _loc.GetString("proxy-status-removed");
    }

    private void SaveProfiles()
    {
        var normalized = Profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Host))
            .Select(p => p.ToProfile())
            .ToList();

        var selectedId = _cfg.GetCVar(CVars.LauncherProxySelectedProfileId);
        if (string.IsNullOrWhiteSpace(selectedId) || normalized.All(p => p.Id != selectedId))
            selectedId = normalized.FirstOrDefault()?.Id ?? "";

        LauncherProxyProfiles.Save(_cfg, normalized, selectedId);
        ResetRuntimeProxyBypass();
        Status = _loc.GetString("proxy-status-saved", ("count", normalized.Count));
    }

    private async Task TestSelectedAsync()
    {
        if (SelectedProfile == null)
            return;

        await RunTestAsync(new[] { SelectedProfile });
    }

    private async Task TestAllAsync()
    {
        await RunTestAsync(Profiles.ToList());
    }

    private async Task RunTestAsync(IReadOnlyList<ProxyProfileItem> targets)
    {
        if (targets.Count == 0)
            return;

        if (!await _testsSemaphore.WaitAsync(0))
            return;

        try
        {
            IsTesting = true;
            Status = _loc.GetString("proxy-status-testing");

            foreach (var item in targets)
            {
                item.LastTest = _loc.GetString("proxy-test-running");
                item.LastTcp = "-";
                item.LastConnect = "-";
                item.LastUdp = "-";

                var cfg = item.ToConfig();
                try
                {
                    var tcpMs = await ProbeTcpMedianAsync(cfg, connectOnly: false);
                    var connectMs = await ProbeTcpMedianAsync(cfg, connectOnly: true);
                    item.LastTcp = tcpMs <= 0 ? "<1 ms" : $"{tcpMs} ms";
                    item.LastConnect = connectMs <= 0 ? "<1 ms" : $"{connectMs} ms";

                    using var udpCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                    var udp = await Socks5Probe.ProbeUdpAssociateAsync(cfg, udpCts.Token);
                    item.LastUdp = udp.Ok ? _loc.GetString("proxy-test-ok") : _loc.GetString("proxy-test-fail");
                    item.LastTest = udp.Ok ? _loc.GetString("proxy-test-ok") : udp.Error;
                }
                catch (Exception e)
                {
                    item.LastTcp = _loc.GetString("proxy-test-fail");
                    item.LastConnect = _loc.GetString("proxy-test-fail");
                    item.LastUdp = _loc.GetString("proxy-test-fail");
                    item.LastTest = e.Message;
                }
            }

            Status = _loc.GetString("proxy-status-tested");
        }
        finally
        {
            IsTesting = false;
            _testsSemaphore.Release();
        }
    }

    private static async Task<long> ProbeTcpMedianAsync(Socks5ProxyConfig cfg, bool connectOnly)
    {
        var values = new List<long>(3);
        for (var i = 0; i < 3; i++)
        {
            using var tcpCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var ping = connectOnly
                ? await Socks5Probe.ProbeTcpConnectOnlyAsync(cfg, tcpCts.Token)
                : await Socks5Probe.ProbeTcpAsync(cfg, tcpCts.Token);
            values.Add(ping);
            await Task.Delay(60);
        }

        values.Sort();
        return values[values.Count / 2];
    }

    private void ReloadProfiles()
    {
        Profiles.Clear();
        var selectedId = _cfg.GetCVar(CVars.LauncherProxySelectedProfileId);
        foreach (var profile in LauncherProxyProfiles.Load(_cfg))
        {
            Profiles.Add(new ProxyProfileItem(profile));
        }

        SelectedProfile = Profiles.FirstOrDefault(p => string.Equals(p.Id, selectedId, StringComparison.Ordinal))
                          ?? Profiles.FirstOrDefault();

        Status = Profiles.Count == 0
            ? _loc.GetString("proxy-status-empty")
            : _loc.GetString("proxy-status-loaded", ("count", Profiles.Count));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ResetRuntimeProxyBypass()
    {
        LauncherProxyRuntimeState.ResetDynamicProxyState();
    }

    public sealed class ProxyProfileItem : INotifyPropertyChanged
    {
        private string _id = "";
        private string _name = "";
        private string _host = "";
        private int _port = 1080;
        private string _username = "";
        private string _password = "";
        private string _lastTcp = "-";
        private string _lastConnect = "-";
        private string _lastUdp = "-";
        private string _lastTest = "-";

        public ProxyProfileItem()
        {
        }

        public ProxyProfileItem(LauncherProxyProfile profile)
        {
            _id = profile.Id;
            _name = profile.Name;
            _host = profile.Host;
            _port = profile.Port;
            _username = profile.Username;
            _password = profile.Password;
        }

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Protocol => "SOCKS5";

        public string Host
        {
            get => _host;
            set
            {
                _host = value;
                OnPropertyChanged(nameof(Host));
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                _port = Math.Clamp(value, 1, 65535);
                OnPropertyChanged(nameof(Port));
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        public string LastTcp
        {
            get => _lastTcp;
            set
            {
                _lastTcp = value;
                OnPropertyChanged(nameof(LastTcp));
            }
        }

        public string LastUdp
        {
            get => _lastUdp;
            set
            {
                _lastUdp = value;
                OnPropertyChanged(nameof(LastUdp));
            }
        }

        public string LastConnect
        {
            get => _lastConnect;
            set
            {
                _lastConnect = value;
                OnPropertyChanged(nameof(LastConnect));
            }
        }

        public string LastTest
        {
            get => _lastTest;
            set
            {
                _lastTest = value;
                OnPropertyChanged(nameof(LastTest));
            }
        }

        public Socks5ProxyConfig ToConfig()
        {
            return new Socks5ProxyConfig(Host.Trim(), Port, Username ?? "", Password ?? "");
        }

        public LauncherProxyProfile ToProfile()
        {
            return new LauncherProxyProfile(
                string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
                Name.Trim(),
                Host.Trim(),
                Math.Clamp(Port, 1, 65535),
                Username ?? "",
                Password ?? "");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed record ProxyProfileDraft(
        string? Id,
        string Name,
        string Host,
        int Port,
        string Username,
        string Password);
}

