using System;
using System.ComponentModel;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using static SS14.Launcher.Utility.HubUtility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class ServerEntryViewModel : ObservableRecipient, IRecipient<FavoritesChanged>, IRecipient<ServerListDisplaySettingsChanged>, IViewModelBase
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly ServerStatusData _cacheData;
    private readonly IServerSource _serverSource;
    private readonly DataManager _cfg;
    private readonly MainWindowViewModel _windowVm;
    private string Address => _cacheData.Address;
    private string _fallbackName = string.Empty;
    private bool _isExpanded;

    public ServerEntryViewModel(MainWindowViewModel windowVm, ServerStatusData cacheData, IServerSource serverSource,
        DataManager cfg)
    {
        _cfg = cfg;
        _windowVm = windowVm;
        _cacheData = cacheData;
        _serverSource = serverSource;
        IsActive = true;
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusData cacheData,
        FavoriteServer favorite,
        IServerSource serverSource,
        DataManager cfg)
        : this(windowVm, cacheData, serverSource, cfg)
    {
        Favorite = favorite;
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusDataWithFallbackName ssdfb,
        IServerSource serverSource,
        DataManager cfg)
        : this(windowVm, ssdfb.Data, serverSource, cfg)
    {
        FallbackName = ssdfb.FallbackName ?? "";
    }

    public void Tick()
    {
        OnPropertyChanged(nameof(RoundStartTime));
    }

    public void ConnectPressed()
    {
        ConnectingViewModel.StartConnect(_windowVm, Address);
    }

    public FavoriteServer? Favorite { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            CheckUpdateInfo();
        }
    }

    public string Name => Favorite?.Name ?? _cacheData.Name ?? _fallbackName;

    private string FavoriteButtonText => IsFavorite
        ? _loc.GetString("server-entry-remove-favorite")
        : _loc.GetString("server-entry-add-favorite");

    public bool IsFavorite => _cfg.FavoriteServers.Lookup(Address).HasValue;

    public bool ViewedInFavoritesPane { get; set; }

    public bool HaveData => _cacheData.Status == ServerStatusCode.Online;
    public bool ShowRoundTimeColumn => HaveData && _cfg.GetCVar(CVars.ServerListShowRoundTime);
    public bool ShowPlayerCountColumn => HaveData && _cfg.GetCVar(CVars.ServerListShowPlayers);
    public bool ShowMapColumn => HaveData && _cfg.GetCVar(CVars.ServerListShowMap);
    public bool ShowModeColumn => HaveData && _cfg.GetCVar(CVars.ServerListShowMode);
    public bool ShowPingColumn => HaveData && _cfg.GetCVar(CVars.ServerListShowPing);

    public string ServerStatusString
    {
        get
        {
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    return _loc.GetString("server-entry-offline");
                case ServerStatusCode.FetchingStatus:
                case ServerStatusCode.Online:
                    return _loc.GetString("server-entry-fetching");
                default:
                    throw new NotSupportedException();
            }
        }
    }

    // Give a ratio for servers with a defined player count, or just a current number for those without.
    public string PlayerCountString =>
        _loc.GetString("server-entry-player-count",
            ("players", _cacheData.PlayerCount), ("max", _cacheData.SoftMaxPlayerCount));


    public DateTime? RoundStartTime => _cacheData.RoundStartTime;

    public string? MapName => _cacheData.MapName;

    public string? PresetName => _cacheData.PresetName;

    public string MapDisplayString => string.IsNullOrWhiteSpace(MapName) ? "-" : MapName!;

    public string ModeDisplayString => string.IsNullOrWhiteSpace(PresetName) ? "-" : PresetName!;

    public int? PingMilliseconds => _cacheData.Ping is null ? null : Math.Max(0, (int)Math.Round(_cacheData.Ping.Value.TotalMilliseconds));

    public string PingString => PingMilliseconds is int ping ? $"{ping} ms" : "-";

    public bool IsPingGood => PingMilliseconds is int ping && ping <= 120;
    public bool IsPingMid => PingMilliseconds is int ping && ping > 120 && ping <= 220;
    public bool IsPingBad => PingMilliseconds is int ping && ping > 220;
    public bool IsPingUnknown => PingMilliseconds is null;

    public string RoundStatusString =>
        _cacheData.RoundStatus == GameRoundStatus.InLobby
            ? _loc.GetString("server-entry-status-lobby")
            : "";

    public string Description
    {
        get
        {
            string? statusDescription;
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    statusDescription = _loc.GetString("server-entry-description-offline");
                    break;
                case ServerStatusCode.FetchingStatus:
                    statusDescription = _loc.GetString("server-entry-description-fetching");
                    break;
                default:
                    statusDescription = _cacheData.StatusInfo switch
                    {
                        ServerStatusInfoCode.NotFetched => _loc.GetString("server-entry-description-fetching"),
                        ServerStatusInfoCode.Fetching => _loc.GetString("server-entry-description-fetching"),
                        ServerStatusInfoCode.Error => _loc.GetString("server-entry-description-error"),
                        ServerStatusInfoCode.Fetched => _cacheData.Description ??
                                                        _loc.GetString("server-entry-description-none"),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    break;
            }

            return statusDescription;
        }
    }

    public bool IsOnline => _cacheData.Status == ServerStatusCode.Online;

    public string FallbackName
    {
        get => _fallbackName;
        set
        {
            SetProperty(ref _fallbackName, value);
            OnPropertyChanged(nameof(Name));
        }
    }

    public ServerStatusData CacheData => _cacheData;

    public string? FetchedFrom
    {
        get
        {
            if (_cfg.HasCustomHubs)
            {
                return _cacheData.HubAddress == null
                    ? null
                    : _loc.GetString("server-fetched-from-hub", ("hub", GetHubShortName(_cacheData.HubAddress)));
            }

            return null;
        }
    }

    public bool ShowFetchedFrom => _cfg.HasCustomHubs && !ViewedInFavoritesPane;

    public void FavoriteButtonPressed()
    {
        if (IsFavorite)
        {
            // Remove favorite.
            _cfg.RemoveFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value);
        }
        else
        {
            var fav = new FavoriteServer(_cacheData.Name ?? FallbackName, Address);
            _cfg.AddFavoriteServer(fav);
        }

        _cfg.CommitConfig();
    }

    public void FavoriteRaiseButtonPressed()
    {
        if (IsFavorite)
        {
            // Usual business, raise priority
            _cfg.RaiseFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value);
        }

        _cfg.CommitConfig();
    }

    public void Receive(FavoritesChanged message)
    {
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(FavoriteButtonText));
    }

    public void Receive(ServerListDisplaySettingsChanged message)
    {
        OnPropertyChanged(nameof(ShowRoundTimeColumn));
        OnPropertyChanged(nameof(ShowPlayerCountColumn));
        OnPropertyChanged(nameof(ShowMapColumn));
        OnPropertyChanged(nameof(ShowModeColumn));
        OnPropertyChanged(nameof(ShowPingColumn));
    }

    private void CheckUpdateInfo()
    {
        if (!IsExpanded || _cacheData.Status != ServerStatusCode.Online)
            return;

        if (_cacheData.StatusInfo is not (ServerStatusInfoCode.NotFetched or ServerStatusInfoCode.Error))
            return;

        _serverSource.UpdateInfoFor(_cacheData);
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        _cacheData.PropertyChanged += OnCacheDataOnPropertyChanged;
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        _cacheData.PropertyChanged -= OnCacheDataOnPropertyChanged;
    }

    private void OnCacheDataOnPropertyChanged(object? _, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(IServerStatusData.PlayerCount):
            case nameof(IServerStatusData.SoftMaxPlayerCount):
                OnPropertyChanged(nameof(ServerStatusString));
                OnPropertyChanged(nameof(PlayerCountString));
                OnPropertyChanged(nameof(ShowPlayerCountColumn));
                break;

            case nameof(IServerStatusData.RoundStartTime):
                OnPropertyChanged(nameof(RoundStartTime));
                OnPropertyChanged(nameof(ShowRoundTimeColumn));
                break;

            case nameof(IServerStatusData.RoundStatus):
                OnPropertyChanged(nameof(RoundStatusString));
                break;

            case nameof(IServerStatusData.MapName):
                OnPropertyChanged(nameof(MapName));
                OnPropertyChanged(nameof(MapDisplayString));
                break;

            case nameof(IServerStatusData.PresetName):
                OnPropertyChanged(nameof(PresetName));
                OnPropertyChanged(nameof(ModeDisplayString));
                break;

            case nameof(IServerStatusData.Ping):
                OnPropertyChanged(nameof(PingMilliseconds));
                OnPropertyChanged(nameof(PingString));
                OnPropertyChanged(nameof(IsPingGood));
                OnPropertyChanged(nameof(IsPingMid));
                OnPropertyChanged(nameof(IsPingBad));
                OnPropertyChanged(nameof(IsPingUnknown));
                OnPropertyChanged(nameof(ShowPingColumn));
                break;

            case nameof(IServerStatusData.Status):
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(ServerStatusString));
                OnPropertyChanged(nameof(PlayerCountString));
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(HaveData));
                OnPropertyChanged(nameof(MapName));
                OnPropertyChanged(nameof(PresetName));
                OnPropertyChanged(nameof(MapDisplayString));
                OnPropertyChanged(nameof(ModeDisplayString));
                OnPropertyChanged(nameof(ShowRoundTimeColumn));
                OnPropertyChanged(nameof(ShowPlayerCountColumn));
                OnPropertyChanged(nameof(ShowMapColumn));
                OnPropertyChanged(nameof(ShowModeColumn));
                OnPropertyChanged(nameof(ShowPingColumn));
                OnPropertyChanged(nameof(PingString));
                OnPropertyChanged(nameof(IsPingGood));
                OnPropertyChanged(nameof(IsPingMid));
                OnPropertyChanged(nameof(IsPingBad));
                OnPropertyChanged(nameof(IsPingUnknown));
                CheckUpdateInfo();
                break;

            case nameof(IServerStatusData.Name):
                OnPropertyChanged(nameof(Name));
                break;

            case nameof(IServerStatusData.Description):
            case nameof(IServerStatusData.StatusInfo):
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(HaveData));
                break;
        }
    }
}
