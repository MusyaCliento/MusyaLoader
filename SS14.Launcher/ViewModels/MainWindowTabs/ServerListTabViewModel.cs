using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Toolkit.Mvvm.Messaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class ServerListTabViewModel : MainWindowTabViewModel
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly MainWindowViewModel _windowVm;
    private readonly ServerListCache _serverListCache;
    private CancellationTokenSource? _searchCancel;

    public ObservableCollection<ServerEntryViewModel> SearchedServers { get; } = new BatchObservableCollection<ServerEntryViewModel>();

    private string? _searchString;

    public override string Name => _loc.GetString("tab-servers-title");

    public string? SearchString
    {
        get => _searchString;
        set => this.RaiseAndSetIfChanged(ref _searchString, value);
    }

    private const int throttleMs = 200;

    public bool SpinnerVisible => _serverListCache.Status < RefreshListStatus.Updated;

    public string ListText
    {
        get
        {
            var status = _serverListCache.Status;
            switch (status)
            {
                case RefreshListStatus.Error:
                    return _loc.GetString("tab-servers-list-status-error");
                case RefreshListStatus.PartialError:
                    return _loc.GetString("tab-servers-list-status-partial-error");
                case RefreshListStatus.UpdatingMaster:
                    return _loc.GetString("tab-servers-list-status-updating-master");
                case RefreshListStatus.NotUpdated:
                    return "";
                case RefreshListStatus.Updated:
                default:
                    if (SearchedServers.Count == 0 && _serverListCache.AllServers.Count != 0)
                        return _loc.GetString("tab-servers-list-status-none-filtered");

                    if (_serverListCache.AllServers.Count == 0)
                        return _loc.GetString("tab-servers-list-status-none");

                    return "";
            }
        }
    }

    [Reactive] public bool FiltersVisible { get; set; }
    public bool ShowRoundTimeColumn => _windowVm.Cfg.GetCVar(CVars.ServerListShowRoundTime);
    public bool ShowPlayerCountColumn => _windowVm.Cfg.GetCVar(CVars.ServerListShowPlayers);
    public bool ShowMapColumn => _windowVm.Cfg.GetCVar(CVars.ServerListShowMap);
    public bool ShowModeColumn => _windowVm.Cfg.GetCVar(CVars.ServerListShowMode);
    public bool ShowPingColumn => _windowVm.Cfg.GetCVar(CVars.ServerListShowPing);

    public ServerListFiltersViewModel Filters { get; }

    public ServerListTabViewModel(MainWindowViewModel windowVm)
    {
        Filters = new ServerListFiltersViewModel(windowVm.Cfg, _loc);
        Filters.FiltersUpdated += FiltersOnFiltersUpdated;

        _windowVm = windowVm;
        _serverListCache = Locator.Current.GetRequiredService<ServerListCache>();
        WeakReferenceMessenger.Default.Register<ServerListDisplaySettingsChanged>(this, (_, _) => RaiseServerListDisplayPropertiesChanged());

        _serverListCache.AllServers.CollectionChanged += ServerListUpdated;

        _serverListCache.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ServerListCache.Status):
                    this.RaisePropertyChanged(nameof(ListText));
                    this.RaisePropertyChanged(nameof(SpinnerVisible));
                    break;
            }
        };

        _loc.LanguageSwitched += () => Filters.UpdatePresentFilters(_serverListCache.AllServers);

        this.WhenAnyValue(x => x.SearchString)
            .Throttle(TimeSpan.FromMilliseconds(throttleMs), RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateSearchedList());
    }

    private void FiltersOnFiltersUpdated()
    {
        UpdateSearchedList();
    }

    public override void Selected()
    {
        _serverListCache.RequestInitialUpdate();
    }

    public void RefreshPressed()
    {
        _serverListCache.RequestRefresh();
    }

    private void ServerListUpdated(object? sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
    {
        Filters.UpdatePresentFilters(_serverListCache.AllServers);

        if (_serverListCache.AllServers.Count > 0)
        {
            var statusCache = Locator.Current.GetService<ServerStatusCache>();
            if (statusCache != null)
            {
                statusCache.BulkInitialUpdateStatus(_serverListCache.AllServers);
            }
        }

        UpdateSearchedList();
    }

    private void RaiseServerListDisplayPropertiesChanged()
    {
        this.RaisePropertyChanged(nameof(ShowRoundTimeColumn));
        this.RaisePropertyChanged(nameof(ShowPlayerCountColumn));
        this.RaisePropertyChanged(nameof(ShowMapColumn));
        this.RaisePropertyChanged(nameof(ShowModeColumn));
        this.RaisePropertyChanged(nameof(ShowPingColumn));
    }

    private void UpdateSearchedList()
    {
        _searchCancel?.Cancel();
        _searchCancel = new CancellationTokenSource();
        var token = _searchCancel.Token;

        var allServersSnapshot = _serverListCache.AllServers.ToList();
        var currentSearch = SearchString;

        _ = Task.Run(async () =>
        {
            try 
            {
                if (token.IsCancellationRequested) return;

                var sortList = new List<ServerStatusData>();
                foreach (var server in allServersSnapshot)
                {
                    if (token.IsCancellationRequested) return;
                    
                    if (string.IsNullOrWhiteSpace(currentSearch) || 
                       (server.Name != null && server.Name.Contains(currentSearch, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        sortList.Add(server);
                    }
                }

                Filters.ApplyFilters(sortList);
                sortList.Sort(ServerSortComparer.Instance);

                if (token.IsCancellationRequested) return;

                var vms = new List<ServerEntryViewModel>(sortList.Count);
                foreach (var server in sortList)
                {
                    if (token.IsCancellationRequested) return;
                    vms.Add(new ServerEntryViewModel(_windowVm, server, _serverListCache, _windowVm.Cfg));
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;

                    if (SearchedServers is BatchObservableCollection<ServerEntryViewModel> batch)
                    {
                        batch.ReplaceAll(vms);
                    }
                    else
                    {
                        SearchedServers.Clear();
                        foreach (var vm in vms) SearchedServers.Add(vm);
                    }

                    this.RaisePropertyChanged(nameof(ListText));
                }, DispatcherPriority.Background);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private bool DoesSearchMatch(ServerStatusData data)
    {
        if (string.IsNullOrWhiteSpace(SearchString))
            return true;

        return data.Name != null &&
               data.Name.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase);
    }

    private sealed class ServerSortComparer : NotNullComparer<ServerStatusData>
    {
        public static readonly ServerSortComparer Instance = new();

        public override int Compare(ServerStatusData x, ServerStatusData y)
        {
            var res = x.PlayerCount.CompareTo(y.PlayerCount);
            if (res != 0) return -res;

            res = string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            if (res != 0) return res;

            return string.Compare(x.Address, y.Address, StringComparison.Ordinal);
        }
    }

    private sealed class BatchObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            Items.Clear();
            foreach (var item in items) Items.Add(item);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
