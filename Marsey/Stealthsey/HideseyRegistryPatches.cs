using System.Collections;
using System.Reflection;

namespace Marsey.Stealthsey;

/// <summary>
/// Postfixes for the wide patch-hiding registry hooks (partial of <see cref="HideseyPatches"/>).
/// The three type-getters share ONE postfix (LieTypeList) — all routed through the unified
/// Hidesey.IsHiddenType predicate, so EntitySystem / IoC / Component channels stay consistent
/// with each other and with the old FindAllTypes / GetTypes / GetType filters.
/// The command dictionary is reconstructed generically via reflection so we never hard-link
/// IConsoleCommand.
/// </summary>
public static partial class HideseyPatches
{
    // Shared by GetEntitySystemTypes / GetRegisteredTypes / AllRegisteredTypes (all IEnumerable<Type>)
    public static void LieTypeList(ref IEnumerable<Type> __result)
    {
        if (__result == null) return;
        __result = Hidesey.LyingTypeList(__result);
    }

    // ConfigurationManager.GetRegisteredCVars() : IEnumerable<string>
    public static void LieCvars(ref IEnumerable<string> __result)
    {
        if (__result == null) return;
        __result = Hidesey.LyingCvars(__result);
    }

    /// <summary>
    /// ClientConsoleHost.AvailableCommands getter : IReadOnlyDictionary&lt;string, IConsoleCommand&gt;.
    /// Patches the override on ClientConsoleHost, not the base ConsoleHost getter (see hook 5 in
    /// PerjurizeRegistries — the base getter is never invoked on the client). Rebuilds a new
    /// dictionary of the same generic args minus hidden command keys. Uses `ref object __result`
    /// (same pattern as LieContentFindFiles) to avoid a hard Robust dep. Unconditional (no
    /// FromHidden) — by design. Only reallocates when something was removed.
    /// </summary>
    public static void LieCommands(ref object __result)
    {
        if (__result == null) return;
        if (!Hidesey.AnyHiddenCommands()) return;

        try
        {
            var (kType, vType) = ExtractDictionaryArgs(__result.GetType());
            if (vType == null || kType != typeof(string)) return;   // commands are keyed by string
            if (__result is not IEnumerable entries) return;

            var newDictType = typeof(Dictionary<,>).MakeGenericType(kType, vType);
            var newDict = (IDictionary)Activator.CreateInstance(newDictType)!;

            PropertyInfo? keyProp = null, valProp = null;
            int removed = 0;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (keyProp == null)
                {
                    var et = entry.GetType();
                    keyProp = et.GetProperty("Key");
                    valProp = et.GetProperty("Value");
                    if (keyProp == null || valProp == null) return; // unknown shape — leave untouched
                }

                var key = keyProp.GetValue(entry) as string;
                if (key != null && Hidesey.IsHiddenCommand(key)) { removed++; continue; }
                newDict[key!] = valProp!.GetValue(entry);
            }

            if (removed > 0)
                __result = newDict;
        }
        catch { /* never break the console */ }
    }

    private static (Type? key, Type? val) ExtractDictionaryArgs(Type t)
    {
        foreach (var iface in t.GetInterfaces())
        {
            if (!iface.IsGenericType) continue;
            var def = iface.GetGenericTypeDefinition();
            if (def == typeof(IReadOnlyDictionary<,>) || def == typeof(IDictionary<,>))
            {
                var args = iface.GetGenericArguments();
                return (args[0], args[1]);
            }
        }
        return (null, null);
    }
}
