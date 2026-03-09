using System.Reflection;
using HarmonyLib;
using Marsey.Game.Resources.Reflection;
using Marsey.Handbreak;
using Marsey.Misc;

namespace Marsey.Game.Resources.Dumper.Resource;

/// <summary>
/// By complete accident this dumps everything
/// </summary>
public static class ResourceDumper
{
    public static MethodInfo? CFRMi;
    private static MethodInfo? _contentFindFilesMethod;

    public static void Patch()
    {
        try
        {
            FileHandler.CheckRenameDirectory(MarseyDumper.path);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "[DUMPER] Preparing to patch resource manager...");

            if (ResourceTypes.ProtoMan == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.FATL, "[DUMPER] PrototypeManager is null!");
                return;
            }

            if (ResourceTypes.ResPath == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.FATL, "[DUMPER] ResPath is null!");
                return;
            }

            CFRMi = AccessTools.Method(ResourceTypes.ProtoMan, "ContentFileRead", new[] { ResourceTypes.ResPath });

            if (CFRMi == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.FATL, "[DUMPER] Failed to locate ContentFileRead method!");
                return;
            }

            _contentFindFilesMethod = AccessTools.Method(ResourceTypes.ProtoMan, "ContentFindFiles", new[] { ResourceTypes.ResPath });

            Helpers.PatchMethod(
                targetType: ResourceTypes.ProtoMan,
                targetMethodName: "ContentFindFiles",
                patchType: typeof(ResDumpPatches),
                patchMethodName: "PostfixCFF",
                patchingType: HarmonyPatchType.Postfix,
                new Type[] { ResourceTypes.ResPath }
            );

            MarseyLogger.Log(MarseyLogger.LogType.INFO, "[DUMPER] Resource patch successfully applied!");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"[DUMPER] Failed while applying resource patch! {ex}");
        }
    }

    /// <summary>
    /// Removes the dump patch from ContentFindFiles
    /// </summary>
    public static void Unpatch()
    {
        try
        {
            if (_contentFindFilesMethod == null)
            {
                MarseyLogger.Log(MarseyLogger.LogType.WARN, "[DUMPER] ContentFindFiles method is null, cannot unpatch.");
                return;
            }

            Manual.Unpatch(_contentFindFilesMethod, HarmonyPatchType.Postfix);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "[DUMPER] Dumper patch successfully removed!");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.FATL, $"[DUMPER] Failed to unpatch dumper! {ex}");
        }
    }
}
