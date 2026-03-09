using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Marsey.Config;
using Marsey.Game.Patches.Marseyports.Attributes;
using Marsey.Misc;
using Marsey.Stealthsey;
using Marsey.Stealthsey.Reflection;

namespace Marsey.Game.Patches.Marseyports;

/// <summary>
/// Manages code backports, fixes and patches
/// </summary>
public static class MarseyPortMan
{
    public static string fork = "";
    public static string engine = string.Empty;
    private static Version? _engineVersion;
    private static IEnumerable<Type>? _backports;

    private static bool TryParseEngineVersion(string? eng, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(eng)) return false;

        string value = eng.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(1);

        int splitIndex = value.IndexOfAny(new[] { '-', '+' });
        if (splitIndex >= 0)
            value = value.Substring(0, splitIndex);

        if (!Version.TryParse(value, out Version? parsed) || parsed == null)
            return false;

        version = parsed;
        return true;
    }

    public static void SetEngineVer(string eng)
    {
        engine = eng ?? string.Empty;
        if (TryParseEngineVersion(engine, out Version parsed))
            _engineVersion = parsed;
        else
            _engineVersion = null;
    }
    public static void SetForkID(string forkid) => fork = forkid;

    public static void Initialize()
    {
        // https://www.youtube.com/watch?v=vmUGxXrlRmE
        if (!MarseyConf.Backports) return;

        MarseyLogger.Log(MarseyLogger.LogType.DEBG, "Backporter", $"Starting backporter against fork \"{fork}\", engine {engine}.");

        IEnumerable<Type> backports = GetBackports();
        MarseyLogger.Log(MarseyLogger.LogType.DEBG, "Backporter",$"Found {backports.Count()} available backports.");

        _backports = backports.Where(ValidateBackport);
        MarseyLogger.Log(MarseyLogger.LogType.DEBG, "Backporter",$"Found {_backports.Count()} valid backports.");
    }

    private static IEnumerable<Type> GetBackports()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        IEnumerable<Type> types = SafeGetTypes(assembly);
        return types.Where(t => t.Namespace != null && t.Namespace.StartsWith("Marsey.Game.Patches.Marseyports.Fixes"));
    }

    /// <summary>
    /// Determines if this backport should be applied
    /// </summary>
    private static bool ValidateBackport(Type backport)
    {
        BackportTargetFork? BTF = backport.GetCustomAttribute<BackportTargetFork>();
        BackportTargetEngine? BTE = backport.GetCustomAttribute<BackportTargetEngine>();
        BackportTargetEngineAfter? BTEAf = backport.GetCustomAttribute<BackportTargetEngineAfter>();
        BackportTargetEngineBefore? BTEB = backport.GetCustomAttribute<BackportTargetEngineBefore>();
        BackportTargetEngineAny? BTEAny = backport.GetCustomAttribute<BackportTargetEngineAny>();

        Version? engineVersion = _engineVersion;

        // Discard if fork id is set and does not match
        if (BTF != null && BTF.ForkID != fork) return false;
        // Discard if target engine is set and does not match
        if (BTE != null && (engineVersion == null || BTE.Ver.CompareTo(engineVersion) != 0)) return false;
        // Discard if target engine after is set and version is below
        if (BTEAf != null && (engineVersion == null || BTEAf.Ver.CompareTo(engineVersion) > 0)) return false;
        // Discard if target engine before is set and version is above
        if (BTEB != null && (engineVersion == null || BTEB.Ver.CompareTo(engineVersion) < 0)) return false;
        // Discard if any engine is targeted, but backports of this type are disabled
        if (BTEAny != null && MarseyConf.DisableAnyBackports) return false;

        return true;
    }

    [Patching]
    public static void PatchBackports(bool Content = false)
    {
        if (_backports == null) return;

        foreach (Type backport in _backports)
        {
            object instance = AccessTools.CreateInstance(backport);

            // We are backporting fixes to engine for now
            PropertyInfo contentProperty = AccessTools.Property(backport, "Content");
            bool content = contentProperty != null && (bool)(contentProperty.GetValue(instance) ?? false);

            switch (Content)
            {
                case false when content:
                case true when !content:
                    continue;
            }

            MethodInfo patchMethod = AccessTools.Method(backport, "Patch");
            patchMethod.Invoke(instance, null);

            MarseyLogger.Log(MarseyLogger.LogType.DEBG, "Backporter", $"Backported {backport.Name}.");
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}
