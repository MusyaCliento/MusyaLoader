using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Marsey.PatchAssembly;
using Marsey.Patches;
using Marsey.Misc;
using Marsey.Stealthsey;
using Marsey.Stealthsey.Reflection;

namespace Marsey.Subversion;

/// <summary>
/// Manages patches/addons based on the Subverter patch
/// </summary>
public static class Subverter
{
    public static List<SubverterPatch> GetSubverterPatches() => PatchListManager.GetPatchList<SubverterPatch>();
}

public class SubverterPatch : IPatch
{
    private const string DefaultIcon = "avares://SS14.Launcher/Assets/marsey-icons/subverterpatches.png";

    public string Asmpath { get; set; }
    public Assembly Asm { get; set; }
    public string Name { get; set; }
    public string Desc { get; set; }
    public MethodInfo? Entry { get; set; }
    public bool Enabled { get; set; }
    public string IconPath { get; }

    public SubverterPatch(
        string asmpath,
        Assembly asm,
        string name,
        string desc,
        string? iconResource = null)
    {
        Asmpath = asmpath;
        Name = name;
        Desc = desc;
        Asm = asm;
        IconPath = ResolveIconPath(asm, iconResource);
    }

    private static string ResolveIconPath(Assembly asm, string? iconResource)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(iconResource))
            {
                string? extracted = TryExtractEmbeddedIcon(asm, iconResource);
                if (!string.IsNullOrWhiteSpace(extracted))
                    return extracted;
            }
        }
        catch
        {
            // Keep fallback icon if path resolution fails.
        }

        return DefaultIcon;
    }

    private static string? TryExtractEmbeddedIcon(Assembly asm, string iconResource)
    {
        string? resourceName = asm
            .GetManifestResourceNames()
            .FirstOrDefault(n => string.Equals(n, iconResource, StringComparison.Ordinal))
            ?? asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(iconResource, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            return null;

        using Stream? stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;

        string iconDir = Path.Combine(Path.GetTempPath(), "MarseyPatchIcons");
        Directory.CreateDirectory(iconDir);

        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{asm.FullName}|{resourceName}"));
        string iconPath = Path.Combine(iconDir, $"{Convert.ToHexString(hashBytes)[..16]}.png");

        if (!File.Exists(iconPath))
        {
            using var fs = File.Create(iconPath);
            stream.CopyTo(fs);
        }

        return iconPath;
    }

    public override bool Equals(object? obj)
    {
        if (obj is SubverterPatch other)
        {
            return this.Name == other.Name && this.Desc == other.Desc;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return System.HashCode.Combine(Name, Desc);
    }
}
