using System.Reflection;
using HarmonyLib;
using Marsey.Config;
using Marsey.Game.Managers;
using Marsey.Game.Misc;
using Marsey.Handbreak;
using Marsey.Misc;

namespace Marsey.Game.Patches;

/// <summary>
/// Hooks to EntryPoint
/// Signals to Marsey when the game proper is going to start
/// </summary>
public static class Sentry
{
    private static bool _starting;
    public static bool State => _starting;
    
    public static void Patch()
    {
        MethodInfo? PrefEP = typeof(Sentry).GetMethod("PrefBLR", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? PrefAH = typeof(Sentry).GetMethod("PrefAH", BindingFlags.Static | BindingFlags.NonPublic);
        
        Type EP = AccessTools.TypeByName("Robust.Shared.ContentPack.BaseModLoader");
        MethodInfo? InitMi = AccessTools.Method(EP, "BroadcastRunLevel");

        Manual.Patch(InitMi, PrefEP, HarmonyPatchType.Prefix);

        var gcType = AccessTools.TypeByName("Robust.Client.GameController");
        var typeInitializer = gcType?.TypeInitializer;

        if (typeInitializer == null || PrefAH == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Sentry", "Could not apply anti-Harmony bypass: target constructor not found.");
            return;
        }

        HarmonyManager.GetHarmony().Patch(typeInitializer, prefix: new HarmonyMethod(PrefAH));
        MarseyLogger.Log(MarseyLogger.LogType.DEBG, "Sentry", "Applied anti-Harmony bypass for GameController static constructor.");
    }

    private static void PrefBLR(ref object level)
    {
        if (level is not Enum || Convert.ToInt32(level) != 1) return; // ModRunLevel.Init
        MarseyLogger.Log(MarseyLogger.LogType.DEBG, "Sentry set");
        _starting = true;
    }

    private static bool PrefAH()
    {
        return false;
    }
}
