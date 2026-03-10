using Marsey.Config;
using System;
using System.Text.RegularExpressions;

namespace Marsey.Stealthsey;

public static class Abjure
{
    private static Version? engineVer { get; set; } 
    
    public static bool CheckMalbox(string engineversion, HideLevel MarseyHide)
    {
        if (!TryParseEngineVersion(engineversion, out var parsed))
        {
            parsed = new Version(0, 0);
        }

        engineVer = parsed;

        return engineVer >= MarseyVars.Detection && MarseyHide == HideLevel.Disabled;
    }

    private static bool TryParseEngineVersion(string engineVersion, out Version? parsed)
    {
        if (Version.TryParse(engineVersion, out var direct))
        {
            parsed = direct;
            return true;
        }

        var match = Regex.Match(engineVersion, @"\d+(\.\d+)+");

        if (match.Success && Version.TryParse(match.Value, out var extracted))
        {
            parsed = extracted;
            return true;
        }

        parsed = null;
        return false;
    }
}