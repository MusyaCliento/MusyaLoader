using System;
using System.Reflection;
using Marsey.Misc;
using Marsey.Stealthsey;

namespace Marsey.Subversion;

/// <summary>
///     Manages the Hidesey patch helper class.
///     The subverter patch carries a class named "Sedition" with public static delegate fields;
///     at load time we find that class by name and bind our bridge methods to those fields by
///     reflection (the patch does not reference Marsey).
/// </summary>
public static class Sedition
{
    /// <summary>
    /// Hides a subversion module from the game.
    /// </summary>
    /// <remarks>Assigned to the patch's hideDelegate field.</remarks>
    private static void HideDelegate(Assembly asm)
    {
        MarseyLogger.Log(MarseyLogger.LogType.DEBG, "Hiding");
        Hidesey.HidePatch(asm);
    }

    /// <summary>
    /// Builds a real HideManifest from the primitive bridge arguments and applies it.
    /// </summary>
    /// <remarks>Assigned to the patch's hideManifestDelegate field.</remarks>
    private static void HideManifestDelegate(
        string[] systems, string[] comps, string[] ioc,
        string[] cvars, string[] commands, string[] stackNames)
    {
        var manifest = new HideManifest();
        if (systems    != null) manifest.Systems.AddRange(systems);
        if (comps      != null) manifest.Comps.AddRange(comps);
        if (ioc        != null) manifest.Ioc.AddRange(ioc);
        if (cvars      != null) manifest.Cvars.AddRange(cvars);
        if (commands   != null) manifest.Commands.AddRange(commands);
        if (stackNames != null) manifest.StackNames.AddRange(stackNames);

        Hidesey.Apply(manifest);
    }

    public static void InitSedition(Assembly assembly, string? assemblyName)
    {
        Type? seditionType = assembly.GetType("Sedition");
        if (seditionType == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"{assemblyName} has no Hidesey class");
            return;
        }

        SetupSedition(seditionType);
    }

    private static void SetupSedition(Type sedition)
    {
        BindDelegate(sedition, "hideDelegate",         nameof(HideDelegate));
        BindDelegate(sedition, "hideManifestDelegate", nameof(HideManifestDelegate));
    }

    /// <summary>
    /// Bind a static bridge method on this class to a public static delegate field on the patch's
    /// Sedition class. Missing fields are non-fatal (older patch without the manifest bridge) —
    /// logged and skipped so the rest still binds.
    /// </summary>
    private static void BindDelegate(Type sedition, string fieldName, string methodName)
    {
        MethodInfo? bridge = typeof(Sedition).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo? field = sedition.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

        if (bridge == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.ERRO, $"Sedition bridge method '{methodName}' not found");
            return;
        }

        if (field == null)
        {
            // Patch may predate this delegate — not fatal.
            MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"Sedition field '{fieldName}' not present on patch, skipping");
            return;
        }

        try
        {
            Delegate del = Delegate.CreateDelegate(field.FieldType, bridge);
            field.SetValue(null, del);
            MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"Sedition delegate '{fieldName}' bound");
        }
        catch (Exception e)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"Failed to assign sedition delegate '{fieldName}': {e.Message}");
        }
    }
}
