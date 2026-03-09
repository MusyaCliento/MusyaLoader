using Marsey.Config;
using Marsey.Game.Resources.Dumper.Resource;
using Marsey.Misc;

namespace Marsey.Game.Resources.Dumper;

/// <summary>
/// Dumps game content
/// </summary>
public static class MarseyDumper
{
    public static string path = "marsey";

    public static void Start()
    {
        MarseyLogger.Log(MarseyLogger.LogType.INFO, "[DUMPER] Starting MarseyDumper...");

        try
        {
            GetExactPath();
            Patch();

            MarseyLogger.Log(MarseyLogger.LogType.INFO, $"[DUMPER] Dumping initialized. Saving resources to: {path}");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"[DUMPER] Failed to initialize MarseyDumper! {ex}");
        }
    }

    private static void GetExactPath()
    {
        string fork = ResMan.GetForkID() ?? "marsey";

        // Используем MarseyFolder вместо создания папки Dumper
        path = Path.Combine(MarseyVars.MarseyDumperFolder, "Dumps", fork);

        MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"[DUMPER] Path resolved: {path}");
    }

    private static void Patch()
    {
        MarseyLogger.Log(MarseyLogger.LogType.DEBG, "[DUMPER] Applying resource patches...");
        ResourceDumper.Patch();
    }
}
