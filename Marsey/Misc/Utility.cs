using System;
using System.Reflection;
using Marsey.Config;
using Marsey.IPC;
using Marsey.PatchAssembly;
using Marsey.Stealthsey;

namespace Marsey.Misc;

public static class MarseyLogger
{
    public enum LogType
    {
        INFO,
        WARN,
        ERRO,
        FATL,
        DEBG,
        TRCE
    }

    /// <summary>
    /// Log function used by the loader
    /// </summary>
    /// <param name="logType">Log level</param>
    /// <param name="message">Log message</param>
    public static void Log(LogType logType, string message)
    {
        if (logType == LogType.DEBG && MarseyConf.DebugAllowed != true)
            return;

        SharedLog(logType, $"[{logType}] {message}");
    }

    /// <summary>
    /// Ditto but specifying a system used
    /// </summary>
    public static void Log(LogType logType, string system, string message)
    {
        switch (logType)
        {
            case LogType.DEBG when MarseyConf.DebugAllowed != true:
            case LogType.TRCE when MarseyConf.TraceAllowed != true:
                return;
            default:
                SharedLog(logType, $"[{logType}] [{system}] {message}");
                break;
        }
    }

    /// <summary>
    /// Log function used by patches
    /// </summary>
    /// <param name="asm">Assembly name of patch</param>
    /// <param name="message">Log message</param>
    /// <see cref="AssemblyFieldHandler.SetupLogger"/>
    public static void Log(AssemblyName asm, string message)
    {
        SharedLog(LogType.INFO, $"[{asm.Name}] {message}");
    }

    private static void SharedLog(LogType logType, string message)
    {
        if (logType is not (LogType.FATL or LogType.WARN) && !MarseyConf.Logging)
            return;

        string line = $"[{MarseyVars.MarseyLoggerPrefix}] {message}";
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = GetColor(logType);
        Console.WriteLine(line);
        Console.ForegroundColor = previous;
    }

    private static ConsoleColor GetColor(LogType logType)
    {
        return logType switch
        {
            LogType.TRCE => ConsoleColor.Green,
            LogType.INFO => ConsoleColor.Blue,
            LogType.WARN => ConsoleColor.Yellow,
            LogType.ERRO => ConsoleColor.Red,
            LogType.FATL => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }
}
public abstract class Utility
{
    public static bool CheckEnv(string envName)
    {
        string envVar = Envsey.CleanFlag(envName)!;
        return !string.IsNullOrEmpty(envVar) && bool.Parse(envVar);
    }

    public static void ReadConf()
    {
        IPC.Client MarseyConfPipeClient = new();
        string config = MarseyConfPipeClient.ConnRecv("MarseyConf");

        Dictionary<string, string> envVars = new();
        foreach (string seg in config.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = seg.Split('=', 2);
            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]))
                continue;

            envVars[parts[0]] = PercentDecode(parts[1]);
        }

        // Apply the environment variables to MarseyConf
        foreach (KeyValuePair<string, string> kv in envVars)
        {
            if (!MarseyConf.EnvVarMap.TryGetValue(kv.Key, value: out Action<string>? value)) continue;

            MarseyLogger.Log(MarseyLogger.LogType.DEBG, $"{kv.Key} read {kv.Value}");
            value(kv.Value);
        }
    }

    private static string PercentDecode(string s)
    {
        try
        {
            return Uri.UnescapeDataString(s);
        }
        catch
        {
            return s;
        }
    }
}
