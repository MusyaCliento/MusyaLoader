using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SS14.Launcher.Marseyverse.Engines;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.EngineManager;

public sealed class EngineManagerCustom : IEngineManager
{
    private readonly EngineManagerDynamic _inner;

    public EngineManagerCustom(EngineManagerDynamic inner)
    {
        _inner = inner;
    }

    public string GetEnginePath(string engineVersion)
    {
        var selected = CustomEngineRegistry.GetSelectedEngine();
        if (selected is { CanUse: true })
            return selected.ClientZipPath!;

        return _inner.GetEnginePath(engineVersion);
    }

    public string GetEngineSignature(string engineVersion)
    {
        var selected = CustomEngineRegistry.GetSelectedEngine();
        if (selected != null)
        {
            return string.IsNullOrWhiteSpace(selected.Signature) ? "DEADBEEF" : selected.Signature;
        }

        return _inner.GetEngineSignature(engineVersion);
    }

    public Task<EngineModuleManifest> GetEngineModuleManifest(CancellationToken cancel = default)
        => _inner.GetEngineModuleManifest(cancel);

    public Task<EngineInstallationResult> DownloadEngineIfNecessary(
        string engineVersion,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
        => _inner.DownloadEngineIfNecessary(engineVersion, progress, cancel);

    public Task<bool> DownloadModuleIfNecessary(
        string moduleName,
        string moduleVersion,
        EngineModuleManifest manifest,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
        => _inner.DownloadModuleIfNecessary(moduleName, moduleVersion, manifest, progress, cancel);

    public Task DoEngineCullMaybeAsync(SqliteConnection contenCon)
        => _inner.DoEngineCullMaybeAsync(contenCon);

    public void ClearAllEngines() => _inner.ClearAllEngines();

    public string GetEngineModule(string moduleName, string moduleVersion)
        => _inner.GetEngineModule(moduleName, moduleVersion);
}
