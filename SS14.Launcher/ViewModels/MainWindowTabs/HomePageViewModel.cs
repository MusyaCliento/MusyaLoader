using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DynamicData;
using DynamicData.Alias;
using Microsoft.Toolkit.Mvvm.Messaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
using Marsey.Config;
using SS14.Launcher;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;
using SS14.Launcher.Views;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class HomePageViewModel : MainWindowTabViewModel
{
    public MainWindowViewModel MainWindowViewModel { get; }
    private readonly DataManager _cfg;
    private readonly ServerStatusCache _statusCache = new ServerStatusCache();
    private readonly ServerListCache _serverListCache;

    public HomePageViewModel(MainWindowViewModel mainWindowViewModel)
    {
        MainWindowViewModel = mainWindowViewModel;
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _serverListCache = Locator.Current.GetRequiredService<ServerListCache>();
        WeakReferenceMessenger.Default.Register<ServerListDisplaySettingsChanged>(this, (_, _) => RaiseServerListDisplayPropertiesChanged());

        _cfg.FavoriteServers
            .Connect()
            .Select(x => new ServerEntryViewModel(MainWindowViewModel, _statusCache.GetStatusFor(x.Address), x, _statusCache, _cfg) { ViewedInFavoritesPane = true })
            .OnItemAdded(a =>
            {
                if (IsSelected)
                {
                    _statusCache.InitialUpdateStatus(a.CacheData);
                }
            })
            .Sort(Comparer<ServerEntryViewModel>.Create((a, b) => {
                var dc = a.Favorite!.RaiseTime.CompareTo(b.Favorite!.RaiseTime);
                if (dc != 0)
                {
                    return -dc;
                }
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            }))
            .Bind(out var favorites)
            .Subscribe(_ =>
            {
                FavoritesEmpty = favorites.Count == 0;
            });

        Favorites = favorites;
    }

    public ReadOnlyObservableCollection<ServerEntryViewModel> Favorites { get; }
    public ObservableCollection<ServerEntryViewModel> Suggestions { get; } = new();

    [Reactive] public bool FavoritesEmpty { get; private set; } = true;

    public override string Name => LocalizationManager.Instance.GetString("tab-home-title");
    public Control? Control { get; set; }

    public async void DirectConnectPressed()
    {
        if (!TryGetWindow(out var window))
        {
            return;
        }

        var res = await new DirectConnectDialog().ShowDialog<string?>(window);
        if (res == null)
        {
            return;
        }

        ConnectingViewModel.StartConnect(MainWindowViewModel, res);
    }

    public async void AddFavoritePressed()
    {
        if (!TryGetWindow(out var window))
        {
            return;
        }

        var (name, address) = await new AddFavoriteDialog().ShowDialog<(string name, string address)>(window);

        try
        {
            _cfg.AddFavoriteServer(new FavoriteServer(name, address));
            _cfg.CommitConfig();
        }
        catch (ArgumentException)
        {
            // Happens if address already a favorite, so ignore.
            ShowFavoriteAlreadyExists();
        }
    }

    private static void ShowFavoriteAlreadyExists()
    {
        const string message = "That server is already in your favorites.";
        const string caption = "Favorite Already Added";

        if (OperatingSystem.IsWindows())
        {
            Helpers.MessageBoxHelper(message, caption, 0);
            return;
        }

        // Cross-platform fallback: just log.
        Serilog.Log.Warning("{Message}", message);
    }

    private bool TryGetWindow([NotNullWhen(true)] out Window? window)
    {
        window = Control?.GetVisualRoot() as Window;
        return window != null;
    }

    public void RefreshPressed()
    {
        _statusCache.Refresh();
        _serverListCache.RequestRefresh();
    }

    public override void Selected()
    {
        foreach (var favorite in Favorites)
        {
            _statusCache.InitialUpdateStatus(favorite.CacheData);
        }
        _serverListCache.RequestInitialUpdate();
    }

    public bool DumpAssemblies
    {
        get => _cfg.GetCVar(CVars.DumpAssemblies);
        set
        {
            _cfg.SetCVar(CVars.DumpAssemblies, value);
            _cfg.CommitConfig();
            MarseyConf.Dumper = value;
        }
    }

    public bool ShowRoundTimeColumn => _cfg.GetCVar(CVars.ServerListShowRoundTime);
    public bool ShowPlayerCountColumn => _cfg.GetCVar(CVars.ServerListShowPlayers);
    public bool ShowMapColumn => _cfg.GetCVar(CVars.ServerListShowMap);
    public bool ShowModeColumn => _cfg.GetCVar(CVars.ServerListShowMode);
    public bool ShowPingColumn => _cfg.GetCVar(CVars.ServerListShowPing);

    private void RaiseServerListDisplayPropertiesChanged()
    {
        this.RaisePropertyChanged(nameof(ShowRoundTimeColumn));
        this.RaisePropertyChanged(nameof(ShowPlayerCountColumn));
        this.RaisePropertyChanged(nameof(ShowMapColumn));
        this.RaisePropertyChanged(nameof(ShowModeColumn));
        this.RaisePropertyChanged(nameof(ShowPingColumn));
    }
}
