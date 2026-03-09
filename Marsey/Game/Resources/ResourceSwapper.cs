using System.Reflection;
using System.Linq;
using HarmonyLib;
using Marsey.Game.Resources.Reflection;
using Marsey.Handbreak;
using Marsey.Misc;

namespace Marsey.Game.Resources;

public static class ResourceSwapper
{
    private static readonly List<string> _filepaths = new();
    private static readonly Dictionary<string, string> _canonToDisk = new(StringComparer.OrdinalIgnoreCase);
    private static bool _patched;
    
    public static void Start()
    {
        _filepaths.Clear();
        _canonToDisk.Clear();

        List<ResourcePack> rPacks = ResMan.GetRPacks();
        
        foreach (ResourcePack rpack in rPacks)
        {
             PopulateFiles(rpack.Dir);
        }

        if (_canonToDisk.Count == 0)
        {
            MarseyLogger.Log(MarseyLogger.LogType.DEBG, "No files found in resource packs.");
            return;
        }

        MarseyLogger.Log(MarseyLogger.LogType.INFO, $"ResourceSwapper: Loaded {_canonToDisk.Count} overrides.");
        Patch();
    }

    private static void PopulateFiles(string directory)
    {
        string absoluteDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(absoluteDirectory)) return;

        string[] files = Directory.GetFiles(absoluteDirectory, "*", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            // Ignore only pack metadata files in the pack root.
            // Do not skip nested files like Textures/.../*.rsi/meta.json or any icon.png assets.
            string relativePath = Path.GetRelativePath(absoluteDirectory, file).Replace('\\', '/');
            if (relativePath.Equals("meta.json", StringComparison.OrdinalIgnoreCase)) continue;
            if (relativePath.Equals("icon.png", StringComparison.OrdinalIgnoreCase)) continue;

            string canonPath = BuildCanonicalPath(absoluteDirectory, file);
            
            _canonToDisk[canonPath.ToLowerInvariant()] = file;
            _filepaths.Add(file);
        }
    }

    private static string BuildCanonicalPath(string packRoot, string filePath)
    {
        string relative = Path.GetRelativePath(packRoot, filePath).Replace('\\', '/');

        if (relative.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            relative = relative["Resources/".Length..];

        if (!relative.StartsWith("/"))
            relative = "/" + relative;

        return relative;
    }

    private static void Patch()
    {
        if (_patched || ResourceTypes.ResPath == null) return;

        Helpers.PatchMethod(
            targetType: ResourceTypes.ProtoMan,
            targetMethodName: "ContentFindFiles",
            patchType: typeof(ResourcePatches),
            patchMethodName: "SwapCFFRes",
            patchingType: HarmonyPatchType.Postfix,
            new Type[] { ResourceTypes.ResPath }
        );

        Helpers.PatchMethod(
            targetType: ResourceTypes.ProtoMan,
            targetMethodName: "TryContentFileRead",
            patchType: typeof(ResourcePatches),
            patchMethodName: "TrySwapContentRead",
            patchingType: HarmonyPatchType.Prefix,
            new[] { ResourceTypes.ResPath, typeof(Stream).MakeByRefType() }
        );

        _patched = true;
    }

    public static bool TryResolveOverride(string canonPath, out string diskPath)
    {
        return _canonToDisk.TryGetValue(canonPath.ToLowerInvariant(), out diskPath!);
    }

    public static IEnumerable<string> OverrideCanonPaths() => _canonToDisk.Keys;
}
