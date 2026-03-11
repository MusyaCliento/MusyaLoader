using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using DynamicData;
using ReactiveUI;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;

using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;
// --- MARSEY PATCH BEGIN ---
using Marsey.Config;
using Marsey.Game.Patches;
using Marsey.IPC;
using Marsey.Stealthsey;
using Marsey.Misc;
// --- MARSEY PATCH END ---

namespace SS14.Launcher.Models;

/// <summary>
/// Responsible for actually launching the game.
/// Either by connecting to a game server, or by launching a local content bundle.
/// </summary>
public partial class Connector : ReactiveObject
{
    private readonly Updater _updater;
    private readonly DataManager _cfg;
    private readonly LoginManager _loginManager;
    private readonly IEngineManager _engineManager;

    private ConnectionStatus _status = ConnectionStatus.None;
    private bool _clientExitedBadly;
    private string? _launchFailureReason;

    private readonly HttpClient _http;
    private Process? _udpRelayProcess;
    private bool _udpRelayIndependent;

    // --- MARSEY PATCH BEGIN ---
    private string? _forkid;
    private string? _engine;
    // --- MARSEY PATCH END ---

    private TaskCompletionSource<PrivacyPolicyAcceptResult>? _acceptPrivacyPolicyTcs;
    private ServerPrivacyPolicyInfo? _serverPrivacyPolicyInfo;
    private bool _privacyPolicyDifferentVersion;

    public Connector()
    {
        _updater = Locator.Current.GetRequiredService<Updater>();
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginManager = Locator.Current.GetRequiredService<LoginManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    public ConnectionStatus Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public bool ClientExitedBadly
    {
        get => _clientExitedBadly;
        private set => this.RaiseAndSetIfChanged(ref _clientExitedBadly, value);
    }

    public string? LaunchFailureReason
    {
        get => _launchFailureReason;
        private set => this.RaiseAndSetIfChanged(ref _launchFailureReason, value);
    }

    public ServerPrivacyPolicyInfo? PrivacyPolicyInfo => _serverPrivacyPolicyInfo;
    public bool PrivacyPolicyDifferentVersion
    {
        get => _privacyPolicyDifferentVersion;
        private set => this.RaiseAndSetIfChanged(ref _privacyPolicyDifferentVersion, value);
    }

    public async void Connect(string address, CancellationToken cancel = default)
    {
        LaunchFailureReason = null;
        try
        {
            await ConnectInternalAsync(address, cancel);
        }
        catch (ConnectException e)
        {
            Log.Error(e, "Failed to connect: {status}", e.Status);
            Status = e.Status;
        }
        catch (OperationCanceledException e)
        {
            Log.Information(e, "Cancelled connect");
            Status = ConnectionStatus.Cancelled;
        }
        finally
        {
            Cleanup();
        }
    }

    public async void LaunchContentBundle(IStorageFile file, CancellationToken cancel = default)
    {
        Log.Information("Launching content bundle: {FileName}", file.Path);
        LaunchFailureReason = null;

        try
        {
            await LaunchContentBundleInternal(file, cancel);
        }
        catch (ConnectException e)
        {
            Log.Error(e, "Failed to launch: {status}", e.Status);
            Status = e.Status;
        }
        catch (OperationCanceledException e)
        {
            Log.Information(e, "Cancelled launch");
            Status = ConnectionStatus.Cancelled;
        }
        finally
        {
            Cleanup();
        }
    }

    private async Task ConnectInternalAsync(string address, CancellationToken cancel)
    {
        Log.Warning("========== SERVER CONNECT START: {Address} ==========", address);
        Status = ConnectionStatus.Connecting;

    var (info, parsedAddr, infoAddr) = await GetServerInfoAsync(address, cancel);

    await HandlePrivacyPolicyAsync(info, cancel);

    // Run update.
    Status = ConnectionStatus.Updating;

    // Must have been set when retrieving build info (inferred to be automatic zipping).
    Debug.Assert(info.BuildInformation != null, "info.BuildInformation != null");

    var installation = await RunUpdateAsync(info.BuildInformation, cancel);

    var connectAddress = GetConnectAddress(info, infoAddr);
    connectAddress = TryStartUdpRelayIfNeeded(connectAddress);

    await LaunchClientWrap(installation, info, info.BuildInformation, connectAddress, parsedAddr, false, cancel);
        Log.Warning("========== SERVER CONNECT END: {Address} ==========", address);
    }

    private async Task HandlePrivacyPolicyAsync(ServerInfo info, CancellationToken cancel)
    {
        // Skip privacy policy check if configured
        if (_cfg.GetCVar(CVars.SkipPrivacyPolicy))
        {
            Log.Debug("Skipping privacy policy check (SkipPrivacyPolicy is enabled)");
            return;
        }

        if (info.PrivacyPolicy == null)
        {
            // Server has no privacy policy configured, nothing to do.
            return;
        }

        var identifier = info.PrivacyPolicy.Identifier;
        var version = info.PrivacyPolicy.Version;

        if (_cfg.HasAcceptedPrivacyPolicy(identifier, out var acceptedVersion))
        {
            if (version == acceptedVersion)
            {
                Log.Debug(
                    "User has previously accepted privacy policy {Identifier} with version {Version}",
                    identifier,
                    acceptedVersion);

                // User has previously accepted privacy policy, update last connected time in DB at least.
                _cfg.UpdateConnectedToPrivacyPolicy(identifier);
                _cfg.CommitConfig();
                return;
            }
            else
            {
                Log.Debug("User previously accepted privacy policy but version has changed!");
                PrivacyPolicyDifferentVersion = true;
            }
        }

        // Ask user for privacy policy acceptance by waiting here.
        Log.Debug("Prompting user for privacy policy acceptance: {Identifer} version {Version}", identifier, version);
        _serverPrivacyPolicyInfo = info.PrivacyPolicy;
        _acceptPrivacyPolicyTcs = new TaskCompletionSource<PrivacyPolicyAcceptResult>();

        Status = ConnectionStatus.AwaitingPrivacyPolicyAcceptance;
        var result = await _acceptPrivacyPolicyTcs.Task.WaitAsync(cancel);

        if (result == PrivacyPolicyAcceptResult.Accepted)
        {
            // Yippee they're ok with it.
            Log.Debug("User accepted privacy policy");
            _cfg.AcceptPrivacyPolicy(identifier, version);
            _cfg.CommitConfig();
            return;
        }

        // They're not ok with it. Just throw cancellation so the code cleans up I guess.
        // We could just have the connection screen treat "deny" as a cancellation op directly,
        // but that would make the logs less clear.
        Log.Information("User denied privacy policy, cancelling connection attempt!");
        throw new OperationCanceledException();
    }

    public void ConfirmPrivacyPolicy(PrivacyPolicyAcceptResult result)
    {
        if (_acceptPrivacyPolicyTcs == null)
        {
            Log.Error("_acceptPrivacyPolicyTcs is null???");
            return;
        }

        _acceptPrivacyPolicyTcs.SetResult(result);
    }

    private void Cleanup()
    {
        _serverPrivacyPolicyInfo = null;
        _acceptPrivacyPolicyTcs = null;
        PrivacyPolicyDifferentVersion = default;

        if (_udpRelayProcess != null && !_udpRelayIndependent)
        {
            try
            {
                if (!_udpRelayProcess.HasExited)
                    _udpRelayProcess.Kill(true);
            }
            catch
            {
            }
            finally
            {
                _udpRelayProcess = null;
            }
        }
    }

    private async Task LaunchContentBundleInternal(IStorageFile file, CancellationToken cancel)
    {
        Status = ConnectionStatus.Updating;

        ContentLaunchInfo installation;
        await using (var zipStream = await file.OpenReadAsync())
        {
            var zipHash = await Task.Run(() => Updater.HashFileSha256(zipStream), cancel);

            zipStream.Seek(0, SeekOrigin.Begin);

            using var zipFile = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var metadataJson = zipFile.GetEntry("rt_content_bundle.json");
            if (metadataJson == null)
            {
                Log.Error("Zip file did not contain rt_content_bundle.json");
                throw new ConnectException(ConnectionStatus.NotAContentBundle);
            }

            ContentBundleMetadata? metadata;
            using (var metadataStream = metadataJson.Open())
            {
                metadata = JsonSerializer.Deserialize<ContentBundleMetadata>(metadataStream);
            }

            if (metadata == null)
            {
                Log.Error("rt_content_bundle.json deserialized as null");
                throw new ConnectException(ConnectionStatus.NotAContentBundle);
            }

            Log.Debug("Loaded metadata for content bundle, continuing with launch");

            //
            // Big comment time
            //
            // Originally, I wanted to implement content bundles by not touching the Content DB at all.
            // (At least, if you're not using a base build)
            // The loader would open the zip file directly and provide the engine with both files simultaneously.
            //
            // That all kinda fell apart when I realized that manifest.yml has to be interpreted by the launcher.
            // And then also stuff like dependent engine versions have to be tracked and all that.
            // So, instead we merge the provided content bundle into the Content DB and start the game as normal.
            //
            // I don't like this solution much, as content bundles for SS14 replays will be quite bug (150+ MB).
            // It's a lot of data that needs to get uselessly shoved between the Content DB.
            //
            // In the future, a "hybrid" mode may be best:
            // The launcher will create a new version in the Content DB that contains just the manifest.yml.
            // (or base build data overlaid if necessary)
            // The loader would still be in charge of transparently merging in the zip file at runtime.

            //
            // EXCEPT!
            // SS14 replays, the biggest files, don't have a manifest.yml! So that above comment is all for naught!
            // We only ingest into the ContentDB if there isn't a manifest.yml and there *is* a base build.
            // Why this set of requirements? ...because it's the least intrusive to make SS14 replays better.
            // Also, we need to actually be able to access the zip as a path to give it to the launcher.
            //
            if (zipFile.GetEntry("manifest.yml") is null
                && metadata.BaseBuild is not null
                && file.TryGetLocalPath() is { } localPath)
            {
                installation = await RunUpdateAsync(metadata.GetBaseBuildInformation(), cancel);
                installation = installation with { OverlayZip = localPath };
            }
            else
            {
                installation = await InstallContentBundleAsync(zipFile, zipHash, metadata, cancel);
            }

            if (metadata.ServerGC == true)
                installation = installation with { ServerGC = true };
        }

        Log.Debug("Launching client");

        // I originally wanted to pass through build info,
        // but then realized I'd need to pipe the entries in the SQLite DB ("AnonymousContentBundle") up and ehhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh.
        await LaunchClientWrap(installation, null, null, null, null, true, cancel);
    }

    private async Task LaunchClientWrap(
        ContentLaunchInfo launchInfo,
        ServerInfo? info = null,
        ServerBuildInformation? buildInfo = null,
        Uri? connectAddress = null,
        Uri? parsedAddr = null,
        bool contentBundle = false,
        CancellationToken cancel = default)
    {
        Status = ConnectionStatus.StartingClient;
        bool isConnectionLaunch = info != null || connectAddress != null || parsedAddr != null;

        var clientProc = await ConnectLaunchClient(launchInfo, info, buildInfo, connectAddress, parsedAddr, contentBundle);

        if (clientProc != null)
        {
            // Wait 300ms, if the client exits with a bad error code before that it's probably fucked.
            var waitClient = clientProc.WaitForExitAsync(cancel);
            var waitDelay = Task.Delay(300, cancel);

            await Task.WhenAny(waitDelay, waitClient);

            if (!clientProc.HasExited)
            {
                Status = ConnectionStatus.ClientRunning;
                await waitClient;
                return;
            }

            ClientExitedBadly = clientProc.ExitCode != 0;
        }
        else
        {
            ClientExitedBadly = true;
        }

        Status = isConnectionLaunch && ClientExitedBadly
            ? ConnectionStatus.ConnectionFailed
            : ConnectionStatus.ClientExited;
    }

    private async Task<Process?> ConnectLaunchClient(ContentLaunchInfo launchInfo,
        ServerInfo? info,
        ServerBuildInformation? serverBuildInformation,
        Uri? connectAddress,
        Uri? parsedAddr,
        bool contentBundle)
    {

        var cVars = new List<(string, string)>();

        // --- MARSEY PATCH BEGIN ---
        if (_loginManager.ActiveAccount != null && _loginManager.ActiveAccount.Status != AccountLoginStatus.Guest)
            await _loginManager.UpdateSingleAccountStatus(_loginManager.ActiveAccount!);
        // --- MARSEY PATCH END ---

        if (info != null && info.AuthInformation.Mode != AuthMode.Disabled && _loginManager.ActiveAccount != null
            // --- MARSEY PATCH BEGIN ---
            && _loginManager.ActiveAccount.Status != AccountLoginStatus.Guest)
            // --- MARSEY PATCH END ---
        {
            var account = _loginManager.ActiveAccount;

            cVars.Add(("ROBUST_AUTH_TOKEN", account.LoginInfo.Token.Token));
            cVars.Add(("ROBUST_AUTH_USERID", account.LoginInfo.UserId.ToString()));
            cVars.Add(("ROBUST_AUTH_PUBKEY", info.AuthInformation.PublicKey));
            cVars.Add(("ROBUST_AUTH_SERVER", ConfigConstants.AuthUrl.GetMostSuccessfulUrl()));
        }

        try
        {
            // --- MARSEY PATCH BEGIN ---
            string uname = _loginManager.ActiveAccount?.Status == AccountLoginStatus.Guest ? _cfg.GetCVar(CVars.GuestUsername)
                : _loginManager.ActiveAccount?.Username ?? ConfigConstants.FallbackUsername;
            // --- MARSEY PATCH END ---

            var compatMode = (_cfg.GetCVar(CVars.CompatMode) && !OperatingSystem.IsMacOS()) || CheckForceCompatMode();
            if (compatMode)
            {
                var robustVersion = launchInfo.ModuleInfo.Single(x => x.Module == "Robust").Version;
                var enginePath = _engineManager.GetEnginePath(robustVersion);
                var engineDir = Path.GetDirectoryName(enginePath);
                var eglPath = engineDir == null ? null : Path.Combine(engineDir, "libEGL.dll");

                if (string.IsNullOrWhiteSpace(eglPath) || !File.Exists(eglPath))
                {
                    LaunchFailureReason = $"Compatibility mode requires libEGL.dll, but it was not found ({eglPath ?? "unknown path"}). Disable compatibility mode or reinstall engine files.";
                    throw new ConnectException(ConnectionStatus.ConnectionFailed, new FileNotFoundException(LaunchFailureReason, eglPath));
                }
            }

            var args = new List<string>
            {
                // Pass username to launched client.
                // We don't load username from client_config.toml when launched via launcher.
                // --- MARSEY PATCH BEGIN ---
                "--username", uname,
                // --- MARSEY PATCH END ---

                // GLES2 forcing or using default fallback
                "--cvar", $"display.compat={compatMode}",

                // Tell game we are launcher
                "--cvar", "launch.launcher=true"
            };

            if (contentBundle)
            {
                args.Add("--cvar");
                args.Add("launch.content_bundle=true");
            }

            if (connectAddress != null)
            {
                // We are using the launcher. Don't show main menu etc..
                // Note: --launcher also implied --connect.
                // For this reason, content bundles do not set --launcher.
                args.Add("--launcher");

                args.Add("--connect-address");
                args.Add(connectAddress.ToString());
            }

            if (parsedAddr != null)
            {
                args.Add("--ss14-address");
                args.Add(parsedAddr.ToString());
            }

            // --- MARSEY PATCH BEGIN ---
            // Steal forkid for the dumper
            _forkid = serverBuildInformation?.ForkId;
            _engine = serverBuildInformation?.EngineVersion;
            // --- MARSEY PATCH END ---

            // Pass build info to client. Initally added for replays, it is now used for connecting on modern robust CDN versions.
            // If engine_version or manifest_hash is null, the client WILL fail to connect.
            // serverBuildInformation is only null in case of content bundles which shouldn't try to connect to live servers anyways

            BuildCVar("download_url", serverBuildInformation?.DownloadUrl);
            BuildCVar("manifest_url", serverBuildInformation?.ManifestUrl);
            BuildCVar("manifest_download_url", serverBuildInformation?.ManifestDownloadUrl);
            BuildCVar("version", serverBuildInformation?.Version);
            BuildCVar("fork_id", serverBuildInformation?.ForkId);
            BuildCVar("hash", serverBuildInformation?.Hash);
            BuildCVar("manifest_hash", serverBuildInformation?.ManifestHash);
            // --- MARSEY PATCH BEGIN ---
            // engine_version не нужен, клиент сам знает
            // --- MARSEY PATCH END ---

            void BuildCVar(string name, string? value)
            {
                if (value == null)
                    return;

                args.Add("--cvar");
                args.Add($"build.{name}={value}");
            }

            // --- MARSEY PATCH BEGIN ---
            return await LaunchClient(launchInfo, args, cVars);
            // --- MARSEY PATCH END ---
        }
        catch (Exception e)
        {
            Log.Error(e, "Exception while starting client");
            LaunchFailureReason ??= $"Failed to start client: {e.Message}";
            return null;
        }
    }

    private static Uri GetConnectAddress(ServerInfo info, Uri infoAddr)
    {
        if (string.IsNullOrEmpty(info.ConnectAddress))
        {
            // No connect address specified, use same address/port as base address.
            return new UriBuilder
            {
                Scheme = "udp",
                Host = infoAddr.Host,
                Port = infoAddr.Port
            }.Uri;
        }

        try
        {
            return new Uri(info.ConnectAddress);
        }
        catch (FormatException e)
        {
            Log.Error(e, "Failed to parse ConnectAddress");
            throw new ConnectException(ConnectionStatus.ConnectionFailed);
        }
    }

    private async Task<ContentLaunchInfo> RunUpdateAsync(ServerBuildInformation info, CancellationToken cancel)
    {
        var installation = await _updater.RunUpdateForLaunchAsync(info, cancel);
        if (installation == null)
        {
            throw new ConnectException(ConnectionStatus.UpdateError);
        }

        return installation;
    }

    private async Task<ContentLaunchInfo> InstallContentBundleAsync(
        ZipArchive archive,
        byte[] zipHash,
        ContentBundleMetadata metadata,
        CancellationToken cancel)
    {
        var installation = await _updater.InstallContentBundleForLaunchAsync(archive, zipHash, metadata, cancel);
        if (installation == null)
        {
            throw new ConnectException(ConnectionStatus.UpdateError);
        }

        return installation;
    }

    private async Task<(ServerInfo, Uri, Uri)> GetServerInfoAsync(string address, CancellationToken cancel)
    {
        if (!UriHelper.TryParseSs14Uri(address, out var parsedAddress))
        {
            Log.Error("Invalid URI in GetServerInfoAsync: {Uri}", address);
            throw new ConnectException(ConnectionStatus.ConnectionFailed);
        }

        // Fetch server connect info.
        var infoAddr = UriHelper.GetServerInfoAddress(parsedAddress);

        try
        {
            var info = await _http.GetFromJsonAsync<ServerInfo>(infoAddr, cancel) ?? throw new InvalidDataException();
            if (info.BuildInformation is {} buildInfo && (buildInfo.Acz || string.IsNullOrEmpty(buildInfo.DownloadUrl)))
            {
                var acz = info.BuildInformation.Acz;
                var apiAddress = UriHelper.GetServerApiAddress(parsedAddress);

                // Infer download URL to be self-hosted client address if not supplied
                // (The server may not know it's own address)
                info.BuildInformation.DownloadUrl = new Uri(apiAddress, "client.zip").ToString();

                if (acz)
                {
                    info.BuildInformation.ManifestUrl = new Uri(apiAddress, "manifest.txt").ToString();
                    info.BuildInformation.ManifestDownloadUrl = new Uri(apiAddress, "download").ToString();
                }
            }
            return (info, parsedAddress, infoAddr);
        }
        catch (Exception e) when (e is JsonException or HttpRequestException or InvalidDataException)
        {
            throw new ConnectException(ConnectionStatus.ConnectionFailed, e);
        }
    }

    public static InstalledEngineModule? GetInstalledModuleForEngineVersion(
        Version engineVersion,
        string moduleName,
        DataManager dataManager)
    {
        return dataManager.EngineModules
            .Where(m => m.Name == moduleName)
            .Select(m => new { Version = Version.Parse(m.Version), m })
            .Where(m => engineVersion >= m.Version)
            .MaxBy(m => m.Version)?.m;
    }

    // --- MARSEY PATCH BEGIN ---
    private async Task<Process?> LaunchClient(
        ContentLaunchInfo launchInfo,
        IEnumerable<string> extraArgs,
        List<(string, string)> env)
    {
        string engineVersion = launchInfo.ModuleInfo.Single(x => x.Module == "Robust").Version;
        ProcessStartInfo startInfo = await GetLoaderStartInfo(engineVersion, launchInfo.Version, env);
        LaunchFailureReason = null;

        // Abort if engine version hates us и мы не скрываемся
        if (Abjure.CheckMalbox(engineVersion, (HideLevel)_cfg.GetCVar(CVars.MarseyHide)))
        {
            Log.Error("Engine version over 183 with hidesey disabled, aborting.");
            return null;
        }

        // Prepare IPC pipes for Marsey without blocking process start.
        // Pipes must be opened before/around launch, but awaiting here deadlocks
        // because the loader is the one that connects to them.
        _ = Marsify().ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Log.Error(t.Exception, "Marsey IPC preparation failed");
            }
        }, TaskScheduler.Default);

        ConfigureEnvironmentVariables(startInfo, launchInfo, engineVersion);
        ConfigureLogging(startInfo);
        SetDynamicPgo(startInfo);
        UnfuckGlibcLinux(startInfo);
        ConfigureMultiWindow(launchInfo, startInfo);

        startInfo.UseShellExecute = false;
        startInfo.ArgumentList.AddRange(extraArgs);

        if (!File.Exists(startInfo.FileName))
        {
            LaunchFailureReason = $"Loader was not found: {startInfo.FileName}";
            throw new FileNotFoundException(LaunchFailureReason, startInfo.FileName);
        }

        Process? process = Process.Start(startInfo);
        if (process != null && _cfg.GetCVar(CVars.LogClient))
        {
            SetupManualPipeLogging(process);
        }

        return process;
    }

    private void ConfigureEnvironmentVariables(ProcessStartInfo startInfo, ContentLaunchInfo launchInfo, string engineVersion)
    {
        // Set environment variables for engine modules.
        foreach (var (moduleName, moduleVersion) in launchInfo.ModuleInfo)
        {
            if (moduleName == "Robust")
                continue;

            var modulePath = _engineManager.GetEngineModule(moduleName, moduleVersion);
            var envVar = $"ROBUST_MODULE_{moduleName.ToUpperInvariant().Replace('.', '_')}";
            startInfo.EnvironmentVariables[envVar] = modulePath;
        }

        // Set other necessary environment variables.
        startInfo.EnvironmentVariables["SS14_DISABLE_SIGNING"] = _cfg.GetCVar(CVars.DisableSigning) ? "true" : null;
        startInfo.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        startInfo.EnvironmentVariables["MARSEY_JUMP_LOADER_DEBUG"] = MarseyConf.JumpLoaderDebug ? "true" : null;

        ConfigureProxyEnvironment(startInfo);
    }

    private void ConfigureProxyEnvironment(ProcessStartInfo info)
    {
        if (!_cfg.GetCVar(CVars.LauncherProxyApplyToLoader))
        {
            ClearProxyEnv(info);
            return;
        }

        if (!Socks5ProxyHelper.TryReadProxyValues(_cfg, out var proxy, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error))
                Log.Warning("Proxy for loader is enabled but config is invalid: {Error}", error);
            ClearProxyEnv(info);
            return;
        }

        var proxyUri = proxy.ToProxyUriString(includeCredentials: true);
        info.EnvironmentVariables["ALL_PROXY"] = proxyUri;
        info.EnvironmentVariables["HTTP_PROXY"] = proxyUri;
        info.EnvironmentVariables["HTTPS_PROXY"] = proxyUri;
        info.EnvironmentVariables["NO_PROXY"] = "localhost,127.0.0.1";

        info.EnvironmentVariables["all_proxy"] = proxyUri;
        info.EnvironmentVariables["http_proxy"] = proxyUri;
        info.EnvironmentVariables["https_proxy"] = proxyUri;
        info.EnvironmentVariables["no_proxy"] = "localhost,127.0.0.1";
    }

    private static void ClearProxyEnv(ProcessStartInfo info)
    {
        string[] keys =
        [
            "ALL_PROXY", "HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY",
            "all_proxy", "http_proxy", "https_proxy", "no_proxy"
        ];

        foreach (var key in keys)
        {
            if (info.EnvironmentVariables.ContainsKey(key))
                info.EnvironmentVariables.Remove(key);
        }
    }

    private Uri TryStartUdpRelayIfNeeded(Uri connectAddress)
    {
        if (!_cfg.GetCVar(CVars.LauncherProxyUseUdpRelay))
            return connectAddress;

        if (!string.Equals(connectAddress.Scheme, "udp", StringComparison.OrdinalIgnoreCase))
            return connectAddress;

        if (!Socks5ProxyHelper.TryReadProxyValues(_cfg, out var proxyCfg, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error))
                Log.Warning("UDP relay enabled but proxy config invalid: {Error}", error);
            return connectAddress;
        }

        var listenHost = "127.0.0.1";
        var listenPort = GetFreeUdpPort();
        var targetHost = connectAddress.Host;
        var targetPort = connectAddress.Port;

        var proxyServicePath = GetProxyServiceExecutablePath();
        if (string.IsNullOrWhiteSpace(proxyServicePath) || !File.Exists(proxyServicePath))
        {
            Log.Warning("ProxyService executable not found: {Path}", proxyServicePath);
            return connectAddress;
        }

        var debug = _cfg.GetCVar(CVars.LauncherProxyServiceDebug);
        var independent = _cfg.GetCVar(CVars.LauncherProxyServiceIndependent);

        var psi = new ProcessStartInfo
        {
            FileName = proxyServicePath,
            UseShellExecute = false,
            CreateNoWindow = !debug
        };
        if (!debug)
        {
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
        }

        psi.ArgumentList.Add("--listen-host");
        psi.ArgumentList.Add(listenHost);
        psi.ArgumentList.Add("--listen-port");
        psi.ArgumentList.Add(listenPort.ToString());
        psi.ArgumentList.Add("--target-host");
        psi.ArgumentList.Add(targetHost);
        psi.ArgumentList.Add("--target-port");
        psi.ArgumentList.Add(targetPort.ToString());
        psi.ArgumentList.Add("--socks-host");
        psi.ArgumentList.Add(proxyCfg.Host);
        psi.ArgumentList.Add("--socks-port");
        psi.ArgumentList.Add(proxyCfg.Port.ToString());

        if (!string.IsNullOrWhiteSpace(proxyCfg.Username))
        {
            psi.ArgumentList.Add("--socks-user");
            psi.ArgumentList.Add(proxyCfg.Username);
            psi.ArgumentList.Add("--socks-pass");
            psi.ArgumentList.Add(proxyCfg.Password ?? "");
        }

        if (!independent)
        {
            psi.ArgumentList.Add("--parent-pid");
            psi.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
        }

        var process = Process.Start(psi);
        if (process != null)
        {
            _udpRelayProcess = process;
            _udpRelayIndependent = independent;

            if (!debug)
                PipeLogOutput(process);
        }

        var relayAddress = new UriBuilder("udp", listenHost, listenPort).Uri;
        Log.Information("UDP relay enabled: {From} -> {To}", connectAddress, relayAddress);
        return relayAddress;
    }

    private static int GetFreeUdpPort()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    private static string? GetProxyServiceExecutablePath()
    {
#if FULL_RELEASE
        var basePath = LauncherPaths.DirLauncherInstall;
        if (OperatingSystem.IsMacOS())
            return null;

        if (OperatingSystem.IsWindows())
            return Path.Combine(basePath, "SS14.ProxyService.exe");
        return Path.Combine(basePath, "SS14.ProxyService");
#else
#if RELEASE
        const string buildConfiguration = "Release";
#else
        const string buildConfiguration = "Debug";
#endif
        var basePath = Path.GetFullPath(Path.Combine(
            LauncherPaths.DirLauncherInstall,
            "..", "..", "..", "..",
            "SS14.ProxyService", "bin", buildConfiguration, "net10.0"));

        if (OperatingSystem.IsWindows())
            return Path.Combine(basePath, "SS14.ProxyService.exe");
        return Path.Combine(basePath, "SS14.ProxyService");
#endif
    }

    private void ConfigureLogging(ProcessStartInfo startInfo)
    {
        if (!_cfg.GetCVar(CVars.LogClient)) return;

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            startInfo.EnvironmentVariables["SS14_LOG_CLIENT"] = LauncherPaths.PathClientMacLog;
        }
    }

    private void SetDynamicPgo(ProcessStartInfo startInfo)
    {
        if (!_cfg.GetCVar(CVars.DynamicPgo)) return;

        Log.Debug("Dynamic PGO is enabled.");
        startInfo.EnvironmentVariables["DOTNET_TieredPGO"] = "1";
        startInfo.EnvironmentVariables["DOTNET_TC_QuickJitForLoops"] = "1";
        startInfo.EnvironmentVariables["DOTNET_ReadyToRun"] = "0";

        startInfo.EnvironmentVariables["DOTNET_gcServer"] = "1";
        startInfo.EnvironmentVariables["DOTNET_TieredCompilationMinCallCount"] = "0";
    }

    private void UnfuckGlibcLinux(ProcessStartInfo startInfo)
    {
        if (OperatingSystem.IsLinux())
        {
            // https://github.com/space-wizards/RobustToolbox/issues/2563
            startInfo.EnvironmentVariables["GLIBC_TUNABLES"] = "glibc.rtld.dynamic_sort=1";
        }
    }

    private void SetupManualPipeLogging(Process process)
    {
        // Set up manual-pipe logging for new client with PID.
        Log.Debug("Setting up manual-pipe logging for new client with PID {pid}.", process.Id);

        var fileStdout = new FileStream(
            LauncherPaths.PathClientStdoutLog,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Delete | FileShare.ReadWrite,
            4096,
            FileOptions.Asynchronous);

        var fileStderr = new FileStream(
            LauncherPaths.PathClientStderrLog,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Delete | FileShare.ReadWrite,
            4096,
            FileOptions.Asynchronous);

        File.Delete(LauncherPaths.PathClientStdmarseyLog);
        FileStream? fileStdmarsey = null;

        MarseyConf.SeparateLogger = _cfg.GetCVar(CVars.SeparateLogging);

        if (MarseyConf.MarseyHide < HideLevel.Explicit)
        {
            fileStdmarsey = new FileStream(
                LauncherPaths.PathClientStdmarseyLog,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Delete | FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous);
        }

        PipeOutput(process, fileStdout, fileStderr, fileStdmarsey);
    }

    private async Task<ProcessStartInfo> GetLoaderStartInfo(string engineVersion, long contentVersion, List<(string, string)> env)
    {
        var startInfo = await GetLoaderStartInfo();
        var binPath = _engineManager.GetEnginePath(engineVersion);
        var sig = _engineManager.GetEngineSignature(engineVersion);
        var pubKey = LauncherPaths.PathPublicKey;

        startInfo.ArgumentList.Add(binPath);
        startInfo.ArgumentList.Add(sig);
        startInfo.ArgumentList.Add(pubKey);

        foreach (var (k, v) in env)
        {
            startInfo.EnvironmentVariables[k] = v;
        }

        startInfo.EnvironmentVariables["SS14_LOADER_CONTENT_DB"] = LauncherPaths.PathContentDb;
        startInfo.EnvironmentVariables["SS14_LOADER_CONTENT_VERSION"] = contentVersion.ToString();
        startInfo.EnvironmentVariables["SS14_LAUNCHER_PATH"] = Process.GetCurrentProcess().MainModule!.FileName;

        if (_cfg.GetCVar(CVars.DisallowHwid))
            startInfo.EnvironmentVariables["ROBUST_AUTH_ALLOW_HWID"] = "0";

        return startInfo;
    }

    private async Task Marsify()
    {
        Log.Debug("Preparing patch assemblies.");
        Task modsTask = FileHandler.PrepareMods();
        Task confTask = ConfigureMarsey();
        await Task.WhenAll(modsTask, confTask);
        MarseyCleanup();
    }

    // TODO: Make this a json or something like holy shit
    private async Task ConfigureMarsey()
    {
        // Prepare environment variables
        Dictionary<string, string?> envVars = new Dictionary<string, string?>
        {
            { "MARSEY_LOGGING", _cfg.GetCVar(CVars.LogPatcher) ? "true" : null },
            { "MARSEY_LOADER_DEBUG", _cfg.GetCVar(CVars.LogLoaderDebug) ? "true" : null },
            { "MARSEY_LOADER_TRACE", _cfg.GetCVar(CVars.LogLoaderTrace) ? "true" : null },
            { "MARSEY_SEPARATE_LOGGER", _cfg.GetCVar(CVars.SeparateLogging) ? "true" : null },
            { "MARSEY_THROW_FAIL", _cfg.GetCVar(CVars.ThrowPatchFail) ? "true" : null },
            { "MARSEY_HIDE_LEVEL", $"{_cfg.GetCVar(CVars.MarseyHide)}" },
            { "MARSEY_JAMMER", _cfg.GetCVar(CVars.JamDials) ? "true" : null },
            { "MARSEY_DISABLE_REC", _cfg.GetCVar(CVars.Blackhole) ? "true" : null },
            { "MARSEY_DISABLE_PRESENCE", _cfg.GetCVar(CVars.DisableRPC) ? "true" : null },
            { "MARSEY_FAKE_PRESENCE", _cfg.GetCVar(CVars.FakeRPC) ? "true" : null },
            { "MARSEY_PRESENCE_USERNAME", _cfg.GetCVar(CVars.RPCUsername) },
            { "MARSEY_FORCINGHWID", _cfg.GetCVar(CVars.ForcingHWId) ? "true" : null },
            { "MARSEY_FORCEDHWID", _cfg.GetCVar(CVars.ForcingHWId) ? MarseyGetHWId() : null },
            { "MARSEY_FORCEDHWID_LEGACY", _cfg.GetCVar(CVars.ForcingHWId) ? MarseyGetLegacyHWId() : null },
            { "MARSEY_FLYI", _cfg.GetCVar(CVars.ForcingHWId) ? MarseyGetFlYi() : null },
            { "MARSEY_AUTODELETE_HWID", _cfg.GetCVar(CVars.AutoDeleteHWID) ? "true" : null },
            { "MARSEY_FORKID", _forkid },
            { "MARSEY_ENGINE", _engine },
            { "MARSEY_BACKPORTS", _cfg.GetCVar(CVars.Backports) ? "true" : null },
            { "MARSEY_NO_ANY_BACKPORTS", _cfg.GetCVar(CVars.DisableAnyEngineBackports) ? "true" : null },
            { "MARSEY_DISABLE_STRICT", _cfg.GetCVar(CVars.DisableStrict) ? "true" : null },
            { "MARSEY_DUMP_ASSEMBLIES", _cfg.GetCVar(CVars.DumpAssemblies) ? "true" : null },
            { "MARSEY_PATCHLESS", _cfg.GetCVar(CVars.Patchless) ? "true" : null }
        };

        // Serialize environment variables
        string serializedEnvVars = string.Join(";", envVars.Select(kv => $"{kv.Key}={kv.Value}"));

        await SendConfig(serializedEnvVars);
    }

    private string MarseyGetFlYi()
    {
        if (_loginManager.ActiveAccount != null)
        {
            return _loginManager.ActiveAccount.LoginInfo.UserId.ToString();
        }
        return string.Empty;
    }

    private async Task SendConfig(string config)
    {
        Server MarseyConfPipeServer = new Server();
        await MarseyConfPipeServer.ReadySend("MarseyConf", config);
    }

    private string MarseyGetLegacyHWId()
    {
        if (_cfg.GetCVar(CVars.LIHWIDBind) && _loginManager.ActiveAccount != null)
        {
            return _loginManager.ActiveAccount.LoginInfo.LegacyHWId;
        }
        return HWID.GenerateRandom();
    }

    private string MarseyGetHWId()
    {
        string forcedHWID = _cfg.GetCVar(CVars.ForcedHWId);
        if (_cfg.GetCVar(CVars.LIHWIDBind) && _loginManager.ActiveAccount != null)
        {
            forcedHWID = _loginManager.ActiveAccount.LoginInfo.ModernHWId;
        }

        Log.Debug($"Exiting with {forcedHWID}");
        return forcedHWID;
    }

    private void MarseyCleanup()
    {
        MarseyConf.Dumper = false;
    }

    private static async void PipeOutput(Process process, Stream targetStdout, Stream targetStderr, Stream? targetStdmarsey)
    {
        async Task DoPipe(StreamReader reader, Stream writer, Stream? marseyWriter = null)
        {
            var readStream = reader.BaseStream;
            var buf = new byte[4096];
            while (true)
            {
                var read = await readStream.ReadAsync(buf);
                if (read == 0)
                {
                    Log.Debug("EOF, ending pipe logging for {pid}.", process.Id);
                    return;
                }

                if (MarseyConf.SeparateLogger && marseyWriter != null && buf.AsSpan(0, read).StartsWith(Encoding.UTF8.GetBytes($"[{MarseyVars.MarseyLoggerPrefix}]")))
                {
                    await marseyWriter.WriteAsync(buf.AsMemory(0, read));
                    await marseyWriter.FlushAsync();
                }
                else
                    await writer.WriteAsync(buf.AsMemory(0, read));
            }
        }

        await Task.WhenAll(
            DoPipe(process.StandardOutput, targetStdout, targetStdmarsey),
            DoPipe(process.StandardError, targetStderr));
    }
    // --- MARSEY PATCH END ---

    private static void ConfigureMultiWindow(ContentLaunchInfo launchInfo, ProcessStartInfo startInfo)
    {
        // Implemented in private repo for Steam.
    }

    private static async void PipeOutput(Process process, Stream targetStdout, Stream targetStderr)
    {
        async Task DoPipe(StreamReader reader, Stream writer)
        {
            var readStream = reader.BaseStream;
            var buf = new byte[4096];
            while (true)
            {
                var read = await readStream.ReadAsync(buf);
                if (read == 0)
                {
                    Log.Debug("EOF, ending pipe logging for {pid}.", process.Id);
                    return;
                }

                await writer.WriteAsync(buf.AsMemory(0, read));
            }
        }

        await Task.WhenAll(
            DoPipe(process.StandardOutput, targetStdout),
            DoPipe(process.StandardError, targetStderr));
    }

    private static void PipeLogOutput(Process process)
    {
        Log.Debug("Piping output for process {pid} straight to logs", process.Id);

        async void DoPipe(TextReader reader)
        {
            while (true)
            {
                var read = await reader.ReadLineAsync();

                if (read == null)
                {
                    Log.Debug("EOF, ending pipe logging for {pid}", process.Id);
                    return;
                }

                Log.Information("piped: {content}", read);
            }
        }

        DoPipe(process.StandardError);
        DoPipe(process.StandardOutput);
    }

#pragma warning disable 162
    private static async Task<ProcessStartInfo> GetLoaderStartInfo()
    {
        string basePath;

#if FULL_RELEASE
            const bool release = true;
#else
        const bool release = false;
#endif

        if (release)
        {
            basePath = LauncherPaths.DirLauncherInstall;
            if (OperatingSystem.IsMacOS())
                basePath = Path.Combine(basePath, "..", "..");
            else
                basePath = Path.Combine(basePath, "loader");
        }
        else
        {
#if RELEASE
            const string buildConfiguration = "Release";
#else
            const string buildConfiguration = "Debug";
#endif
            basePath = Path.GetFullPath(Path.Combine(
                LauncherPaths.DirLauncherInstall,
                "..", "..", "..", "..",
                "SS14.Loader", "bin", buildConfiguration, "net10.0"));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            return new ProcessStartInfo
            {
                FileName = Path.Combine(basePath, "SS14.Loader")
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                FileName = Path.Combine(basePath, "SS14.Loader.exe"),
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (release)
            {
                var appPath = Path.GetFullPath(Path.Combine(basePath, "Space Station 14.app"));
                Log.Debug("Using app bundle: {appPath}", appPath);

                Log.Debug("Clearing quarantine on loader.");

                // Clear the quarantine attribute off the loader to avoid any funny business with failing to start it.
                // This seemed to ONLY BE A PROBLEM if the quarantined file in question
                // is inside a secured location like ~/Desktop is now on Catalina.
                // Fucking stupid since we can clearly just work around it like this...
                // Thank you, Blaisorblade on Ask Different
                // https://apple.stackexchange.com/questions/105155/denied-file-read-access-on-file-i-own-and-have-full-r-w-permissions-on
                var xattr = Process.Start(new ProcessStartInfo
                {
                    FileName = "xattr",
                    ArgumentList = {"-d", "com.apple.quarantine", appPath},
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                });

                if (xattr != null)
                    PipeLogOutput(xattr);

                await xattr.WaitForExitAsync();

                var startInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { appPath }
                };

                if (RuntimeInformation.OSArchitecture != Architecture.X64)
                {
                    // Intel macs may be running unsupported macOS versions without open --arch.
                    // So don't add it. It's not necessary anyways.

                    // Versions before Sonoma also don't have it.
                    // If you're on one of those... uhh.. Why are you running an outdated OS?
                    // But don't add --arch so that people on an outdated OS can still use native Apple Silicon.
                    if (OperatingSystem.IsMacOSVersionAtLeast(14))
                    {
                        startInfo.ArgumentList.Add("--arch");
                        startInfo.ArgumentList.Add(
                            RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x86_64");
                    }
                }

                startInfo.ArgumentList.Add("--args");

                return startInfo;
            }
            else
            {
                return new ProcessStartInfo
                {
                    FileName = Path.Combine(basePath, "SS14.Loader"),
                };
            }
        }

        throw new NotSupportedException("Unsupported platform.");
    }
#pragma warning restore 162

    public enum ConnectionStatus
    {
        None,
        Updating,
        UpdateError,
        Connecting,
        AwaitingPrivacyPolicyAcceptance,
        ConnectionFailed,
        StartingClient,
        ClientRunning,
        ClientExited,
        Cancelled,
        NotAContentBundle
    }

    private sealed class ConnectException : Exception
    {
        public ConnectionStatus Status { get; }

        public ConnectException(ConnectionStatus status)
        {
            Status = status;
        }

        public ConnectException(ConnectionStatus status, Exception inner)
            : base($"Failed to connect: {status}", inner)
        {
            Status = status;
        }
    }
}

public sealed record ContentBundleMetadata(
    [property: JsonPropertyName("server_gc")]
    bool? ServerGC,
    [property: JsonPropertyName("engine_version")]
    string EngineVersion,
    [property: JsonPropertyName("base_build")]
    ContentBundleBaseBuild? BaseBuild
)
{
    public ServerBuildInformation GetBaseBuildInformation()
    {
        if (BaseBuild == null)
            throw new InvalidOperationException("Metadata must have base build!");

        return new ServerBuildInformation
        {
            DownloadUrl = BaseBuild.DownloadUrl,
            ManifestUrl = BaseBuild.ManifestUrl,
            ManifestDownloadUrl = BaseBuild.ManifestDownloadUrl,
            EngineVersion = EngineVersion,
            Version = BaseBuild.Version,
            ForkId = BaseBuild.ForkId,
            Hash = BaseBuild.Hash,
            ManifestHash = BaseBuild.ManifestHash,
            Acz = false
        };
    }
}

public sealed record ContentBundleBaseBuild(
    [property: JsonPropertyName("fork_id")] string ForkId,
    [property: JsonPropertyName("version")] string Version,
    // Old zip-download system.
    [property: JsonPropertyName("download_url")] string? DownloadUrl,
    [property: JsonPropertyName("hash")] string? Hash,
    // Newer manifest download system.
    [property: JsonPropertyName("manifest_download_url")] string? ManifestDownloadUrl,
    [property: JsonPropertyName("manifest_url")] string? ManifestUrl,
    [property: JsonPropertyName("manifest_hash")] string? ManifestHash
);

public enum PrivacyPolicyAcceptResult
{
    Denied,
    Accepted,
}

