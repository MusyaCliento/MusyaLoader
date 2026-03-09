using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Marsey.Misc;

namespace Marsey.Game.Resources.Dumper.Resource;

internal static class ResDumpPatches
{
    private static readonly object ExitLock = new();
    private static CancellationTokenSource? _exitCts;
    private const int ExitDelayMs = 10000;

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void PostfixCFF(ref object __instance, ref dynamic __result)
    {
        if (ResourceDumper.CFRMi == null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "[DUMPER] CFRMi is null — skipping dump pass.");
            return;
        }

        int dumped = 0;

        foreach (dynamic file in __result)
        {
            try
            {
                string canonPath = file.CanonPath;
                string fixedCanonPath = canonPath.StartsWith("/") ? canonPath[1..] : canonPath;
                string fullpath = Path.Combine(MarseyDumper.path, fixedCanonPath);

                FileHandler.CreateDir(fullpath);

                object? CFRout = ResourceDumper.CFRMi.Invoke(__instance, new object?[] { file });

                if (CFRout is not MemoryStream stream)
                {
                    MarseyLogger.Log(MarseyLogger.LogType.WARN, $"[DUMPER] Could not dump file: {canonPath} (invalid stream)");
                    continue;
                }

                FileHandler.SaveToFile(fullpath, stream);
                dumped++;
            }
            catch (Exception ex)
            {
                MarseyLogger.Log(MarseyLogger.LogType.FATL, $"[DUMPER] Failed to dump a resource file! {ex}");
            }
        }

        MarseyLogger.Log(MarseyLogger.LogType.INFO, $"[DUMPER] Dump pass complete — dumped {dumped} files.");

        try
        {
            lock (ExitLock)
            {
                _exitCts?.Cancel();
                _exitCts = new CancellationTokenSource();
                CancellationToken token = _exitCts.Token;

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(ExitDelayMs, token).ConfigureAwait(false);
                        MarseyLogger.Log(MarseyLogger.LogType.INFO, "[DUMPER] No activity — exiting process to disconnect from server.");
                        Environment.Exit(0);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        MarseyLogger.Log(MarseyLogger.LogType.WARN, $"[DUMPER] Failed to exit: {ex}");
                    }
                }, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, $"[DUMPER] Failed to schedule exit: {ex}");
        }
    }
}
