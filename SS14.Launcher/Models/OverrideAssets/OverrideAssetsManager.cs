using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Models.OverrideAssets;

public sealed class OverrideAssetsManager
{
    private readonly DataManager _dataManager;
    private readonly HttpClient _httpClient;
    private readonly LauncherInfoManager _infoManager;

    private bool _overridesUpdated;
    private bool _initialized = false;
    private CancellationTokenSource? _updateCancel;

    public event Action<OverrideAssetsChanged>? AssetsChanged;
    public OverrideAssetsManager(DataManager dataManager, HttpClient httpClient, LauncherInfoManager infoManager)
    {
        _dataManager = dataManager;
        _httpClient = httpClient;
        _infoManager = infoManager;
    }

    public void Initialize()
    {
        if (_initialized)
            return;

        using var con = GetSqliteConnection();
        con.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF;");

        Log.Debug("Migrating override assets database...");

        var sw = Stopwatch.StartNew();
        var success = Migrator.Migrate(con, "SS14.Launcher.Models.OverrideAssets.Migrations");
        if (!success)
            throw new Exception("Migration failed!");

        Log.Debug("Did override assets DB migrations in {MigrationTime}", sw.Elapsed);

        _initialized = true;
        _dataManager.GetCVarEntry(CVars.OverrideAssets).PropertyChanged += OnOverrideAssetsEnabledChanged;

        if (_dataManager.GetCVar(CVars.OverrideAssets))
            LoadAssets();
    }

    private void OnOverrideAssetsEnabledChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_dataManager.GetCVar(CVars.OverrideAssets))
            LoadAssets();
        else
        {
            _updateCancel?.Cancel();
            ClearAssets();
        }
    }

    public async void LoadAssets()
    {
        var dict = await Task.Run(() =>
        {
            using var con = GetSqliteConnection();
            var d = new Dictionary<string, byte[]>();
            foreach (var (name, data) in con.Query<(string, byte[])>("SELECT Name, Data FROM OverrideAsset"))
            {
                d.Add(name, data);
            }
            return d;
        });

        AssetsChanged?.Invoke(new OverrideAssetsChanged(dict));

        if (!_overridesUpdated)
            UpdateAssets();
    }

    private async void UpdateAssets()
    {
        _updateCancel?.Cancel();
        _updateCancel = new CancellationTokenSource();
        try
        {
            await Task.Run(() => UpdateAssetsBody(_updateCancel.Token));
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log.Warning(e, "Error while fetching new override assets"); }

        _overridesUpdated = true;
        LoadAssets();
    }

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    [SuppressMessage("ReSharper", "UseAwaitUsing")]
    private async Task UpdateAssetsBody(CancellationToken cancel)
    {
        await _infoManager.LoadTask.ConfigureAwait(false);
        if (_infoManager.Model == null) return;

        var assetsOverrides = _infoManager.Model.OverrideAssets;
        using var db = GetSqliteConnection();
        using var tx = db.BeginTransaction();

        var names = db.Query<(string, string)>("SELECT Name, OverrideName FROM OverrideAsset")
            .ToDictionary(k => k.Item1, v => v.Item2);

        foreach (var (name, overrideName) in assetsOverrides)
        {
            if (overrideName == null) continue;
            if (names.TryGetValue(name, out var curOverride) && curOverride == overrideName)
            {
                names.Remove(name);
                continue;
            }

            var url = ConfigConstants.UrlAssetsBase + overrideName;
            var data = await url.GetByteArrayAsync(_httpClient, cancel);

            db.Execute("INSERT OR REPLACE INTO OverrideAsset(Name, OverrideName, Data) VALUES (@Name, @OverrideName, @Data)",
                new { Name = name, OverrideName = overrideName, Data = data });

            names.Remove(name);
        }

        foreach (var name in names.Keys)
        {
            db.Execute("DELETE FROM OverrideAsset WHERE Name = @Name", new { Name = name });
        }

        cancel.ThrowIfCancellationRequested();
        tx.Commit();
    }

    private void ClearAssets()
    {
        AssetsChanged?.Invoke(new OverrideAssetsChanged(new Dictionary<string, byte[]>()));
    }

    public static SqliteConnection GetSqliteConnection()
    {
        var con = new SqliteConnection(GetDbConnectionString());
        con.Open();
        con.Execute("PRAGMA mmap_size=67108864; PRAGMA cache_size=-8192;"); 
        return con;
    }

    private static string GetDbConnectionString()
    {
        return $"Data Source={LauncherPaths.PathOverrideAssetsDb};Mode=ReadWriteCreate;Foreign Keys=True";
    }

}

public sealed record OverrideAssetsChanged(Dictionary<string, byte[]> Files);
