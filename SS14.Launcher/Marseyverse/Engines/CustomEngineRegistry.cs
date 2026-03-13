using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Marsey.Config;
using SS14.Launcher;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Marseyverse.Engines;

public static class CustomEngineRegistry
{
    private const string MetaFileName = "engine.json";
    private const string SidecarSuffix = ".engine.json";
    private const string SelectionFileName = "engine.marsey";
    private const string DefaultIcon = "avares://SS14.Launcher/Assets/marsey-icons/engines.png";

    public static List<CustomEngineInfo> ScanEngines()
    {
        var result = new List<CustomEngineInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetEngineRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                if (Path.GetFileName(dir).Equals(".cache", StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = LoadFromDirectory(dir);
                if (info != null && seen.Add(info.SourcePath))
                    result.Add(info);
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.zip", SearchOption.TopDirectoryOnly))
            {
                var info = LoadFromZipOrSidecar(file);
                if (info != null && seen.Add(info.SourcePath))
                    result.Add(info);
            }
        }

        return result.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static CustomEngineInfo? GetSelectedEngine()
    {
        var selectedPath = LoadSelection();
        if (string.IsNullOrWhiteSpace(selectedPath))
            return null;

        if (Directory.Exists(selectedPath))
            return LoadFromDirectory(selectedPath);

        if (File.Exists(selectedPath))
            return LoadFromZipOrSidecar(selectedPath);

        return null;
    }

    public static string? LoadSelection()
    {
        var path = Path.Combine(LauncherPaths.DirUserData, SelectionFileName);
        if (!File.Exists(path))
            return null;

        var value = File.ReadAllText(path).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static void SaveSelection(string? sourcePath)
    {
        var path = Path.Combine(LauncherPaths.DirUserData, SelectionFileName);
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        File.WriteAllText(path, sourcePath);
    }

    private static CustomEngineInfo? LoadFromDirectory(string dir)
    {
        var meta = TryReadMetaFromFile(Path.Combine(dir, MetaFileName));
        var name = meta?.Name ?? Path.GetFileName(dir);
        var description = meta?.Description ?? string.Empty;
        var icon = ResolveIcon(meta?.Icon, dir, fromZip: false);
        var signature = meta?.Signature;
        var clientZip = ResolveClientZip(dir, meta?.ClientZip);

        return new CustomEngineInfo(dir, name, description, icon, clientZip, signature);
    }

    private static CustomEngineInfo? LoadFromZipOrSidecar(string zipPath)
    {
        EngineMeta? meta = null;
        var sidecarPath = zipPath + SidecarSuffix;
        if (File.Exists(sidecarPath))
        {
            meta = TryReadMetaFromFile(sidecarPath);
        }
        else
        {
            meta = TryReadMetaFromZip(zipPath);
        }

        var name = meta?.Name ?? Path.GetFileNameWithoutExtension(zipPath);
        var description = meta?.Description ?? string.Empty;
        var icon = ResolveIcon(meta?.Icon, Path.GetDirectoryName(zipPath) ?? LauncherPaths.DirMarseyEngines, fromZip: meta?.SourceIsZip == true, zipPath);
        var signature = meta?.Signature;

        return new CustomEngineInfo(zipPath, name, description, icon, zipPath, signature);
    }

    private static string ResolveIcon(string? iconValue, string baseDir, bool fromZip, string? zipPath = null)
    {
        if (string.IsNullOrWhiteSpace(iconValue))
            return DefaultIcon;

        if (Path.IsPathRooted(iconValue) && File.Exists(iconValue))
            return iconValue;

        if (!fromZip)
        {
            var iconPath = Path.Combine(baseDir, iconValue);
            return File.Exists(iconPath) ? iconPath : DefaultIcon;
        }

        if (string.IsNullOrWhiteSpace(zipPath))
            return DefaultIcon;

        var extracted = ExtractZipIcon(zipPath, iconValue);
        return extracted ?? DefaultIcon;
    }

    private static string? ResolveClientZip(string dir, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var candidate = Path.Combine(dir, requested);
            if (File.Exists(candidate))
                return candidate;
            if (Directory.Exists(candidate))
                return candidate;
        }

        var folderRoot = FindEngineFolderRoot(dir);
        if (!string.IsNullOrWhiteSpace(folderRoot))
            return folderRoot;

        var candidates = Directory.EnumerateFiles(dir, "Robust.Client_*.zip", SearchOption.TopDirectoryOnly)
            .Select(p => new { Path = p, Rid = TryGetRidFromFile(p) })
            .Where(p => p.Rid != null)
            .ToList();

        if (candidates.Count == 1)
            return candidates[0].Path;

        if (candidates.Count > 1)
        {
            var rids = candidates.Select(c => c.Rid!).ToList();
            var best = RidUtility.FindBestRid(rids);
            if (best != null)
            {
                var bestMatch = candidates.FirstOrDefault(c => string.Equals(c.Rid, best, StringComparison.OrdinalIgnoreCase));
                if (bestMatch != null)
                    return bestMatch.Path;
            }
        }

        var fallback = Path.Combine(dir, "Robust.Client.zip");
        if (File.Exists(fallback))
            return fallback;

        var zips = Directory.EnumerateFiles(dir, "*.zip", SearchOption.TopDirectoryOnly).ToList();
        if (zips.Count == 1)
            return zips[0];

        return BuildEngineZipFromFolder(dir);
    }

    private static string? TryGetRidFromFile(string filePath)
    {
        var name = Path.GetFileName(filePath);
        if (!name.StartsWith("Robust.Client_", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return null;

        return name["Robust.Client_".Length..^4];
    }

    private static EngineMeta? TryReadMetaFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var meta = JsonSerializer.Deserialize<EngineMeta>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (meta != null)
                meta.SourceIsZip = false;
            return meta;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read engine metadata from {Path}", path);
            return null;
        }
    }

    private static EngineMeta? TryReadMetaFromZip(string zipPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(MetaFileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return null;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var meta = JsonSerializer.Deserialize<EngineMeta>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (meta != null)
                meta.SourceIsZip = true;
            return meta;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read engine metadata from zip {Path}", zipPath);
            return null;
        }
    }

    private static string? ExtractZipIcon(string zipPath, string iconValue)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entryName = iconValue.Replace('\\', '/');
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return null;

            var cacheDir = Path.Combine(Path.GetTempPath(), "MarseyEngines");
            Directory.CreateDirectory(cacheDir);

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(zipPath + "|" + entry.FullName));
            var fileName = Convert.ToHexString(hash)[..16] + Path.GetExtension(entry.FullName);
            var destPath = Path.Combine(cacheDir, fileName);

            if (!File.Exists(destPath) || new FileInfo(destPath).Length != entry.Length)
            {
                entry.ExtractToFile(destPath, true);
            }

            return destPath;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to extract engine icon from zip {Path}", zipPath);
            return null;
        }
    }

    private sealed class EngineMeta
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? ClientZip { get; set; }
        public string? Signature { get; set; }

        [JsonIgnore]
        public bool SourceIsZip { get; set; }
    }

    private static IEnumerable<string> GetEngineRoots()
    {
        var roots = new List<string>();

        var cwdRoot = Path.Combine(Directory.GetCurrentDirectory(), MarseyVars.MarseyEngineFolder);
        roots.Add(cwdRoot);

        var launcherRoot = LauncherPaths.DirMarseyEngines;
        if (!string.Equals(cwdRoot, launcherRoot, StringComparison.OrdinalIgnoreCase))
            roots.Add(launcherRoot);

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? BuildEngineZipFromFolder(string dir)
    {
        try
        {
            var root = FindEngineFolderRoot(dir);
            if (string.IsNullOrWhiteSpace(root))
                return null;

            var cacheDir = GetDefaultCacheDir();
            Directory.CreateDirectory(cacheDir);

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dir));
            var hashSuffix = Convert.ToHexString(hash)[..8];
            var zipName = $"{Path.GetFileName(dir)}_{hashSuffix}.zip";
            var zipPath = Path.Combine(cacheDir, zipName);

            var latest = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            if (File.Exists(zipPath))
            {
                var zipTime = File.GetLastWriteTimeUtc(zipPath);
                if (zipTime >= latest)
                    return zipPath;
            }

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(root, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);
            return zipPath;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to build engine zip from folder {Path}", dir);
            return null;
        }
    }

    private static string? FindEngineFolderRoot(string dir)
    {
        var clientDll = Directory.EnumerateFiles(dir, "Robust.Client.dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (clientDll == null)
            return null;

        var root = Path.GetDirectoryName(clientDll);
        return string.IsNullOrWhiteSpace(root) ? null : root;
    }

    private static string GetDefaultCacheDir()
    {
        foreach (var root in GetEngineRoots())
        {
            if (Directory.Exists(root))
                return Path.Combine(root, ".cache");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), MarseyVars.MarseyEngineFolder, ".cache");
    }
}
