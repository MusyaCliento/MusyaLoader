using Marsey.Config;
using Marsey.Game.Patches.Marseyports;
using Marsey.Game.Resources.Dumper;
using Marsey.Game.Resources.Dumper.Resource;
using Marsey.Game.Resources.Reflection;
using Marsey.Misc;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Marsey.Game.Resources;

public static class ResMan
{
    private static readonly List<ResourcePack> _resourcePacks = new List<ResourcePack>();
    private static string? _fork;

    /// <summary>
    /// Executed by the loader
    /// </summary>
    public static void Initialize()
    {
        ResourceTypes.Initialize();

        _fork = MarseyPortMan.fork; // Peak spaghet moment

        // If we're dumping the game we don't want to dump our own respack now would we
        if (MarseyConf.Dumper)
        {
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "[MARSEY] Starting Resource Dumper...");
            MarseyDumper.Start();

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                MarseyLogger.Log(MarseyLogger.LogType.INFO, "[MARSEY] Dump complete. Disabling Dumper flag.");
                ResourceDumper.Unpatch();
                MarseyConf.Dumper = false;
            };
            return;
        }

        // Retrieve enabled resource packs data through named pipe
        List<string> enabledPacks = FileHandler.GetFilesFromPipe("ResourcePacksPipe");

        if (enabledPacks.Count == 0)
        {
            MarseyLogger.Log(MarseyLogger.LogType.DEBG, "No enabled resource packs were received from launcher.");
            return;
        }

        MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"Detecting {enabledPacks.Count} enabled resource packs.");

        foreach (string dir in enabledPacks)
        {
            InitializeRPack(dir, !MarseyConf.DisableResPackStrict);
        }

        ResourceSwapper.Start();
    }

    /// <summary>
    /// Executed by the launcher
    /// </summary>
    public static void LoadDir()
    {
        _resourcePacks.Clear();

        try
        {
            var basePath = MarseyVars.MarseyResourceFolder;

            if (string.IsNullOrWhiteSpace(basePath))
            {
                MarseyLogger.Log(MarseyLogger.LogType.FATL, "Marsey resource folder path is empty or null.");
                return;
            }

            // If the resource folder doesn't exist — create it (safer) and return (nothing to load yet).
            if (!Directory.Exists(basePath))
            {
                MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"Resource folder not found: '{basePath}'. Creating empty directory.");
                try
                {
                    Directory.CreateDirectory(basePath);
                }
                catch (Exception createEx)
                {
                    MarseyLogger.Log(MarseyLogger.LogType.FATL, $"Failed to create resource folder '{basePath}': {createEx}");
                    return;
                }

                // folder was missing, no subfolders to iterate
                return;
            }

            string[] subDirs = Directory.GetDirectories(basePath);
            foreach (string subdir in subDirs)
            {
                try
                {
                    InitializeRPack(subdir);
                }
                catch (Exception rpEx)
                {
                    // Protect against a single bad pack bringing down the whole loader
                    MarseyLogger.Log(MarseyLogger.LogType.FATL, $"Failed to initialize resource pack at '{subdir}': {rpEx}");
                }
            }
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"Unhandled exception while loading resource packs from '{MarseyVars.MarseyResourceFolder}': {ex}");
        }
    }

    /// <summary>
    /// Creates a ResourcePack object from a given path to a directory
    /// </summary>
    /// <param name="path">resource pack directory</param>
    /// <param name="strict">match fork id</param>
    private static void InitializeRPack(string path, bool strict = false)
    {
        ResourcePack rpack = new ResourcePack(path);

        try
        {
            rpack.ParseMeta();
        }
        catch (RPackException e)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, e.ToString());
            return;
        }

        AddRPack(rpack, strict);
    }

    private static void AddRPack(ResourcePack rpack, bool strict)
    {
        if (_resourcePacks.Any(rp => rp.Dir == rpack.Dir)) return;
        if (strict && rpack.Target != _fork && rpack.Target != "") return;

        _resourcePacks.Add(rpack);
    }

    public static List<ResourcePack> GetRPacks() => _resourcePacks;
    public static string? GetForkID() => _fork;
}
