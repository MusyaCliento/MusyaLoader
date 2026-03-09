using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json; 

namespace Marsey.Patches;

/// <summary>
/// This class contains the data about a patch (called a Marsey), that is later used the loader to alter the game's functionality.
/// </summary>
public class MarseyPatch : IPatch
{
    private const string DefaultIcon = "avares://SS14.Launcher/Assets/marsey-icons/marseypatches.png";

    public string Asmpath { get; set; }
    public Assembly Asm { get; set; }
    public string Name { get; set; }
    public string Desc { get; set; }
    public MethodInfo? Entry { get; set; }
    public bool Preload { get; set; } = false;
    public bool Enabled { get; set; }
    public string IconPath { get; }

    [JsonIgnore]
    public string Version => Asm?.GetName()?.Version?.ToString() ?? "n/a";

    public MarseyPatch(
        string asmpath,
        Assembly asm,
        string name,
        string desc,
        bool preload = false,
        string? iconResource = null)
    {
        this.Asmpath = asmpath;
        this.Asm = asm;
        this.Name = name;
        this.Desc = desc;
        this.Preload = preload;
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
        if (obj is MarseyPatch other)
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
