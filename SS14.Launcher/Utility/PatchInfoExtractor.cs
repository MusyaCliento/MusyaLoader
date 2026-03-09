using System.Reflection;
using System.Linq;
using Marsey.Patches;
using Marsey.Subversion;

namespace SS14.Launcher.Utility;

public static class PatchInfoExtractor
{
    public static string GetPatchDotNetVersion(IPatch patch)
    {
        try
        {
            var asm = patch switch
            {
                MarseyPatch mp => mp.Asm,
                SubverterPatch sp => sp.Asm,
                _ => null
            };

            if (asm == null)
                return "Unknown";

            var attrs = asm.GetCustomAttributes();
            var targetFrameworkAttr = attrs.FirstOrDefault(a => 
                a.GetType().Name == "TargetFrameworkAttribute");
            
            if (targetFrameworkAttr != null)
            {
                var propInfo = targetFrameworkAttr.GetType().GetProperty("FrameworkDisplayName");
                if (propInfo != null)
                {
                    var displayName = propInfo.GetValue(targetFrameworkAttr)?.ToString();
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        var parts = displayName.Split(' ');
                        if (parts.Length >= 2)
                        {
                            return parts[parts.Length - 1];
                        }
                    }
                }
            }
        }
        catch { }

        return "Unknown";
    }

    public static bool IsCompatibleDotNetVersion(string patchVersion, string launcherVersion = "10")
    {
        try
        {
            var patchMajor = int.Parse(patchVersion.Split('.')[0]);
            var launcherMajor = int.Parse(launcherVersion.Split('.')[0]);

            return patchMajor == launcherMajor;
        }
        catch
        {
            return false;
        }
    }

    public static string GetDotNetVersionColor(string patchVersion, string launcherVersion = "10")
    {
        if (IsCompatibleDotNetVersion(patchVersion, launcherVersion))
        {
            return "#00AA00";
        }
        else
        {
            return "#FFAA00";
        }
    }
}
