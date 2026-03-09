using System.Reflection;
using System.Collections;
using HarmonyLib;
using Marsey.Misc;

namespace Marsey.Game.Resources.Reflection;

public static class ResourcePatches
{
    private static void SwapCFFRes(object? path, ref dynamic __result)
    {
        Type? maybeResPathType = ResourceTypes.ResPath;
        if (maybeResPathType == null || __result == null) return;
        Type resPathType = maybeResPathType!;
        if (__result is not IEnumerable existingItems) return;

        ConstructorInfo? constructor = AccessTools.Constructor(resPathType, new[] { typeof(string) });
        if (constructor == null) return;

        string requestedPath = NormalizeCanonPath(path);
        
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        Type listType = typeof(List<>).MakeGenericType(resPathType);
        IList mergedList = (IList)Activator.CreateInstance(listType)!;

        foreach (var item in existingItems)
        {
            string cp = NormalizeCanonPath(item);
            if (seen.Add(cp)) mergedList.Add(item);
        }

        foreach (string over in ResourceSwapper.OverrideCanonPaths())
        {
            if (IsPathInside(over, requestedPath) && seen.Add(over))
            {
                mergedList.Add(constructor.Invoke(new object[] { over }));
            }
        }

        __result = mergedList;
    }

    private static bool TrySwapContentRead(object? path, ref Stream? fileStream, ref bool __result)
    {
        string canonPath = NormalizeCanonPath(path);

        if (ResourceSwapper.TryResolveOverride(canonPath, out string diskPath))
        {
            try
            {
                fileStream = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                MarseyLogger.Log(MarseyLogger.LogType.ERRO, "ResSwap", $"Error reading {diskPath}: {ex.Message}");
            }
        }

        return true;
    }

    private static string NormalizeCanonPath(object? pathObj)
    {
        if (pathObj == null) return "/";
        
        string? pathStr;
        if (pathObj is string s) 
            pathStr = s;
        else 
            pathStr = ResourceTypes.ResPathCanonPath?.GetValue(pathObj) as string ?? pathObj.ToString();

        if (string.IsNullOrEmpty(pathStr)) return "/";

        string normalized = pathStr.Replace('\\', '/').ToLowerInvariant();
        if (!normalized.StartsWith("/")) normalized = "/" + normalized;
        
        return normalized;
    }

    private static bool IsPathInside(string file, string folder)
    {
        if (folder == "/") return true;
        string folderPrefix = folder.EndsWith("/") ? folder : folder + "/";
        return file.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
