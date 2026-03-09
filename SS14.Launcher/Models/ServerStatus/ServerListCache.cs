using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Utility;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;
using static SS14.Launcher.Api.HubApi;

namespace SS14.Launcher.Models.ServerStatus;

public sealed class ServerListCache : ReactiveObject, IServerSource
{
    private readonly HubApi _hubApi;
    private readonly DataManager _dataManager;
    private readonly HttpClient _http;
    private CancellationTokenSource? _refreshCancel;

    public ObservableCollection<ServerStatusData> AllServers => _allServers;
    private readonly ServerListCollection _allServers = new();

    [Reactive]
    public RefreshListStatus Status { get; private set; } = RefreshListStatus.NotUpdated;

    public ServerListCache()
    {
        _hubApi = Locator.Current.GetRequiredService<HubApi>();
        _dataManager = Locator.Current.GetRequiredService<DataManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    public void RequestInitialUpdate()
    {
        if (Status == RefreshListStatus.NotUpdated)
            RequestRefresh();
    }

    public void RequestRefresh()
    {
        _refreshCancel?.Cancel();
        _allServers.Clear();
        _refreshCancel = new CancellationTokenSource(20000);
        RefreshServerList(_refreshCancel.Token);
    }

    public async void RefreshServerList(CancellationToken cancel)
    {
        Status = RefreshListStatus.UpdatingMaster;

        try
        {
            var entries = new HashSet<HubServerListEntry>();
            var requests = new List<(Task<ServerListEntry[]> Request, Uri Hub)>();
            var allSucceeded = true;

            foreach (var hub in ConfigConstants.DefaultHubUrls)
                requests.Add((_hubApi.GetServers(hub, cancel), new Uri(hub.Urls[0])));

            foreach (var hub in _dataManager.Hubs.OrderBy(h => h.Priority))
                requests.Add((_hubApi.GetServers(UrlFallbackSet.FromSingle(hub.Address), cancel), hub.Address));

            try
            {
                await Task.WhenAll(requests.Select(t => t.Request).ToArray());
            }
            catch
            {
                // Individual request failures are handled below via IsCompletedSuccessfully.
            }

            var processedServers = await Task.Run(() => {
                var list = new List<ServerStatusData>();
                foreach (var (request, hub) in requests)
                {
                    if (!request.IsCompletedSuccessfully)
                    {
                        allSucceeded = false;
                        continue;
                    }

                    foreach (var entry in request.Result)
                    {
                        var maybeNewEntry = new HubServerListEntry(entry.Address, hub.AbsoluteUri, entry.StatusData);
                        if (entries.Add(maybeNewEntry))
                        {
                            var statusData = new ServerStatusData(entry.Address, hub.AbsoluteUri);
                            ServerStatusCache.ApplyStatus(statusData, entry.StatusData);
                            list.Add(statusData);
                        }
                    }
                }
                return list;
            }, cancel);

            _allServers.AddItems(processedServers);
            _ = Task.Run(() => UpdatePingForServers(processedServers, cancel), CancellationToken.None);

            if (_allServers.Count == 0) Status = RefreshListStatus.Error;
            else if (!allSucceeded) Status = RefreshListStatus.PartialError;
            else Status = RefreshListStatus.Updated;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (IsProxyError(e))
                Log.Warning("Failed to fetch server list due to proxy connectivity/authentication issue.");
            else
                Log.Error(e, "Failed to fetch server list");
            Status = RefreshListStatus.Error;
        }
    }

    void IServerSource.UpdateInfoFor(ServerStatusData statusData)
    {
        if (statusData.HubAddress == null) return;
        ServerStatusCache.UpdateInfoForCore(statusData, async token => await _hubApi.GetServerInfo(statusData.Address, statusData.HubAddress, token));
    }

    private async Task UpdatePingForServers(IEnumerable<ServerStatusData> servers, CancellationToken cancel)
    {
        using var semaphore = new SemaphoreSlim(8);
        var tasks = servers.Select(async server =>
        {
            if (cancel.IsCancellationRequested)
                return;

            var entered = false;
            try
            {
                await semaphore.WaitAsync(cancel);
                entered = true;
                await ServerStatusCache.UpdatePingFor(server, _http, cancel);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (entered)
                    semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private static bool IsProxyError(Exception exception)
    {
        foreach (var ex in Flatten(exception))
        {
            var msg = ex.Message ?? string.Empty;
            if (msg.Contains("SOCKS", StringComparison.OrdinalIgnoreCase))
                return true;
            if (msg.Contains("proxy tunnel", StringComparison.OrdinalIgnoreCase))
                return true;
            if (msg.Contains("конечный компьютер отверг запрос", StringComparison.OrdinalIgnoreCase))
                return true;
            if (msg.Contains("127.0.0.1:1080", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
                yield return inner;
            yield break;
        }

        var current = exception;
        while (current != null)
        {
            yield return current;
            current = current.InnerException;
        }
    }

    private sealed class ServerListCollection : ObservableCollection<ServerStatusData>
    {
        public void AddItems(IEnumerable<ServerStatusData> items)
        {
            foreach (var item in items) Items.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
