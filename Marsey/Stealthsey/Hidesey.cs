using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using Marsey.Config;
using Marsey.Game.Managers;
using Marsey.Game.Patches;
using Marsey.Handbreak;
using Marsey.Misc;
using Marsey.Stealthsey.Reflection;

namespace Marsey.Stealthsey;

/// <summary>
/// Level of concealment from the game
/// </summary>
public enum HideLevel
{
    Disabled = 0,
    Duplicit = 1,
    Normal = 2,
    Explicit = 3,
    Unconditional = 4
}

/// <summary>
/// Hides patches from the game
/// </summary>
public static partial class Hidesey
{
    private static List<Assembly> _hideseys = new List<Assembly>();
    private static bool _initialized;
    private static bool _caching;
    [ThreadStatic] private static bool _inFromHidden;

    // Fast-path cache for FromHidden: assemblies known to be engine/system, skip stack walk on these
    private static readonly HashSet<Assembly> _knownEngineAsms = new();

    // Asm names that should be treated as hidden even if not in _hideseys list.
    // These are dynamic asms created at runtime (Harmony emits) — matched by name because the
    // anticheat's dynamic-assembly probe flags names like "dynamic / in memory / anonymously
    // hosted / reflection.emit / runtimegenerated" (the obfuscated method name carrying that
    // regex changes every build, so we match the asm names directly rather than the probe).
    private static readonly string[] _dynamicHiddenNames =
    {
        "Anonymously Hosted DynamicMethods Assembly",
        "HarmonySharedState",
    };

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        HideseyAttributeManager.Initialize();

        Load();
    }

    [HideLevelRequirement(HideLevel.Duplicit)]
    private static void Load()
    {
        Disperse();

        Facade.Imposition("Marsey");

        Perjurize();

        MarseyLogger.Log(MarseyLogger.LogType.INFO, $"Hidesey started. Running {MarseyConf.MarseyHide.ToString()} configuration.");
    }

    [HideLevelRequirement(HideLevel.Duplicit)]
    public static void Disperse()
    {
        (string, bool)[] assembliesToHide = new (string, bool)[]
        {
            ("0Harmony", false),
            ("Mono.Cecil", false),
            ("MonoMod", true),
            ("MonoMod.Iced", false),
            ("System.Reflection.Emit,", false),
            ("Marsey", false),
            ("Harmony", true)
        };

        foreach ((string assembly, bool recursive) in assembliesToHide)
        {
            Hide(assembly, recursive);
        }
    }

    public static void PostLoad()
    {
        HWID.Force();
        DiscordRPC.Patch();

        PerjurizeReflection();

        Disperse();
    }

    public static void Cleanup()
    {
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            ToggleCaching();
        });
    }

    private static void ToggleCaching()
    {
        MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"Caching is set to {!_caching}");
        _caching = !_caching;
    }

    private static void Hide(string marsey, bool recursive = false)
    {
        Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly asm in asms)
        {
            if (asm.FullName == null || !asm.FullName.Contains(marsey)) continue;
            Hide(asm);
            if (!recursive) return;
        }
    }

    private static void Hide(Assembly marsey)
    {
        Facade.Cloak(marsey);
        _hideseys.Add(marsey);
    }

    [HideLevelRequirement(HideLevel.Normal)]
    public static void HidePatch(Assembly marsey)
    {
        Hide(marsey);
    }

    /// <summary>
    /// Returns true if assembly should be hidden from anticheat probes.
    /// Covers:
    ///   1. Explicitly registered via Hide()/HidePatch() — in _hideseys list
    ///   2. Dynamic asms by name match (Anonymously Hosted DynamicMethods Assembly,
    ///      HarmonySharedState) — these would otherwise be caught by the anticheat's
    ///      dynamic-assembly regex (dynamic/in memory/anonymously hosted/reflection.emit/
    ///      runtimegenerated).
    /// IMPORTANT: System.Reflection.Emit.* assemblies are NOT hidden — they are part of .NET
    /// runtime baseline and exist on clean clients too.
    /// </summary>
    public static bool IsHiddenAsm(Assembly asm)
    {
        if (asm == null) return false;
        if (_hideseys.Contains(asm)) return true;

        // Dynamic asm name match (Harmony's emitted asms)
        string? name = null;
        try { name = asm.GetName().Name; } catch { return false; }
        if (string.IsNullOrEmpty(name)) return false;

        foreach (var hiddenName in _dynamicHiddenNames)
        {
            if (string.Equals(name, hiddenName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// String-based variant. Used by VFS scan filter (resource filenames without Assembly object).
    /// Matches sanitized assembly names against _hideseys names + dynamic hidden names.
    /// </summary>
    public static bool IsHiddenName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        foreach (var hidden in _hideseys)
        {
            string? hiddenName = null;
            try { hiddenName = hidden.GetName().Name; } catch { continue; }
            if (string.IsNullOrEmpty(hiddenName)) continue;
            if (string.Equals(name, hiddenName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var hiddenName in _dynamicHiddenNames)
        {
            if (string.Equals(name, hiddenName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Undermines system functions, hides what doesnt belong from view
    /// </summary>
    private static void Perjurize()
    {
        MethodInfo? Lie = Helpers.GetMethod(typeof(HideseyPatches), "Lie");

        (MethodInfo?, Type)[] patches =
        [
            (typeof(AppDomain).GetMethod(nameof(AppDomain.GetAssemblies)), typeof(Assembly[])),
            (Assembly.GetExecutingAssembly().GetType().GetMethod(nameof(Assembly.GetReferencedAssemblies)), typeof(AssemblyName[])),
            (typeof(Assembly).GetMethod(nameof(Assembly.GetTypes)), typeof(Type[])),
            (typeof(AssemblyLoadContext).GetProperty("Assemblies")?.GetGetMethod(), typeof(IEnumerable<Assembly>)),
            (typeof(AssemblyLoadContext).GetProperty("All")?.GetGetMethod(), typeof(IEnumerable<AssemblyLoadContext>))
        ];

        foreach ((MethodInfo? targetMethod, Type returnType) in patches)
        {
            Helpers.PatchGenericMethod(
                target: targetMethod,
                patch: Lie,
                patchReturnType: returnType,
                patchType: HarmonyPatchType.Postfix
            );
        }

        // Closes Δ() HasInvalidMetadata and Ψ() HasExternalLibrary anticheat vectors
        PerjurizeTypeGetType();

        // Closes Anticheat fallback path via IReflectionManager.TryLooseGetType
        // (Anticheat calls Type.GetType, then on null falls back to TryLooseGetType).
        // Without this hook, type names like "MarseyEntry" resolve through loose name lookup.
        PerjurizeTryLooseGetType();

        // Closes Assembly.Location leak — even if anticheat gets a ref to our asm somehow.
        PerjurizeAssemblyLocation();

        // Closes Environment.StackTrace leak (whitelisted string property).
        PerjurizeStackTrace();
    }

    private static void PerjurizeTryLooseGetType()
    {
        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(nameof(HideseyPatches.LieTryLooseGetType),
            BindingFlags.Public | BindingFlags.Static);

        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "HideseyPatches.LieTryLooseGetType not found");
            return;
        }

        try
        {
            // Resolve ReflectionManager base class at runtime — that's where TryLooseGetType is declared.
            // Harmony requires patching the declared method, not an inherited override.
            Type? reflectionManagerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "Robust.Shared") continue;
                reflectionManagerType = asm.GetType("Robust.Shared.Reflection.ReflectionManager");
                if (reflectionManagerType != null) break;
            }

            if (reflectionManagerType == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.WARN, "PerjurizeTryLooseGetType: ReflectionManager base type not found");
                return;
            }

            // Patch every TryLooseGetType method declared directly on this type.
            // DeclaredOnly ensures we get the base implementation, not inherited overrides.
            var harm = HarmonyManager.GetHarmony();
            int hooked = 0;
            foreach (var m in reflectionManagerType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (m.Name != "TryLooseGetType") continue;
                if (m.ReturnType != typeof(bool)) continue;
                try
                {
                    harm.Patch(m, postfix: new HarmonyMethod(postfix));
                    MarseyLogger.Log(MarseyLogger.LogType.INFO, $"ReflectionManager.TryLooseGetType({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) hook installed");
                    hooked++;
                }
                catch (Exception ex)
                {
                    MarseyLogger.Log(MarseyLogger.LogType.FATL, $"TryLooseGetType hook failed for overload: {ex.Message}");
                }
            }

            if (hooked == 0)
                MarseyLogger.Log(MarseyLogger.LogType.WARN, "PerjurizeTryLooseGetType: no overloads patched");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"PerjurizeTryLooseGetType failed: {ex.Message}");
        }
    }

    private static void PerjurizeTypeGetType()
    {
        Type[][] signatures =
        {
            new[] { typeof(string) },
            new[] { typeof(string), typeof(bool) },
            new[] { typeof(string), typeof(bool), typeof(bool) },
        };

        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(nameof(HideseyPatches.LieGetType),
            BindingFlags.Public | BindingFlags.Static);

        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "HideseyPatches.LieGetType not found");
            return;
        }

        foreach (var sig in signatures)
        {
            MethodInfo? target = typeof(Type).GetMethod(nameof(Type.GetType), BindingFlags.Public | BindingFlags.Static, null, sig, null);
            if (target == null) continue;

            try
            {
                var harm = HarmonyManager.GetHarmony();
                var hm = new HarmonyMethod(postfix)
                {
                    before = Array.Empty<string>(),
                    after = Array.Empty<string>(),
                    priority = Priority.Normal,
                };
                harm.Patch(target, postfix: hm);
                MarseyLogger.Log(MarseyLogger.LogType.INFO, $"Type.GetType({string.Join(",", sig.Select(t => t.Name))}) hook installed");
            }
            catch (Exception ex)
            {
                MarseyLogger.Log(MarseyLogger.LogType.FATL, $"Type.GetType({string.Join(",", sig.Select(t => t.Name))}) hook FAILED: {ex.Message}");
            }
        }
    }

    private static void PerjurizeAssemblyLocation()
    {
        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(nameof(HideseyPatches.LieLocation),
            BindingFlags.Public | BindingFlags.Static);
        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "HideseyPatches.LieLocation not found");
            return;
        }

        MethodInfo? target = typeof(Assembly).GetProperty("Location")?.GetGetMethod();
        if (target == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Assembly.Location getter not found");
            return;
        }

        try
        {
            var harm = HarmonyManager.GetHarmony();
            var hm = new HarmonyMethod(postfix)
            {
                before = Array.Empty<string>(),
                after = Array.Empty<string>(),
                priority = Priority.Normal,
            };
            harm.Patch(target, postfix: hm);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "Assembly.Location hook installed");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"Assembly.Location hook FAILED: {ex.Message}");
        }
    }

    private static void PerjurizeStackTrace()
    {
        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(nameof(HideseyPatches.LieStackTrace),
            BindingFlags.Public | BindingFlags.Static);
        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "HideseyPatches.LieStackTrace not found");
            return;
        }

        MethodInfo? target = typeof(Environment).GetProperty("StackTrace")?.GetGetMethod();
        if (target == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Environment.StackTrace getter not found");
            return;
        }

        try
        {
            var harm = HarmonyManager.GetHarmony();
            var hm = new HarmonyMethod(postfix)
            {
                before = Array.Empty<string>(),
                after = Array.Empty<string>(),
                priority = Priority.Normal,
            };
            harm.Patch(target, postfix: hm);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "Environment.StackTrace hook installed");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"Environment.StackTrace hook FAILED: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix on Robust's ReflectionManager.
    /// Must run AFTER engine load — Robust.Shared isn't in AppDomain during Perjurize().
    /// Patches both .Assemblies getter and .FindAllTypes() — both are anticheat detection vectors.
    /// Also hooks IModLoader.LoadedModules and IResourceManager.ContentFindFiles.
    /// </summary>
    [HideLevelRequirement(HideLevel.Duplicit)]
    private static void PerjurizeReflection()
    {
        Type? refManType = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Robust.Shared")
            ?.GetType("Robust.Shared.Reflection.ReflectionManager");

        if (refManType == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Robust.Shared.Reflection.ReflectionManager not found, skipping hook");
            return;
        }

        HookReflectionManagerMember(refManType, "Assemblies", true, nameof(HideseyPatches.LieReflection));
        HookReflectionManagerMember(refManType, "FindAllTypes", false, nameof(HideseyPatches.LieFindAllTypes));

        PerjurizeModLoader();
        PerjurizeContentFindFiles();
        PerjurizeRegistries();
    }

    private static void HookReflectionManagerMember(Type refManType, string memberName, bool isProperty, string postfixName)
    {
        MethodInfo? target = isProperty
            ? refManType.GetProperty(memberName)?.GetGetMethod()
            : refManType.GetMethod(memberName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

        if (target == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, $"ReflectionManager.{memberName} not found");
            return;
        }

        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(postfixName, BindingFlags.Public | BindingFlags.Static);
        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, $"HideseyPatches.{postfixName} not found");
            return;
        }

        try
        {
            var harm = HarmonyManager.GetHarmony();
            var hm = new HarmonyMethod(postfix)
            {
                before = Array.Empty<string>(),
                after = Array.Empty<string>(),
                priority = Priority.Normal,
            };
            harm.Patch(target, postfix: hm);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, $"ReflectionManager.{memberName} hook installed");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"ReflectionManager.{memberName} hook FAILED: {ex.Message}");
        }
    }

    /// <summary>
    /// Hook IModLoader.LoadedModules getter — closes the anticheat's module-enumeration probes
    /// + MSV drift (the specific probe slot numbers shift between anticheat builds).
    /// LoadedModules is declared as virtual on BaseModLoader (the base class); concrete subclasses
    /// (ModLoader, ServerModLoader) inherit without overriding, so we must patch the declared method
    /// on the base. Harmony refuses to patch non-declared methods.
    /// </summary>
    private static void PerjurizeModLoader()
    {
        var robustShared = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Robust.Shared");
        if (robustShared == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Robust.Shared not found, skipping LoadedModules hook");
            return;
        }

        Type? baseModLoader = robustShared.GetType("Robust.Shared.ContentPack.BaseModLoader");
        if (baseModLoader == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "BaseModLoader type not found, skipping LoadedModules hook");
            return;
        }

        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(nameof(HideseyPatches.LieModLoader),
            BindingFlags.Public | BindingFlags.Static);
        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "HideseyPatches.LieModLoader not found");
            return;
        }

        MethodInfo? target = baseModLoader
            .GetProperty("LoadedModules", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetGetMethod(true);

        if (target == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "BaseModLoader.LoadedModules getter not found");
            return;
        }

        try
        {
            var harm = HarmonyManager.GetHarmony();
            var hm = new HarmonyMethod(postfix)
            {
                before = Array.Empty<string>(),
                after = Array.Empty<string>(),
                priority = Priority.Normal,
            };
            harm.Patch(target, postfix: hm);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "BaseModLoader.LoadedModules hook installed");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"BaseModLoader.LoadedModules hook FAILED: {ex.Message}");
        }
    }

    /// <summary>
    /// Hook IResourceManager.ContentFindFiles — closes the anticheat's VFS-scan probes (the
    /// specific probe slot numbers shift between anticheat builds).
    /// Patches base ResourceManager directly (declared method). ResourceCache and any other
    /// subclasses inherit the patched implementation automatically.
    /// </summary>
    private static void PerjurizeContentFindFiles()
    {
        var robustShared = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Robust.Shared");
        if (robustShared == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Robust.Shared not found, skipping ContentFindFiles hook");
            return;
        }

        Type? resMan = robustShared.GetType("Robust.Shared.ContentPack.ResourceManager");
        if (resMan == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "ResourceManager type not found, skipping ContentFindFiles hook");
            return;
        }

        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(nameof(HideseyPatches.LieContentFindFiles),
            BindingFlags.Public | BindingFlags.Static);
        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "HideseyPatches.LieContentFindFiles not found");
            return;
        }

        var candidates = resMan.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(m => m.Name == "ContentFindFiles" && m.DeclaringType == resMan)
            .ToList();

        if (candidates.Count == 0)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "No ContentFindFiles methods found on ResourceManager");
            return;
        }

        var harm = HarmonyManager.GetHarmony();
        foreach (var target in candidates)
        {
            try
            {
                var hm = new HarmonyMethod(postfix)
                {
                    before = Array.Empty<string>(),
                    after = Array.Empty<string>(),
                    priority = Priority.Normal,
                };
                harm.Patch(target, postfix: hm);
                var paramSig = string.Join(",", target.GetParameters().Select(p => p.ParameterType.Name));
                MarseyLogger.Log(MarseyLogger.LogType.INFO, $"ResourceManager.ContentFindFiles({paramSig}) hook installed");
            }
            catch (Exception ex)
            {
                MarseyLogger.Log(MarseyLogger.LogType.FATL, $"ResourceManager.ContentFindFiles hook FAILED: {ex.Message}");
            }
        }
    }

    private static HideLevel GetHideseyLevel()
    {
        string envVar = Environment.GetEnvironmentVariable("MARSEY_HIDE_LEVEL")!;

        if (int.TryParse(envVar, out int hideLevelValue) && Enum.IsDefined(typeof(HideLevel), hideLevelValue))
            return (HideLevel)hideLevelValue;

        return HideLevel.Normal;
    }

    #region LyingPatches

    // Assembly-channel filters use IsHiddenAsm; the three type-channel filters below
    // (LyingTyper / LyingFindAllTypes / LyingGetType) route through the SAME Hidesey.IsHiddenType
    // predicate as the registry hooks in HideseyRegistries.cs — so a type hidden via the manifest
    // disappears from GetTypes / FindAllTypes / Type.GetType and from the EntitySystem / IoC /
    // Component getters consistently, with no cross-channel drift.

    public static Assembly[] LyingDomain(Assembly[] original)
    {
        if (FromHidden()) return original;
        return original.Where(a => !IsHiddenAsm(a)).ToArray();
    }

    public static IEnumerable<Assembly> LyingContext(IEnumerable<Assembly> original)
    {
        if (FromHidden()) return original;
        return original.Where(a => !IsHiddenAsm(a));
    }

    public static IReadOnlyList<Assembly> LyingReflection(IReadOnlyList<Assembly> original)
    {
        if (FromHidden()) return original;
        return original.Where(a => !IsHiddenAsm(a)).ToList();
    }

    public static IEnumerable<Assembly> LyingModLoader(IEnumerable<Assembly> original)
    {
        if (FromHidden()) return original;
        return original.Where(a => !IsHiddenAsm(a)).ToList();
    }

    public static IEnumerable<AssemblyLoadContext> LyingManifest(IEnumerable<AssemblyLoadContext> original)
    {
        return original.Where(context => context.Name != "Assembly.Load(byte[], ...)");
    }

    public static AssemblyName[] LyingReference(AssemblyName[] original)
    {
        if (FromHidden()) return original;
        return original.Where(an => !IsHiddenName(an.Name)).ToArray();
    }

    public static Type[] LyingTyper(Type[] original)
    {
        if (FromHidden()) return original;

        if (!_caching)
            return original.Where(t => !IsHiddenType(t)).ToArray();

        Type[] cached = Facade.Cached;
        if (cached != Array.Empty<Type>()) return cached;

        cached = original.Where(t => !IsHiddenType(t)).ToArray();
        Facade.Cache(cached);
        return cached;
    }

    public static IEnumerable<Type> LyingFindAllTypes(IEnumerable<Type> original)
    {
        if (FromHidden()) return original;
        return original.Where(t => !IsHiddenType(t)).ToList();
    }

    public static Type? LyingGetType(Type? original)
    {
        if (original == null) return null;
        if (FromHidden()) return original;
        if (IsHiddenType(original)) return null;
        return original;
    }

    /// <summary>
    /// Filter for Assembly.Location getter — returns a legitimate-looking path for hidden assemblies.
    /// </summary>
    public static string LyingLocation(string original, Assembly instance)
    {
        if (FromHidden()) return original;
        if (!IsHiddenAsm(instance)) return original;

        // Return Robust.Client.Location — guaranteed to exist, looks normal.
        try
        {
            var robust = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Robust.Client");
            if (robust != null && !robust.IsDynamic)
                return robust.Location;
        }
        catch { }
        return string.Empty;
    }

    /// <summary>
    /// Filter Environment.StackTrace string — drop lines that mention hidden assemblies.
    /// Delegates the per-line decision to Hidesey.StackLineMentionsHidden (see HideseyRegistries),
    /// which sources hidden names dynamically from the hidden-assembly list + infra keywords +
    /// dynamic Harmony asm names + manifest StackNames.
    /// </summary>
    public static string LyingStackTrace(string original)
    {
        if (FromHidden()) return original;
        if (string.IsNullOrEmpty(original)) return original;

        var sb = new System.Text.StringBuilder(original.Length);
        bool hadAny = false;
        foreach (var line in original.Split('\n'))
        {
            if (StackLineMentionsHidden(line)) continue;
            if (hadAny) sb.Append('\n');
            sb.Append(line);
            hadAny = true;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Expose the hidden assemblies list to other Marsey-context code (e.g. SourceDumper).
    /// </summary>
    public static IReadOnlyList<Assembly> GetHiddenAssemblies() => _hideseys.AsReadOnly();

    #endregion

    internal static bool FromContent()
    {
        StackTrace stackTrace = new();

        foreach (StackFrame frame in stackTrace.GetFrames())
        {
            MethodBase? method = frame.GetMethod();
            if (method == null || method.DeclaringType == null) continue;
            string? namespaceName = method.DeclaringType.Namespace;
            if (!string.IsNullOrEmpty(namespaceName) && namespaceName.StartsWith("Content."))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Thread-local override for FromHidden() — when set, FromHidden returns false even if caller
    /// is a hidden assembly. Used by SourceDumper to capture "engine view" — what the anticheat
    /// would see if it called the hooked getter itself.
    ///
    /// Usage:
    ///   using (Hidesey.ForceEngineView())
    ///   {
    ///       // Any call to a hooked getter here will see filtered/sanitized result.
    ///       var modules = modLoader.LoadedModules.ToList();
    ///   }
    /// </summary>
    public static IDisposable ForceEngineView()
    {
        return new EngineViewScope();
    }

    [ThreadStatic] private static bool _forceEngineView;

    public static bool IsEngineViewForced() => _forceEngineView;

    private sealed class EngineViewScope : IDisposable
    {
        private readonly bool _prev;
        public EngineViewScope() { _prev = _forceEngineView; _forceEngineView = true; }
        public void Dispose() { _forceEngineView = _prev; }
    }

    /// <summary>
    /// Check if the call originated from a hidden assembly (our own mod code).
    /// Used by Lying* methods to provide asymmetric filtering — mods see truth, everyone else sees filtered list.
    /// </summary>
    internal static bool FromHidden()
    {
        // Engine-view scope: pretend we're external caller.
        if (_forceEngineView) return false;

        if (_inFromHidden) return false;
        _inFromHidden = true;
        try
        {
            StackTrace stackTrace = new(0, false);
            var frames = stackTrace.GetFrames();

            foreach (var frame in frames)
            {
                MethodBase? method = frame.GetMethod();
                Assembly? asm = method?.DeclaringType?.Assembly;
                if (asm == null) continue;

                if (asm.IsDynamic) continue;

                if (_knownEngineAsms.Contains(asm))
                    return false;

                var asmName = asm.GetName().Name ?? "";

                if (asmName.Contains("Harmony") || asmName.Contains("MonoMod") || asmName.Contains("Mono.Cecil"))
                    continue;

                if (asmName == "Marsey")
                    continue;

                bool isHidden = _hideseys.Contains(asm);
                if (!isHidden && (asmName.StartsWith("Robust.") || asmName.StartsWith("Content.") || asmName.StartsWith("System.")))
                    _knownEngineAsms.Add(asm);

                return isHidden;
            }

            return false;
        }
        finally
        {
            _inFromHidden = false;
        }
    }
}
