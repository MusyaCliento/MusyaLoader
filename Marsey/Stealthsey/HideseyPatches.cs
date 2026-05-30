using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Marsey.Config;
using Marsey.Misc;
using Marsey.Stealthsey.Reflection;

namespace Marsey.Stealthsey;

/// <summary>
/// Manual patches used with Hidesey
/// Not based off MarseyPatch or SubverterPatch
/// </summary>
public static partial class HideseyPatches
{
    public static List<string>? LastEngineSnapshot { get; private set; }
    public static int EngineSnapshotCallCount { get; private set; }
    public static string? LastEngineCaller { get; private set; }

    public static List<string>? LastEngineFindAllTypes { get; private set; }
    public static int EngineFindAllTypesCallCount { get; private set; }
    public static string? LastEngineFindAllTypesCaller { get; private set; }

    public static List<string> BlockedTypeGetTypeProbes { get; } = new();
    public static int TypeGetTypeBlockedCount { get; private set; }
    public static string? LastBlockedTypeGetTypeProbe { get; private set; }
    public static string? LastBlockedTypeGetTypeCaller { get; private set; }

    /// <summary>
    /// Diagnostic: external callers that asked for Assembly.Location of a hidden asm.
    /// </summary>
    public static List<string> BlockedLocationProbes { get; } = new();
    public static int LocationBlockedCount { get; private set; }
    public static string? LastBlockedLocationCaller { get; private set; }

    /// <summary>
    /// Diagnostic: Environment.StackTrace filter — how many times we sanitized a non-hidden caller's view.
    /// </summary>
    public static int StackTraceFilteredCount { get; private set; }
    public static string? LastFilteredStackTraceCaller { get; private set; }

    /// <summary>
    /// Diagnostic: IModLoader.LoadedModules filter — non-hidden callers that got their view sanitized.
    /// </summary>
    public static List<string>? LastEngineModLoader { get; private set; }
    public static int EngineModLoaderCallCount { get; private set; }
    public static string? LastEngineModLoaderCaller { get; private set; }

    /// <summary>
    /// Diagnostic: IResourceManager.ContentFindFiles filter — non-hidden callers that got their view sanitized.
    /// </summary>
    public static int ContentFindFilesFilteredCount { get; private set; }
    public static string? LastContentFindFilesCaller { get; private set; }
    public static List<string> ContentFindFilesBlockedNames { get; } = new();

    public static void Lie<T>(ref T __result)
    {
        __result = __result switch
        {
            Assembly[] assemblies => (T)(object)Hidesey.LyingDomain(assemblies),
            IReadOnlyList<Assembly> readOnlyAsms => (T)(object)Hidesey.LyingReflection(readOnlyAsms),
            IEnumerable<Assembly> assemblyEnumerable => (T)(object)Hidesey.LyingContext(assemblyEnumerable),
            IEnumerable<AssemblyLoadContext> assemblyLoadContextEnumerable => (T)(object)Hidesey.LyingManifest(
                assemblyLoadContextEnumerable),
            AssemblyName[] assemblyNames => (T)(object)Hidesey.LyingReference(assemblyNames),
            Type[] types => (T)(object)Hidesey.LyingTyper(types),
            _ => throw new InvalidOperationException("Unsupported type for LiePatch")
        };
    }

    public static void LieReflection(ref IReadOnlyList<Assembly> __result)
    {
        var filtered = Hidesey.LyingReflection(__result);
        __result = filtered;
        TryCaptureAssemblies(filtered);
    }

    public static void LieFindAllTypes(ref IEnumerable<Type> __result)
    {
        var filtered = Hidesey.LyingFindAllTypes(__result).ToList();
        __result = filtered;
        TryCaptureFindAllTypes(filtered);
    }

    public static void LieGetType(ref Type? __result, object[] __args)
    {
        var originalResult = __result;
        __result = Hidesey.LyingGetType(__result);

        if (originalResult != null && __result == null && __args.Length > 0 && __args[0] is string probe)
        {
            try
            {
                TypeGetTypeBlockedCount++;
                LastBlockedTypeGetTypeProbe = probe;
                LastBlockedTypeGetTypeCaller = FindFirstBusinessCaller();
                if (BlockedTypeGetTypeProbes.Count < 100)
                    BlockedTypeGetTypeProbes.Add($"{probe} → {originalResult.AssemblyQualifiedName} [by {LastBlockedTypeGetTypeCaller ?? "?"}]");
            }
            catch { }
        }
    }

    /// <summary>
    /// Postfix on IReflectionManager.TryLooseGetType — anticheat fallback path.
    /// Signature on Robust: bool TryLooseGetType(string name, [NotNullWhen(true)] out Type? type)
    /// We check the resolved Type and clear it if hidden.
    ///
    /// Uses Harmony's named ref parameter `__1` which matches positional argument 1
    /// regardless of original parameter name (`type`, `result`, etc).
    /// This works for both `ref Type` and `out Type` signatures.
    /// </summary>
    public static void LieTryLooseGetType(ref bool __result, ref Type? __1, string? __0)
    {
        if (!__result) return;
        if (__1 == null) return;

        try
        {
            var filtered = Hidesey.LyingGetType(__1);
            if (filtered == null)
            {
                var original = __1;
                __1 = null;
                __result = false;

                try
                {
                    TypeGetTypeBlockedCount++;
                    LastBlockedTypeGetTypeProbe = __0 ?? "?";
                    LastBlockedTypeGetTypeCaller = FindFirstBusinessCaller();
                    if (BlockedTypeGetTypeProbes.Count < 100)
                        BlockedTypeGetTypeProbes.Add($"[loose] {LastBlockedTypeGetTypeProbe} → {original.AssemblyQualifiedName} [by {LastBlockedTypeGetTypeCaller ?? "?"}]");
                }
                catch { }
            }
        }
        catch { /* swallow */ }
    }

    /// <summary>
    /// Postfix on Assembly.Location getter.
    /// Returns fake location for hidden assemblies, captures diagnostic for external probes.
    /// </summary>
    public static void LieLocation(ref string __result, Assembly __instance)
    {
        var originalResult = __result;
        __result = Hidesey.LyingLocation(__result, __instance);

        if (!ReferenceEquals(originalResult, __result))
        {
            try
            {
                LocationBlockedCount++;
                LastBlockedLocationCaller = FindFirstBusinessCaller();
                if (BlockedLocationProbes.Count < 100)
                {
                    var asmName = __instance.GetName().Name ?? "?";
                    BlockedLocationProbes.Add($"{asmName} probed by {LastBlockedLocationCaller ?? "?"}");
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Postfix on Environment.StackTrace getter.
    /// Filters out lines that reference hidden assemblies.
    /// </summary>
    public static void LieStackTrace(ref string __result)
    {
        if (string.IsNullOrEmpty(__result)) return;

        var original = __result;
        __result = Hidesey.LyingStackTrace(__result);

        if (!ReferenceEquals(original, __result) && original != __result)
        {
            try
            {
                StackTraceFilteredCount++;
                LastFilteredStackTraceCaller = FindFirstBusinessCaller();
            }
            catch { }
        }
    }

    /// <summary>
    /// Postfix on IModLoader.LoadedModules getter.
    /// Returns enumerable of Assembly (concrete return type matters in postfix).
    /// </summary>
    public static void LieModLoader(ref IEnumerable<Assembly> __result)
    {
        if (__result == null) return;
        var filtered = Hidesey.LyingModLoader(__result).ToList();
        __result = filtered;
        TryCaptureModLoader(filtered);
    }

    /// <summary>
    /// Postfix on IResourceManager.ContentFindFiles — filters DLL paths under /Assemblies and /EnginePatches.
    /// Removes entries whose filename (without extension) matches a hidden assembly name.
    /// Note: __result type can be IEnumerable&lt;ResPath&gt; — we work via reflection on the items
    /// to avoid taking a hard dependency on Robust types in our patch class.
    /// </summary>
    public static void LieContentFindFiles(ref object __result, object[] __args)
    {
        if (__result == null) return;

        // Mod code (hidden caller) wants the real, unfiltered listing — pass through.
        // Only sanitize the result for non-hidden callers (engine / anticheat).
        if (Hidesey.FromHidden()) return;

        // Look at the first argument — usually a path string or a ResPath struct.
        string? pathStr = null;
        if (__args != null && __args.Length > 0 && __args[0] != null)
        {
            var arg = __args[0];
            pathStr = arg as string;
            if (pathStr == null)
            {
                // Likely a ResPath struct — convert to string via ToString() which is canonical for ResPath.
                try { pathStr = arg.ToString(); } catch { }
            }
        }

        // Only filter our two scanned roots — leave everything else untouched.
        if (string.IsNullOrEmpty(pathStr)) return;
        if (!pathStr.StartsWith("/Assemblies", StringComparison.OrdinalIgnoreCase)
            && !pathStr.StartsWith("/EnginePatches", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (__result is IEnumerable enumerable)
            {
                var resultType = __result.GetType();
                var elementType = ExtractElementType(resultType);
                if (elementType == null) return;

                // Materialize via List<elementType>
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType)!;

                int blocked = 0;
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (ShouldKeepResource(item))
                    {
                        list.Add(item);
                    }
                    else
                    {
                        blocked++;
                        if (ContentFindFilesBlockedNames.Count < 100)
                            ContentFindFilesBlockedNames.Add(ExtractFileNameForLog(item) ?? "?");
                    }
                }

                __result = list;

                if (blocked > 0)
                {
                    ContentFindFilesFilteredCount += blocked;
                    LastContentFindFilesCaller = FindFirstBusinessCaller();
                }
            }
        }
        catch { /* never break the engine */ }
    }

    /// <summary>
    /// Returns true if this resource entry should NOT be filtered (caller can keep it).
    /// Filters out .dll/.pdb files whose filename matches a hidden assembly name.
    /// </summary>
    private static bool ShouldKeepResource(object resource)
    {
        try
        {
            // ResPath struct has FilenameWithoutExtension and Extension properties.
            var type = resource.GetType();
            var fnameProp = type.GetProperty("FilenameWithoutExtension");
            var extProp = type.GetProperty("Extension");
            if (fnameProp == null || extProp == null) return true; // unknown shape — don't touch

            var fname = fnameProp.GetValue(resource) as string;
            var ext = extProp.GetValue(resource) as string;

            if (string.IsNullOrEmpty(fname)) return true;

            // Only filter dll/pdb entries.
            if (!string.Equals(ext, "dll", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ext, "pdb", StringComparison.OrdinalIgnoreCase))
                return true;

            return !Hidesey.IsHiddenName(fname);
        }
        catch
        {
            return true;
        }
    }

    private static string? ExtractFileNameForLog(object resource)
    {
        try
        {
            var fnameProp = resource.GetType().GetProperty("Filename");
            return fnameProp?.GetValue(resource) as string;
        }
        catch { return null; }
    }

    private static Type? ExtractElementType(Type collectionType)
    {
        if (collectionType.IsArray) return collectionType.GetElementType();

        foreach (var iface in collectionType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }
        return null;
    }

    private static void TryCaptureAssemblies(IReadOnlyList<Assembly> result)
    {
        try
        {
            string? caller = FindFirstBusinessCaller();
            if (caller == null) return;
            if (!IsEngineCaller(caller)) return;

            EngineSnapshotCallCount++;
            LastEngineSnapshot = result.Select(a => a.GetName().Name ?? "?").ToList();
            LastEngineCaller = caller;
        }
        catch { }
    }

    private static void TryCaptureFindAllTypes(IReadOnlyList<Type> result)
    {
        try
        {
            string? caller = FindFirstBusinessCaller();
            if (caller == null) return;
            if (!IsEngineCaller(caller)) return;

            EngineFindAllTypesCallCount++;
            LastEngineFindAllTypes = result.Select(t => $"{t.Assembly.GetName().Name}::{t.FullName ?? t.Name}").ToList();
            LastEngineFindAllTypesCaller = caller;
        }
        catch { }
    }

    private static void TryCaptureModLoader(IReadOnlyList<Assembly> result)
    {
        try
        {
            string? caller = FindFirstBusinessCaller();
            if (caller == null) return;
            if (!IsEngineCaller(caller)) return;

            EngineModLoaderCallCount++;
            LastEngineModLoader = result.Select(a => a.GetName().Name ?? "?").ToList();
            LastEngineModLoaderCaller = caller;
        }
        catch { }
    }

    private static string? FindFirstBusinessCaller()
    {
        var trace = new StackTrace(0, false);
        foreach (var frame in trace.GetFrames())
        {
            var method = frame.GetMethod();
            var declType = method?.DeclaringType;
            var asm = declType?.Assembly;
            if (asm == null || declType == null) continue;
            if (asm.IsDynamic) continue;

            var asmName = asm.GetName().Name ?? "";
            if (asmName.Contains("Harmony") || asmName.Contains("MonoMod") || asmName.Contains("Mono.Cecil"))
                continue;
            if (asmName == "Marsey") continue;

            return $"{declType.FullName}.{method!.Name}";
        }
        return null;
    }

    private static bool IsEngineCaller(string caller)
    {
        return caller.StartsWith("Robust.") || caller.StartsWith("Content.");
    }

    public static bool Skip() => false;
    public static bool SkipPatchless() => !MarseyConf.Patchless;

    public static bool LevelCheck(MethodBase __originalMethod)
    {
        string fullMethodName = $"{__originalMethod.DeclaringType?.FullName}::{__originalMethod.Name}";
        string parameters = string.Join(", ", __originalMethod.GetParameters().Select(p => p.ParameterType.Name));
        fullMethodName += $"({parameters})";

        object[] customAttributes = __originalMethod.GetCustomAttributes(false);

        HideLevelRequirement? hideLevelRequirement = customAttributes.OfType<HideLevelRequirement>().FirstOrDefault();
        if (hideLevelRequirement != null && MarseyConf.MarseyHide < hideLevelRequirement.Level)
        {
            MarseyLogger.Log(MarseyLogger.LogType.DEBG,
                $"Not executing {fullMethodName} due to lower MarseyHide level. " +
                $"Required: {hideLevelRequirement.Level}, Current: {MarseyConf.MarseyHide}");
            return false;
        }

        HideLevelRestriction? hideLevelRestriction = customAttributes.OfType<HideLevelRestriction>().FirstOrDefault();
        if (hideLevelRestriction != null && MarseyConf.MarseyHide >= hideLevelRestriction.MaxLevel)
        {
            MarseyLogger.Log(MarseyLogger.LogType.DEBG,
                $"Not executing {fullMethodName} due to equal or above MarseyHide level. " +
                $"Threshold: {hideLevelRestriction.MaxLevel}, Current: {MarseyConf.MarseyHide}");
            return false;
        }

        return true;
    }
}
