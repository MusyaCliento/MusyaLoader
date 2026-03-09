using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Api;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.ServerStatus;

public sealed class ServerStatusCache : IServerSource
{
    private readonly Dictionary<string, CacheReg> _cachedData = new();
    private readonly HttpClient _http;

    public ServerStatusCache()
    {
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    public ServerStatusData GetStatusFor(string serverAddress)
    {
        if (_cachedData.TryGetValue(serverAddress, out var reg))
            return reg.Data;

        var data = new ServerStatusData(serverAddress);
        reg = new CacheReg(data);
        _cachedData.Add(serverAddress, reg);
        return data;
    }

    public void InitialUpdateStatus(ServerStatusData data)
    {
        var reg = _cachedData[data.Address];
        if (reg.DidInitialStatusUpdate) return;
        UpdateStatusFor(reg);
    }

    public void BulkInitialUpdateStatus(IEnumerable<ServerStatusData> servers)
    {
        var tasksToRun = new List<ServerStatusData>();
        foreach (var server in servers)
        {
            if (!_cachedData.TryGetValue(server.Address, out var reg))
            {
                reg = new CacheReg(server);
                _cachedData.Add(server.Address, reg);
            }

            if (!reg.DidInitialStatusUpdate)
            {
                reg.DidInitialStatusUpdate = true;
                tasksToRun.Add(server);
            }
        }

        if (tasksToRun.Count == 0) return;

        _ = Task.Run(async () =>
        {
            using var semaphore = new SemaphoreSlim(6);
            var tasks = tasksToRun.Select(async data =>
            {
                await semaphore.WaitAsync();
                try { await UpdateStatusFor(data, _http, CancellationToken.None); }
                finally { semaphore.Release(); }
            });
            await Task.WhenAll(tasks);
        });
    }

    private async void UpdateStatusFor(CacheReg reg)
    {
        reg.DidInitialStatusUpdate = true;
        await reg.Semaphore.WaitAsync();
        var cancelSource = reg.Cancellation = new CancellationTokenSource();
        try { await UpdateStatusFor(reg.Data, _http, cancelSource.Token); }
        finally { reg.Semaphore.Release(); }
    }

    public static async Task UpdateStatusFor(ServerStatusData data, HttpClient http, CancellationToken cancel)
    {
        try
        {
            if (!UriHelper.TryParseSs14Uri(data.Address, out var parsedAddress))
            {
                data.Status = ServerStatusCode.Offline;
                return;
            }

            var statusAddr = UriHelper.GetServerStatusAddress(parsedAddress);
            data.Status = ServerStatusCode.FetchingStatus;
            data.Ping = null;

            ServerApi.ServerStatus status;
            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                linkedToken.CancelAfter(ConfigConstants.ServerStatusTimeout);
                status = await http.GetFromJsonAsync<ServerApi.ServerStatus>(statusAddr, linkedToken.Token) ?? throw new InvalidDataException();
            }

            ApplyStatus(data, status);
            await UpdatePingFor(data, http, cancel);
        }
        catch
        {
            data.Ping = null;
            data.Status = ServerStatusCode.Offline;
        }
    }

    public static async Task UpdatePingFor(ServerStatusData data, HttpClient http, CancellationToken cancel)
    {
        try
        {
            if (!UriHelper.TryParseSs14Uri(data.Address, out var parsedAddress))
            {
                data.Ping = null;
                return;
            }

            var statusAddr = UriHelper.GetServerStatusAddress(parsedAddress);
            var firstProbe = await MeasurePing(statusAddr, http, cancel);
            if (firstProbe == null)
            {
                data.Ping = null;
                return;
            }

            // The very first probe often includes DNS/TLS warm-up overhead.
            // If this is our first known ping for this entry, take a second sample and keep the better one.
            if (data.Ping == null)
            {
                var secondProbe = await MeasurePing(statusAddr, http, cancel);
                if (secondProbe != null)
                {
                    data.Ping = secondProbe.Value < firstProbe.Value ? secondProbe : firstProbe;
                    return;
                }
            }

            data.Ping = firstProbe;
        }
        catch
        {
            data.Ping = null;
        }
    }

    private static async Task<TimeSpan?> MeasurePing(Uri statusAddr, HttpClient http, CancellationToken cancel)
    {
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        linkedToken.CancelAfter(ConfigConstants.ServerStatusTimeout);

        var stopwatch = Stopwatch.StartNew();
        using var response = await http.GetAsync(statusAddr, HttpCompletionOption.ResponseHeadersRead, linkedToken.Token);
        stopwatch.Stop();

        return response.IsSuccessStatusCode ? stopwatch.Elapsed : null;
    }

    public static void ApplyStatus(ServerStatusData data, ServerApi.ServerStatus status)
    {
        data.Status = ServerStatusCode.Online;
        data.Name = status.Name;
        data.MapName = status.Map;
        data.PresetName = status.Preset;
        data.PlayerCount = Math.Max(0, status.PlayerCount);
        data.SoftMaxPlayerCount = Math.Max(0, status.SoftMaxPlayerCount);
        data.RoundStatus = status.RunLevel switch {
            ServerApi.GameRunLevel.InRound => GameRoundStatus.InRound,
            ServerApi.GameRunLevel.PostRound or ServerApi.GameRunLevel.PreRoundLobby => GameRoundStatus.InLobby,
            _ => GameRoundStatus.Unknown
        };

        if (status.RoundStartTime != null)
            data.RoundStartTime = DateTime.Parse(status.RoundStartTime, null, System.Globalization.DateTimeStyles.RoundtripKind);

        data.Tags = (status.Tags ?? Array.Empty<string>()).Concat(ServerTagInfer.InferTags(status)).ToArray();
    }

    public static async void UpdateInfoForCore(ServerStatusData data, Func<CancellationToken, Task<ServerInfo?>> fetch)
    {
        if (data.StatusInfo == ServerStatusInfoCode.Fetching || data.Status != ServerStatusCode.Online) return;

        data.InfoCancel?.Cancel();
        data.InfoCancel = new CancellationTokenSource();
        data.StatusInfo = ServerStatusInfoCode.Fetching;

        try
        {
            using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(data.InfoCancel.Token);
            linkedToken.CancelAfter(ConfigConstants.ServerStatusTimeout);
            var info = await fetch(linkedToken.Token) ?? throw new InvalidDataException();
            
            data.StatusInfo = ServerStatusInfoCode.Fetched;
            data.Description = info.Desc;
            data.Links = info.Links;
        }
        catch { data.StatusInfo = ServerStatusInfoCode.Error; }
    }

    public void Refresh()
    {
        foreach (var datum in _cachedData.Values)
        {
            if (!datum.DidInitialStatusUpdate) continue;
            datum.Cancellation?.Cancel();
            datum.Data.InfoCancel?.Cancel();
            datum.Data.StatusInfo = ServerStatusInfoCode.NotFetched;
            UpdateStatusFor(datum);
        }
    }

    public void Clear()
    {
        foreach (var value in _cachedData.Values)
        {
            value.Cancellation?.Cancel();
            value.Data.InfoCancel?.Cancel();
        }
        _cachedData.Clear();
    }

    void IServerSource.UpdateInfoFor(ServerStatusData statusData)
    {
        UpdateInfoForCore(statusData, async cancel =>
        {
            var uriBuilder = new UriBuilder(UriHelper.GetServerInfoAddress(statusData.Address));
            uriBuilder.Query = "?can_skip_build=1";
            return await _http.GetFromJsonAsync<ServerInfo>(uriBuilder.ToString(), cancel);
        });
    }

    private sealed class CacheReg
    {
        public readonly ServerStatusData Data;
        public readonly SemaphoreSlim Semaphore = new(1);
        public CancellationTokenSource? Cancellation;
        public bool DidInitialStatusUpdate;
        public CacheReg(ServerStatusData data) => Data = data;
    }
}
