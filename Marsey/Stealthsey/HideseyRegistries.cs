using System.Reflection;
using HarmonyLib;
using Marsey.Game.Managers;
using Marsey.Misc;
using Marsey.Stealthsey.Reflection;

namespace Marsey.Stealthsey;

/// <summary>
/// Wide patch-hiding subsystem (partial of <see cref="Hidesey"/>).
///
/// Lets a patch declare, by name, registered engine objects it wants kept out of the
/// anticheat's view. Blacklist model: ONLY what a patch explicitly lists is hidden — system /
/// vanilla objects are untouched. Registration is by string (FullName / registered name) so it
/// can run from the loader Entry before the target types are even loaded.
///
/// Five separate storage registries; type categories funnel through ONE predicate
/// (<see cref="IsHiddenType"/>) so every type channel — old and new — stays consistent:
///   _hiddenSystems  ┐
///   _hiddenComps    ├─ IsHiddenType ──> LieTypeList  (GetEntitySystemTypes / GetRegisteredTypes
///   _hiddenIoc      ┘                                 / AllRegisteredTypes)
///                              and also old LyingFindAllTypes / LyingTyper / LyingGetType
///   _hiddenCvars    ───────────────────> LieCvars    (GetRegisteredCVars)
///   _hiddenCommands ───────────────────> LieCommands (ClientConsoleHost.AvailableCommands,
///                                                      unconditional)
///
/// Type/cvar filters are asymmetric via FromHidden() — our own mod code sees the truth, anyone
/// else (anticheat, engine) sees the filtered list. Command removal is UNCONDITIONAL by design.
/// </summary>
public static partial class Hidesey
{
    // ── Storage registries ─────────────────────────────────────────────────────
    // Separate per-category sets (not one shared) for clarity / per-category semantics.
    private static readonly object _hideRegLock = new();
    private static readonly HashSet<string> _hiddenSystems  = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _hiddenComps    = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _hiddenIoc      = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _hiddenCvars    = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _hiddenCommands = new(StringComparer.OrdinalIgnoreCase);

    // Lock-free fast path for the type filter: true once any type-category registry is non-empty.
    private static volatile bool _anyHiddenTypeNames;

    // Names we already warned about (networked refusal) — getters are called repeatedly, warn once.
    private static readonly HashSet<string> _warnedNetworked = new(StringComparer.Ordinal);

    // Cached runtime-resolved [NetworkedComponent] attribute type (Robust isn't referenced directly).
    private static Type? _networkedCompAttr;
    private static bool _networkedCompAttrResolved;

    // ── Stack-trace name filtering ──────────────────────────────────────────────
    // Infra of the patching framework — always scrubbed from the stack, regardless of registries.
    // (Namespace forms like "HarmonyLib" don't equal the assembly name "0Harmony", so they're
    // listed explicitly here.)
    private static readonly string[] _stackInfraKeywords =
    {
        "0Harmony", "HarmonyLib", "MonoMod", "Mono.Cecil",
    };

    // Extensible: seeded with our own names (timing-independent), plus manifest StackNames.
    // Hidden assemblies (HidePatch) are matched dynamically from the hidden-assembly list too,
    // so this is mostly a safety seed + escape hatch.
    private static readonly HashSet<string> _stackHiddenExtra =
        new(StringComparer.OrdinalIgnoreCase) { "Marsey", "Despada", "SourceDumper" };

    // ── Public API: core (manifest) ─────────────────────────────────────────────

    /// <summary>
    /// Apply a hide manifest — the single merge/log point for the wide patch-hiding system.
    /// Thread-safe, idempotent. Networked components are still registered here; the guard runs
    /// lazily in <see cref="IsHiddenType"/> when the type is actually loaded.
    /// Call from your patch Entry, before content load — names are matched lazily by the hooked
    /// getters, so the targets do not need to exist yet.
    /// </summary>
    [HideLevelRequirement(HideLevel.Normal)]
    public static void Apply(HideManifest manifest)
    {
        if (manifest == null) return;

        lock (_hideRegLock)
        {
            MergeInto(_hiddenSystems,  manifest.Systems);
            MergeInto(_hiddenComps,    manifest.Comps);
            MergeInto(_hiddenIoc,      manifest.Ioc);
            MergeInto(_hiddenCvars,    manifest.Cvars);
            MergeInto(_hiddenCommands, manifest.Commands);
            MergeInto(_stackHiddenExtra, manifest.StackNames);

            _anyHiddenTypeNames = _hiddenSystems.Count > 0 || _hiddenComps.Count > 0 || _hiddenIoc.Count > 0;
        }

        MarseyLogger.Log(MarseyLogger.LogType.DEBG,
            "Hidesey.Apply: " +
            $"systems=[{string.Join(", ", manifest.Systems)}] " +
            $"comps=[{string.Join(", ", manifest.Comps)}] " +
            $"ioc=[{string.Join(", ", manifest.Ioc)}] " +
            $"cvars=[{string.Join(", ", manifest.Cvars)}] " +
            $"commands=[{string.Join(", ", manifest.Commands)}] " +
            $"stackNames=[{string.Join(", ", manifest.StackNames)}]");
    }

    private static void MergeInto(HashSet<string> set, List<string> incoming)
    {
        if (incoming == null) return;
        foreach (var s in incoming)
            if (!string.IsNullOrWhiteSpace(s))
                set.Add(s);
    }

    // ── Public API: thin wrappers (facade over Apply, grep-friendly) ─────────────

    public static void HideSystems(params string[] fullNames)
    { var m = new HideManifest(); m.Systems.AddRange(fullNames); Apply(m); }

    public static void HideComps(params string[] fullNames)
    { var m = new HideManifest(); m.Comps.AddRange(fullNames); Apply(m); }

    public static void HideIoc(params string[] fullNames)
    { var m = new HideManifest(); m.Ioc.AddRange(fullNames); Apply(m); }

    public static void HideCvars(params string[] names)
    { var m = new HideManifest(); m.Cvars.AddRange(names); Apply(m); }

    public static void HideCommands(params string[] names)
    { var m = new HideManifest(); m.Commands.AddRange(names); Apply(m); }

    public static void HideFromStackTrace(params string[] names)
    { var m = new HideManifest(); m.StackNames.AddRange(names); Apply(m); }

    // Singular aliases (read nicely for one-offs)
    public static void HideSystem(string fullName) => HideSystems(fullName);
    public static void HideComp(string fullName)   => HideComps(fullName);
    public static void HideCommand(string name)    => HideCommands(name);
    public static void HideCvar(string name)       => HideCvars(name);

    // ── Unified type predicate (Вид 2) ──────────────────────────────────────────

    /// <summary>
    /// Single source of truth for "is this type hidden". Used by EVERY type channel — the new
    /// registry postfixes AND the old LyingFindAllTypes / LyingTyper / LyingGetType — so a type
    /// hidden via the manifest disappears from all of them consistently (no cross-channel drift).
    /// Hidden if: its assembly is hidden, OR its FullName is in any type-category registry.
    /// Networked components are NEVER hidden (would break the server-verified net hash) — the
    /// guard lives here so it applies to every channel at once.
    /// </summary>
    public static bool IsHiddenType(Type t)
    {
        if (t == null) return false;
        if (IsHiddenAsm(t.Assembly)) return true;        // whole-assembly hiding (existing semantics)
        if (!_anyHiddenTypeNames) return false;          // lock-free fast path: nothing registered

        var fn = t.FullName;
        if (fn == null) return false;

        bool inComps, inOther;
        lock (_hideRegLock)
        {
            inOther = _hiddenSystems.Contains(fn) || _hiddenIoc.Contains(fn);
            inComps = !inOther && _hiddenComps.Contains(fn);
        }

        if (inOther) return true;
        if (!inComps) return false;

        // Component category — networked guard (reflection + logging done outside the lock).
        if (IsNetworkedComponent(t))
        {
            WarnNetworkedRefusal(fn);
            return false;                                // leave VISIBLE — hiding would break net hash
        }
        return true;
    }

    /// <summary>
    /// Filter for the three registry type-getters (EntitySystem / IoC / Component). Asymmetric:
    /// our own code sees the truth. Old type filters live in Hidesey.cs and call IsHiddenType too.
    /// </summary>
    public static IEnumerable<Type> LyingTypeList(IEnumerable<Type> original)
    {
        if (original == null) return original!;
        if (FromHidden()) return original;
        return original.Where(t => t != null && !IsHiddenType(t)).ToList();
    }

    // ── CVar / command filters ──────────────────────────────────────────────────

    public static IEnumerable<string> LyingCvars(IEnumerable<string> original)
    {
        if (original == null) return original!;
        if (FromHidden()) return original;

        bool empty;
        lock (_hideRegLock) empty = _hiddenCvars.Count == 0;
        if (empty) return original;

        var result = new List<string>();
        foreach (var name in original)
        {
            bool hidden;
            lock (_hideRegLock) hidden = name != null && _hiddenCvars.Contains(name);
            if (!hidden) result.Add(name);
        }
        return result;
    }

    /// <summary>
    /// Command-name membership test for the ClientConsoleHost.AvailableCommands postfix. Removal
    /// is UNCONDITIONAL (no FromHidden) by design — the dev invokes the hidden command's logic
    /// from their own UI, not via console lookup. This hooks the AvailableCommands *getter* (what
    /// the anticheat enumerates) only. Verified against SS14: ConsoleHost.ExecuteCommand resolves
    /// through an internal field, NOT this getter, so a hidden command stays fully invokable from
    /// the in-game console while being absent from enumeration / cmd:custom probes. No trade-off.
    /// </summary>
    public static bool IsHiddenCommand(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        lock (_hideRegLock) return _hiddenCommands.Contains(name);
    }

    public static bool AnyHiddenCommands()
    {
        lock (_hideRegLock) return _hiddenCommands.Count > 0;
    }

    // ── Stack-trace line predicate (Вид 1) ──────────────────────────────────────

    /// <summary>
    /// True if a stack-trace line mentions hidden code: patching infra, a dynamic Harmony asm
    /// name, the name of an actually-hidden assembly (dynamic), or an extensible extra keyword.
    /// Used by LyingStackTrace (in Hidesey.cs).
    /// </summary>
    public static bool StackLineMentionsHidden(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;

        foreach (var kw in _stackInfraKeywords)
            if (line.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;

        // dynamic runtime asm names (Harmony emits) — was NOT scrubbed before; closes that gap
        foreach (var kw in _dynamicHiddenNames)
            if (line.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;

        // names of actually-hidden assemblies (dynamic — picks up HidePatch'd asms automatically)
        foreach (var asm in _hideseys)
        {
            string? n;
            try { n = asm.GetName().Name; } catch { continue; }
            if (!string.IsNullOrEmpty(n) && line.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
        }

        lock (_hideRegLock)
            foreach (var kw in _stackHiddenExtra)
                if (line.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    // ── Networked-component guard ────────────────────────────────────────────────

    private static Type? GetNetworkedComponentAttr()
    {
        if (_networkedCompAttrResolved) return _networkedCompAttr;
        _networkedCompAttrResolved = true;
        try
        {
            var robustShared = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Robust.Shared");
            _networkedCompAttr = robustShared?.GetType("Robust.Shared.GameStates.NetworkedComponentAttribute");
            if (_networkedCompAttr == null)
                MarseyLogger.Log(MarseyLogger.LogType.WARN,
                    "Hidesey: NetworkedComponentAttribute not resolved — component hiding will be conservative");
        }
        catch { _networkedCompAttr = null; }
        return _networkedCompAttr;
    }

    private static bool IsNetworkedComponent(Type t)
    {
        var attr = GetNetworkedComponentAttr();
        if (attr == null) return true;                   // can't verify → refuse to hide (avoid net-hash kick)
        try { return Attribute.IsDefined(t, attr); }
        catch { return true; }                            // conservative
    }

    private static void WarnNetworkedRefusal(string fullName)
    {
        lock (_hideRegLock)
            if (!_warnedNetworked.Add(fullName)) return;

        MarseyLogger.Log(MarseyLogger.LogType.WARN,
            $"Hidesey: refusing to hide networked component '{fullName}' — it is part of the " +
            "server-verified network component hash; hiding it would cause a connect-time kick. " +
            "Make it non-networked, or invoke its logic without registering the component.");
    }

    // ── Hook registration (call from PerjurizeReflection, AFTER engine load) ──────

    /// <summary>
    /// Hooks the five registry getters used by the wide patch-hiding system. All resolved by
    /// runtime reflection against Robust types; on a miss logs WARN and skips (never crashes).
    /// The three type-getters share ONE postfix (LieTypeList) routed through IsHiddenType.
    /// </summary>
    private static void PerjurizeRegistries()
    {
        var robustShared = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Robust.Shared");
        if (robustShared == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Robust.Shared not found, skipping registry hooks");
            return;
        }

        // 1. EntitySystemManager.GetEntitySystemTypes() : IEnumerable<Type>   (sealed partial)
        HookDeclaredMethod(robustShared, "Robust.Shared.GameObjects.EntitySystemManager",
            "GetEntitySystemTypes", Type.EmptyTypes, nameof(HideseyPatches.LieTypeList));

        // 2. DependencyCollection.GetRegisteredTypes() : IEnumerable<Type>    (internal sealed)
        //    Recurses into _parentCollection.GetRegisteredTypes() (same type), so the one declared
        //    method covers both AC access paths: _sysMan.DependencyCollection and IoCManager.Instance.
        HookDeclaredMethod(robustShared, "Robust.Shared.IoC.DependencyCollection",
            "GetRegisteredTypes", Type.EmptyTypes, nameof(HideseyPatches.LieTypeList));

        // 3. ComponentFactory.AllRegisteredTypes getter : IEnumerable<Type>   (internal [Virtual])
        //    [Virtual] is Robust's DI attribute, not C# virtual. The getter is declared here and
        //    ServerComponentFactory (the only subclass) does NOT override it, so the declared
        //    getter is the one invoked on every side.
        HookDeclaredGetter(robustShared, "Robust.Shared.GameObjects.ComponentFactory",
            "AllRegisteredTypes", nameof(HideseyPatches.LieTypeList));

        // 4. ConfigurationManager.GetRegisteredCVars() : IEnumerable<string>
        //    Declared directly on ConfigurationManager (Robust.Shared), not virtual/override —
        //    the client/server config managers inherit it without redeclaring, so the declared
        //    method is the one invoked. (HookDeclaredMethod still walks the base chain defensively
        //    in case a future Robust version moves it onto a base class.)
        HookDeclaredMethod(robustShared, "Robust.Shared.Configuration.ConfigurationManager",
            "GetRegisteredCVars", Type.EmptyTypes, nameof(HideseyPatches.LieCvars));

        // 5. ConsoleHost.AvailableCommands — OVERRIDDEN in ClientConsoleHost (Robust.Client).
        //    The base ConsoleHost.AvailableCommands getter is virtual and is NOT the one invoked
        //    on the client (ClientConsoleHost overrides it, returning its own _availableCommands
        //    which also includes server-replicated dummy commands). Patching the base getter is a
        //    no-op on the client, so we patch the override declared on ClientConsoleHost.
        var robustClient = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Robust.Client");
        if (robustClient != null)
        {
            HookDeclaredGetter(robustClient, "Robust.Client.Console.ClientConsoleHost",
                "AvailableCommands", nameof(HideseyPatches.LieCommands));
        }
        else
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Robust.Client not found, skipping AvailableCommands hook");
        }
    }

    private static void HookDeclaredMethod(Assembly asm, string typeName, string methodName, Type[] argTypes, string postfixName)
    {
        try
        {
            Type? t = asm.GetType(typeName);
            if (t == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.WARN, $"PerjurizeRegistries: type {typeName} not found — hook skipped");
                return;
            }

            MethodInfo? target = null;
            for (Type? cur = t; cur != null && target == null; cur = cur.BaseType)
                target = cur.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, argTypes, null);

            if (target == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.WARN, $"PerjurizeRegistries: {typeName}.{methodName}() not found — hook skipped");
                return;
            }

            PatchWithPostfix(target, postfixName, $"{target.DeclaringType?.Name}.{methodName}");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"PerjurizeRegistries: {typeName}.{methodName} hook FAILED: {ex.Message}");
        }
    }

    private static void HookDeclaredGetter(Assembly asm, string typeName, string propertyName, string postfixName)
    {
        try
        {
            Type? t = asm.GetType(typeName);
            if (t == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.WARN, $"PerjurizeRegistries: type {typeName} not found — hook skipped");
                return;
            }

            MethodInfo? target = null;
            for (Type? cur = t; cur != null && target == null; cur = cur.BaseType)
            {
                var prop = cur.GetProperty(propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                target = prop?.GetGetMethod(true);
            }

            if (target == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.WARN, $"PerjurizeRegistries: {typeName}.{propertyName} getter not found — hook skipped");
                return;
            }

            PatchWithPostfix(target, postfixName, $"{target.DeclaringType?.Name}.{propertyName}");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"PerjurizeRegistries: {typeName}.{propertyName} hook FAILED: {ex.Message}");
        }
    }

    private static void PatchWithPostfix(MethodInfo target, string postfixName, string label)
    {
        MethodInfo? postfix = typeof(HideseyPatches).GetMethod(postfixName, BindingFlags.Public | BindingFlags.Static);
        if (postfix == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, $"HideseyPatches.{postfixName} not found");
            return;
        }

        var harm = HarmonyManager.GetHarmony();
        var hm = new HarmonyMethod(postfix)
        {
            before = Array.Empty<string>(),
            after = Array.Empty<string>(),
            priority = Priority.Normal,
        };
        harm.Patch(target, postfix: hm);
        MarseyLogger.Log(MarseyLogger.LogType.INFO, $"{label} hook installed");
    }
}
