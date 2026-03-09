using System;
using System.Collections.Generic;
using System.Reflection;
using Marsey.Config;

namespace Marsey.Game.Misc;

/// <summary>
/// Manages MarseyEntry
/// </summary>
public static class Doorbreak
{
    /// <summary>
    /// Invokes MarseyEntry
    /// </summary>
    /// <param name="entry">MethodInfo of MarseyEntry::Entry()</param>
    /// <param name="threading">Call in another thread</param>
    public static void Enter(MethodInfo? entry, bool threading = true)
    {
        if (entry == null) return;

        if (!TryBuildEntryArgs(entry, out var args))
            return;

        if (threading)
            new Thread(() => { entry.Invoke(null, args); }).Start();
        else
            entry.Invoke(null, args);
    }

    private static bool TryBuildEntryArgs(MethodInfo entry, out object?[] args)
    {
        args = [];
        var parameters = entry.GetParameters();
        if (parameters.Length == 0)
            return true;

        var assemblies = BuildAssembliesMap();
        var assembliesType = assemblies.GetType();

        if (parameters.Length == 1)
        {
            var p0 = parameters[0].ParameterType;
            if (p0.IsAssignableFrom(assembliesType))
            {
                args = [assemblies];
                return true;
            }

            if (p0 == typeof(string))
            {
                args = [MarseyVars.MarseyFolder];
                return true;
            }
        }

        if (parameters.Length == 2)
        {
            var p0 = parameters[0].ParameterType;
            var p1 = parameters[1].ParameterType;
            if (p0.IsAssignableFrom(assembliesType) && p1 == typeof(string))
            {
                args = [assemblies, MarseyVars.MarseyFolder];
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, Assembly> BuildAssembliesMap()
    {
        var map = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        if (GameAssemblies.RobustClient != null)
            map["Robust.Client"] = GameAssemblies.RobustClient;
        if (GameAssemblies.RobustShared != null)
            map["Robust.Shared"] = GameAssemblies.RobustShared;
        if (GameAssemblies.ContentClient != null)
            map["Content.Client"] = GameAssemblies.ContentClient;
        if (GameAssemblies.ContentShared != null)
            map["Content.Shared"] = GameAssemblies.ContentShared;

        return map;
    }
}
