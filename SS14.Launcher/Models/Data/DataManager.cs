using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DynamicData;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.Toolkit.Mvvm.Messaging;
using ReactiveUI;
using Serilog;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.Data;

/// <summary>
/// A CVar entry in the <see cref="DataManager"/>. This is a separate object to allow data binding easily.
/// </summary>
/// <typeparam name="T">The type of value stored by the CVar.</typeparam>
public interface ICVarEntry<T> : INotifyPropertyChanged
{
    public T Value { get; set; }
}

/// <summary>
///     Handles storage of all permanent data,
///     like username, current build, favorite servers...
/// </summary>
/// <remarks>
/// All data is stored in an SQLite DB. Simple config variables are stored K/V in a single table.
/// More complex things like logins is stored in individual tables.
/// </remarks>
public sealed class DataManager : ReactiveObject
{
    private delegate void DbCommand(SqliteConnection connection);

    private readonly SourceCache<FavoriteServer, string> _favoriteServers = new(f => f.Address);

    private readonly SourceCache<LoginInfo, Guid> _logins = new(l => l.UserId);

    // When using dynamic engine management, this is used to keep track of installed engine versions.
    private readonly SourceCache<InstalledEngineVersion, string> _engineInstallations = new(v => v.Version);

    private readonly HashSet<ServerFilter> _filters = new();
    private readonly List<Hub> _hubs = new();

    private readonly Dictionary<string, CVarEntry> _configEntries = new();

    private readonly List<InstalledEngineModule> _modules = new();
    private readonly HashSet<EngineModuleKey> _moduleLookup = new();

    private readonly List<DbCommand> _dbCommandQueue = new();
    private readonly SemaphoreSlim _dbWritingSemaphore = new(1);

    // Privacy policy IDs accepted along with the last accepted version.
    private readonly Dictionary<string, string> _acceptedPrivacyPolicies = new();

    static DataManager()
    {
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
        SqlMapper.AddTypeHandler(new UriTypeHandler());
    }

    public DataManager()
    {
        Filters = new ServerFilterCollection(this);
        Hubs = new HubCollection(this);
        // Set up subscriptions to listen for when the list-data (e.g. logins) changes in any way.
        // All these operations match directly SQL UPDATE/INSERT/DELETE.

        // Favorites
        _favoriteServers.Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(c => ChangeFavoriteServer(ChangeReason.Update, c!));

        _favoriteServers.Connect()
            .ForEachChange(c => ChangeFavoriteServer(c.Reason, c.Current))
            .Subscribe(_ => WeakReferenceMessenger.Default.Send(new FavoritesChanged()));

        // Logins
        _logins.Connect()
            .ForEachChange(c => ChangeLogin(c.Reason, c.Current))
            .Subscribe();

        _logins.Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(c => ChangeLogin(ChangeReason.Update, c!));

        // Engine installations. Doesn't need UPDATE because immutable.
        _engineInstallations.Connect()
            .ForEachChange(c => ChangeEngineInstallation(c.Reason, c.Current))
            .Subscribe();
    }

    //public Guid Fingerprint => Guid.Parse(GetCVar(CVars.Fingerprint));

    public Guid? SelectedLoginId
    {
        get
        {
            var value = GetCVar(CVars.SelectedLogin);
            if (value == "")
                return null;

            if (Guid.TryParse(value, out var guid))
                return guid;

            Log.Warning("Invalid SelectedLogin value in config: {SelectedLogin}. Resetting it.", value);
            SetCVar(CVars.SelectedLogin, "");
            CommitConfig();
            return null;
        }
        set
        {
            //  Marsey Guest привёл к новой системе
            if (value != null && value != Guid.Empty && !_logins.Lookup(value.Value).HasValue)
            {
                throw new ArgumentException("We are not logged in for that user ID.");
            }

            SetCVar(CVars.SelectedLogin, value.ToString()!);
            CommitConfig();
        }
    }

    public IObservableCache<FavoriteServer, string> FavoriteServers => _favoriteServers;
    public IObservableCache<LoginInfo, Guid> Logins => _logins;
    public IObservableCache<InstalledEngineVersion, string> EngineInstallations => _engineInstallations;
    public IEnumerable<InstalledEngineModule> EngineModules => _modules;
    public bool HasEngineModule(string name, string version) => _moduleLookup.Contains(new EngineModuleKey(name, version));
    public ICollection<ServerFilter> Filters { get; }
    public ICollection<Hub> Hubs { get; }

    public bool HasCustomHubs => Hubs.Count > 0;

    public bool ActuallyMultiAccounts =>
#if DEBUG
        true;
#else
        // Sus заявления лучше верну эту строчку на true как в marsey GetCVar(CVars.MultiAccounts);
        true;
#endif

    public void AddFavoriteServer(FavoriteServer server)
    {
        if (_favoriteServers.Lookup(server.Address).HasValue)
        {
            throw new ArgumentException("A server with that address is already a favorite.");
        }

        _favoriteServers.AddOrUpdate(server);
    }

    public void RemoveFavoriteServer(FavoriteServer server)
    {
        _favoriteServers.Remove(server);
    }

    public void RaiseFavoriteServer(FavoriteServer server)
    {
        _favoriteServers.Remove(server);
        server.RaiseTime = DateTimeOffset.UtcNow;
        _favoriteServers.AddOrUpdate(server);
    }

    public void AddEngineInstallation(InstalledEngineVersion version)
    {
        _engineInstallations.AddOrUpdate(version);
    }

    public void RemoveEngineInstallation(InstalledEngineVersion version)
    {
        _engineInstallations.Remove(version);
    }

    public void AddEngineModule(InstalledEngineModule module)
    {
        if (!_moduleLookup.Add(new EngineModuleKey(module.Name, module.Version)))
            return;

        _modules.Add(module);
        AddDbCommand(c => c.Execute("INSERT INTO EngineModule VALUES (@Name, @Version)", module));
    }

    public void RemoveEngineModule(InstalledEngineModule module)
    {
        if (!_moduleLookup.Remove(new EngineModuleKey(module.Name, module.Version)))
            return;

        _modules.Remove(module);
        AddDbCommand(c => c.Execute("DELETE FROM EngineModule WHERE Name = @Name AND Version = @Version", module));
    }

    public void AddLogin(LoginInfo login)
    {
        if (_logins.Lookup(login.UserId).HasValue)
        {
            throw new ArgumentException("A login with that UID already exists.");
        }

        _logins.AddOrUpdate(login);
    }

    public void RemoveLogin(LoginInfo loginInfo)
    {
        _logins.Remove(loginInfo);

        if (loginInfo.UserId == SelectedLoginId)
        {
            SelectedLoginId = null;
        }
    }

    /// <summary>
    /// Overwrites hubs in database with a new list of hubs.
    /// </summary>
    public void SetHubs(List<Hub> hubs)
    {
        Hubs.Clear();
        foreach (var hub in hubs)
        {
            Hubs.Add(hub);
        }
        CommitConfig();
    }

    public bool HasAcceptedPrivacyPolicy(string privacyPolicy, [NotNullWhen(true)] out string? version)
    {
        return _acceptedPrivacyPolicies.TryGetValue(privacyPolicy, out version);
    }

    public void AcceptPrivacyPolicy(string privacyPolicy, string version)
    {
        if (_acceptedPrivacyPolicies.ContainsKey(privacyPolicy))
        {
            // Accepting new version
            AddDbCommand(db => db.Execute("""
                UPDATE AcceptedPrivacyPolicy
                SET Version = @Version, LastConnected = DATETIME('now')
                WHERE Identifier = @Identifier
                """, new { Identifier = privacyPolicy, Version = version }));
        }
        else
        {
            // Accepting new privacy policy entirely.
            AddDbCommand(db => db.Execute("""
                INSERT OR REPLACE INTO AcceptedPrivacyPolicy (Identifier, Version, AcceptedTime, LastConnected)
                VALUES (@Identifier, @Version, DATETIME('now'), DATETIME('now'))
                """, new { Identifier = privacyPolicy, Version = version }));
        }

        _acceptedPrivacyPolicies[privacyPolicy] = version;
    }

    public void UpdateConnectedToPrivacyPolicy(string privacyPolicy)
    {
        AddDbCommand(db => db.Execute("""
            UPDATE AcceptedPrivacyPolicy
            SET LastConnected = DATETIME('now')
            WHERE Version = @Version
            """, new { Version = privacyPolicy }));
    }

    /// <summary>
    ///     Loads config file from disk, or resets the loaded config to default if the config doesn't exist on disk.
    /// </summary>
    public void Load()
    {
        InitializeCVars();

        using var connection = new SqliteConnection(GetCfgDbConnectionString());
        connection.Open();

        var sw = Stopwatch.StartNew();
        var success = Migrator.Migrate(connection, "SS14.Launcher.Models.Data.Migrations");

        if (!success)
            throw new Exception("Migrations failed!");

        Log.Debug("Did migrations in {MigrationTime}", sw.Elapsed);

        // Load from SQLite DB.
        LoadSqliteConfig(connection);

        if (SelectedLoginId is { } selectedLoginId && !_logins.Lookup(selectedLoginId).HasValue)
        {
            Log.Warning("SelectedLogin points to missing login {SelectedLoginId}. Resetting it.", selectedLoginId);
            SetCVar(CVars.SelectedLogin, "");
        }

        //if (GetCVar(CVars.Fingerprint) == "")
        //{
            // If we don't have a fingerprint yet this is either a fresh config or an older config.
            // Generate a fingerprint and immediately save it to disk.
            //SetCVar(CVars.Fingerprint, Guid.NewGuid().ToString());
        //}
        EnsureHwids();
        CommitConfig();
    }

    private void LoadSqliteConfig(SqliteConnection sqliteConnection)
    {
        // Load logins.
        _logins.AddOrUpdate(
            sqliteConnection.Query<(Guid id, string name, string token, string? modernhwid, string? legacyhwid, DateTimeOffset expires)>(
                    """
                    SELECT l.UserId, l.UserName, l.Token, h.ModernHWId, h.LegacyHWId, l.Expires
                    FROM Login AS l
                    LEFT JOIN LoginHwid AS h ON h.UserId = l.UserId
                    """)
                .Select(l => new LoginInfo
                {
                    UserId = l.id,
                    Username = l.name,
                    Token = new LoginToken(l.token, l.expires),
                    ModernHWId = l.modernhwid ?? "",
                    LegacyHWId = l.legacyhwid ?? ""
                }));

        // Favorites
        _favoriteServers.AddOrUpdate(
            sqliteConnection.Query<(string addr, string name, DateTimeOffset raiseTime)>(
                    "SELECT Address,Name,RaiseTime FROM FavoriteServer")
                .Select(l => new FavoriteServer(l.name, l.addr, l.raiseTime)));

        // Engine installations
        _engineInstallations.AddOrUpdate(
            sqliteConnection.Query<InstalledEngineVersion>("SELECT Version,Signature FROM EngineInstallation"));

        // Engine modules
        var loadedModules = sqliteConnection.Query<InstalledEngineModule>("SELECT Name, Version FROM EngineModule");
        foreach (var module in loadedModules)
        {
            _modules.Add(module);
            _moduleLookup.Add(new EngineModuleKey(module.Name, module.Version));
        }

        // Load CVars.
        var configRows = sqliteConnection.Query<(string, object?)>("SELECT Key, Value FROM Config");
        foreach (var (k, v) in configRows)
        {
            if (!_configEntries.TryGetValue(k, out var entry))
                continue;

            try
            {
                if (entry.Type == typeof(string))
                    Set(v as string ?? v?.ToString() ?? string.Empty);
                else if (entry.Type == typeof(bool))
                    Set(ToBool(v));
                else if (entry.Type == typeof(int))
                    Set(ToInt(v));
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to load config key '{ConfigKey}' (db type: {DbType})", k, v?.GetType().Name ?? "null");
            }

            void Set<T>(T value) => ((CVarEntry<T>)entry).ValueInternal = value;
        }

        _filters.UnionWith(sqliteConnection.Query<ServerFilter>("SELECT Category, Data FROM ServerFilter"));
        _hubs.AddRange(sqliteConnection.Query<Hub>("SELECT Address,Priority FROM Hub"));

        /* Закоментированная помоещьная политика
                foreach (var (identifier, version) in sqliteConnection.Query<(string, string)>(
                     "SELECT Identifier, Version FROM AcceptedPrivacyPolicy"))
        {
            _acceptedPrivacyPolicies[identifier] = version;
        }
        */

        // Avoid DB commands from config load.
        _dbCommandQueue.Clear();
    }

    private static int ToInt(object? value)
    {
        if (value is DBNull or null)
            return 0;

        return value switch
        {
            int i => i,
            long l => checked((int)l),
            bool b => b ? 1 : 0,
            string s when int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            string s when long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong) => checked((int)parsedLong),
            string s when bool.TryParse(s.Trim(), out var parsedBool) => parsedBool ? 1 : 0,
            string s when s.Trim().Equals("dark", StringComparison.OrdinalIgnoreCase) => 0,
            string s when s.Trim().Equals("light", StringComparison.OrdinalIgnoreCase) => 1,
            string s when s.Trim().Equals("darkred", StringComparison.OrdinalIgnoreCase) => 2,
            string s when s.Trim().Equals("dark_red", StringComparison.OrdinalIgnoreCase) => 2,
            string s when s.Trim().Equals("darkpurple", StringComparison.OrdinalIgnoreCase) => 3,
            string s when s.Trim().Equals("dark_purple", StringComparison.OrdinalIgnoreCase) => 3,
            string s when s.Trim().Equals("midnightblue", StringComparison.OrdinalIgnoreCase) => 4,
            string s when s.Trim().Equals("midnight_blue", StringComparison.OrdinalIgnoreCase) => 4,
            string s when s.Trim().Equals("emeralddusk", StringComparison.OrdinalIgnoreCase) => 5,
            string s when s.Trim().Equals("emerald_dusk", StringComparison.OrdinalIgnoreCase) => 5,
            string s when s.Trim().Equals("coppernight", StringComparison.OrdinalIgnoreCase) => 6,
            string s when s.Trim().Equals("copper_night", StringComparison.OrdinalIgnoreCase) => 6,
            string s when s.Trim().Equals("eyeburner3000", StringComparison.OrdinalIgnoreCase) => 1,
            string s when s.Trim().Equals("eye_burner_3000", StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };
    }

    private static bool ToBool(object? value)
    {
        if (value is DBNull or null)
            return false;

        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s when bool.TryParse(s.Trim(), out var parsed) => parsed,
            string s when int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) => parsedInt != 0,
            string s when s.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) => true,
            string s when s.Trim().Equals("no", StringComparison.OrdinalIgnoreCase) => false,
            _ => false
        };
    }

    private void InitializeCVars()
    {
        Debug.Assert(_configEntries.Count == 0);

        var baseMethod = typeof(DataManager)
            .GetMethod(nameof(CreateEntry), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var field in typeof(CVars).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            if (!field.FieldType.IsAssignableTo(typeof(CVarDef)))
                continue;

            var def = (CVarDef)field.GetValue(null)!;
            var method = baseMethod.MakeGenericMethod(def.ValueType);
            _configEntries.Add(def.Name, (CVarEntry)method.Invoke(this, new object?[] { def })!);
        }
    }

    private CVarEntry<T> CreateEntry<T>(CVarDef<T> def)
    {
        return new CVarEntry<T>(this, def);
    }

    [SuppressMessage("ReSharper", "UseAwaitUsing")]
    public async void CommitConfig()
    {
        if (_dbCommandQueue.Count == 0)
            return;

        var commands = _dbCommandQueue.ToArray();
        _dbCommandQueue.Clear();
        Log.Debug("Committing config to disk, running {DbCommandCount} commands", commands.Length);

        await Task.Run(async () =>
        {
            // SQLite is thread safe and won't have any problems with having multiple writers
            // (but they'll be synchronous).
            // That said, we need something to wait on when we shut down to make sure everything is written, so.
            await _dbWritingSemaphore.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(GetCfgDbConnectionString());
                connection.Open();
                using var transaction = connection.BeginTransaction();

                foreach (var cmd in commands)
                {
                    cmd(connection);
                }

                var sw = Stopwatch.StartNew();
                transaction.Commit();
                Log.Debug("Commit took: {CommitElapsed}", sw.Elapsed);
            }
            finally
            {
                _dbWritingSemaphore.Release();
            }
        });
    }

    public void Close()
    {
        CommitConfig();
        // Wait for any DB writes to finish to make sure we commit everything.
        _dbWritingSemaphore.Wait();
    }

    private static string GetCfgDbConnectionString()
    {
        var path = Path.Combine(LauncherPaths.DirUserData, "settings.db");
        return $"Data Source={path};Mode=ReadWriteCreate";
    }

    private void AddDbCommand(DbCommand cmd)
    {
        _dbCommandQueue.Add(cmd);
    }

    private void ChangeFavoriteServer(ChangeReason reason, FavoriteServer server)
    {
        // Make immutable copy to avoid race condition bugs.
        var data = new
        {
            server.Address,
            server.RaiseTime,
            server.Name
        };
        AddDbCommand(con =>
        {
            con.Execute(reason switch
                {
                    ChangeReason.Add => "INSERT INTO FavoriteServer VALUES (@Address, @Name, @RaiseTime)",
                    ChangeReason.Update => "UPDATE FavoriteServer SET Name = @Name, RaiseTime = @RaiseTime WHERE Address = @Address",
                    ChangeReason.Remove => "DELETE FROM FavoriteServer WHERE Address = @Address",
                    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
                },
                data
            );
        });
    }

    public void ChangeLogin(ChangeReason reason, LoginInfo login)
    {
        // Make immutable copy to avoid race condition bugs.
        var data = new
        {
            login.UserId,
            UserName = login.Username,
            Token = login.Token.Token,
            Expires = login.Token.ExpireTime,
            login.ModernHWId,
            login.LegacyHWId
        };
        AddDbCommand(con =>
        {
            switch (reason)
            {
                case ChangeReason.Add:
                    con.Execute(
                        "INSERT INTO Login (UserId, UserName, Token, Expires) VALUES (@UserId, @UserName, @Token, @Expires)",
                        data);
                    con.Execute(
                        "INSERT OR REPLACE INTO LoginHwid (UserId, ModernHWId, LegacyHWId) VALUES (@UserId, @ModernHWId, @LegacyHWId)",
                        data);
                    break;
                case ChangeReason.Update:
                    con.Execute(
                        "UPDATE Login SET UserName = @UserName, Token = @Token, Expires = @Expires WHERE UserId = @UserId",
                        data);
                    con.Execute(
                        "INSERT OR REPLACE INTO LoginHwid (UserId, ModernHWId, LegacyHWId) VALUES (@UserId, @ModernHWId, @LegacyHWId)",
                        data);
                    break;
                case ChangeReason.Remove:
                    con.Execute("DELETE FROM LoginHwid WHERE UserId = @UserId", data);
                    con.Execute("DELETE FROM Login WHERE UserId = @UserId", data);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
        });
    }

    private void ChangeEngineInstallation(ChangeReason reason, InstalledEngineVersion engine)
    {
        AddDbCommand(con => con.Execute(reason switch
            {
                ChangeReason.Add => "INSERT INTO EngineInstallation VALUES (@Version, @Signature)",
                ChangeReason.Update =>
                    "UPDATE EngineInstallation SET Signature = @Signature WHERE Version = @Version",
                ChangeReason.Remove => "DELETE FROM EngineInstallation WHERE Version = @Version",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
            },
            // Already immutable.
            engine
        ));
    }

    public T GetCVar<T>([ValueProvider("SS14.Launcher.Models.Data.CVars")] CVarDef<T> cVar)
    {
        var entry = (CVarEntry<T>)_configEntries[cVar.Name];
        return entry.Value;
    }

    public ICVarEntry<T> GetCVarEntry<T>([ValueProvider("SS14.Launcher.Models.Data.CVars")] CVarDef<T> cVar)
    {
        return (CVarEntry<T>)_configEntries[cVar.Name];
    }

    public void SetCVar<T>([ValueProvider("SS14.Launcher.Models.Data.CVars")] CVarDef<T> cVar, T value)
    {
        var name = cVar.Name;
        var entry = (CVarEntry<T>)_configEntries[cVar.Name];
        if (EqualityComparer<T>.Default.Equals(entry.ValueInternal, value))
            return;

        entry.ValueInternal = value;
        entry.FireValueChanged();

        AddDbCommand(con => con.Execute(
            "INSERT OR REPLACE INTO Config VALUES (@Key, @Value)",
            new
            {
                Key = name,
                Value = value
            }));
    }

    private abstract class CVarEntry
    {
        public abstract Type Type { get; }
    }

    private sealed class CVarEntry<T> : CVarEntry, ICVarEntry<T>
    {
        private readonly DataManager _mgr;
        private readonly CVarDef<T> _cVar;

        public CVarEntry(DataManager mgr, CVarDef<T> cVar)
        {
            _mgr = mgr;
            _cVar = cVar;
            ValueInternal = cVar.DefaultValue;
        }

        public override Type Type => typeof(T);

        public event PropertyChangedEventHandler? PropertyChanged;

        public T Value
        {
            get => ValueInternal;
            set => _mgr.SetCVar(_cVar, value);
        }

        public T ValueInternal;

        public void FireValueChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    private readonly record struct EngineModuleKey(string Name, string Version);

    private sealed class ServerFilterCollection : ICollection<ServerFilter>
    {
        private readonly DataManager _parent;

        public ServerFilterCollection(DataManager parent)
        {
            _parent = parent;
        }

        public IEnumerator<ServerFilter> GetEnumerator() => _parent._filters.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(ServerFilter item)
        {
            if (!_parent._filters.Add(item))
                return;

            _parent.AddDbCommand(cmd => cmd.Execute(
                "INSERT INTO ServerFilter (Category, Data) VALUES (@Category, @Data)",
                new { item.Category, item.Data}));
        }

        public void Clear()
        {
            _parent._filters.Clear();

            _parent.AddDbCommand(cmd => cmd.Execute("DELETE FROM ServerFilter"));
        }

        public bool Remove(ServerFilter item)
        {
            if (!_parent._filters.Remove(item))
                return false;

            _parent.AddDbCommand(cmd => cmd.Execute(
                "DELETE FROM ServerFilter WHERE Category = @Category AND Data = @Data",
                new { item.Category, item.Data}));

            return true;
        }

        public bool Contains(ServerFilter item) => _parent._filters.Contains(item);
        public void CopyTo(ServerFilter[] array, int arrayIndex) => _parent._filters.CopyTo(array, arrayIndex);
        public int Count => _parent._filters.Count;
        public bool IsReadOnly => false;
    }

    private sealed class HubCollection : ICollection<Hub>
    {
        private readonly DataManager _parent;

        public HubCollection(DataManager parent)
        {
            _parent = parent;
        }

        public IEnumerator<Hub> GetEnumerator() => _parent._hubs.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Hub item)
        {
            _parent._hubs.Add(item);

            _parent.AddDbCommand(cmd => cmd.Execute(
            "INSERT INTO Hub (Address, Priority) VALUES (@Address, @Priority)",
            new { item.Address, item.Priority }));
        }

        public void Clear()
        {
            _parent._hubs.Clear();

            _parent.AddDbCommand(cmd => cmd.Execute("DELETE FROM Hub"));
        }

        public bool Remove(Hub item)
        {
            if (!_parent._hubs.Remove(item))
                return false;

            _parent.AddDbCommand(cmd => cmd.Execute(
                "DELETE FROM Hub WHERE Address = @Address",
                new { item.Address, item.Priority }));

            return true;
        }

        public void CopyTo(Hub[] array, int arrayIndex) => _parent._hubs.CopyTo(array, arrayIndex);
        public bool Contains(Hub item) => _parent._hubs.Contains(item);
        public int Count => _parent._hubs.Count;
        public bool IsReadOnly => false;

    }

    public void EnsureHwids()
    {
        bool anyChanged = false;
        foreach (var login in _logins.Items)
        {
            bool userChanged = false;
            if (string.IsNullOrEmpty(login.ModernHWId))
            {
                login.ModernHWId = Marsey.Game.Patches.HWID.GenerateRandom();
                userChanged = true;
            }
            if (string.IsNullOrEmpty(login.LegacyHWId))
            {
                login.LegacyHWId = Marsey.Game.Patches.HWID.GenerateRandom();
                userChanged = true;
            }

            if (userChanged)
            {
                this.ChangeLogin(ChangeReason.Update, login);
                Log.Information("Auto-assigned unique HWIDs to {User}", login.Username);
                anyChanged = true;
            }
        }
        if (anyChanged)
        {
            this.CommitConfig();
        }
    }
}

public record FavoritesChanged;

public record ServerListDisplaySettingsChanged;

public record TitleRandomizationChanged;

public record HeaderRandomizationChanged;

public sealed record LauncherInstallRequested(SS14.Launcher.Models.LauncherSelfUpdateInfo UpdateInfo);
