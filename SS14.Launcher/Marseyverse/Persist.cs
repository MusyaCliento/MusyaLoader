using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marsey.Config;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Marseyverse;

public static class Persist
{
    public sealed record ResourcePackConfigEntry(string Dir, bool Enabled);

    public static void SavePatchlistConfig(List<string> patches)
    {
        File.WriteAllLines(Path.Combine(LauncherPaths.DirUserData, MarseyVars.EnabledPatchListFileName), patches);
    }

    public static List<string> LoadPatchlistConfig()
    {
        string filePath = Path.Combine(LauncherPaths.DirUserData, MarseyVars.EnabledPatchListFileName);
        return File.Exists(filePath) ? [..File.ReadAllLines(filePath)] : [];
    }

    public static void SaveResourcePacksConfig(List<ResourcePackConfigEntry> resourcePacks)
    {
        var lines = resourcePacks
            .Select(rp => $"{(rp.Enabled ? "1" : "0")}|{rp.Dir}")
            .ToArray();
        File.WriteAllLines(Path.Combine(LauncherPaths.DirUserData, "resourcepacks.marsey"), lines);
    }

    public static List<ResourcePackConfigEntry> LoadResourcePacksConfig()
    {
        string filePath = Path.Combine(LauncherPaths.DirUserData, "resourcepacks.marsey");
        if (!File.Exists(filePath))
            return [];

        var result = new List<ResourcePackConfigEntry>();
        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Backwards compatibility with old format (only dir, implied enabled).
            var split = line.Split('|', 2);
            if (split.Length != 2)
            {
                result.Add(new ResourcePackConfigEntry(line, true));
                continue;
            }

            var enabled = split[0] == "1";
            var dir = split[1];
            result.Add(new ResourcePackConfigEntry(dir, enabled));
        }

        return result;
    }

    public static void UpdateLauncherConfig()
    {
        DataManager cfg = Locator.Current.GetRequiredService<DataManager>();

        MarseyConf.Logging = cfg.GetCVar(CVars.LogLauncherPatcher);
        MarseyConf.DebugAllowed = cfg.GetCVar(CVars.LogLoaderDebug);
        MarseyConf.TraceAllowed = cfg.GetCVar(CVars.LogLoaderTrace);
        MarseyConf.Dumper = cfg.GetCVar(CVars.DumpAssemblies);
    }
}
