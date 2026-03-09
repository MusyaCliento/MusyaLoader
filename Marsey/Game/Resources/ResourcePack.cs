using Marsey.Misc;
using Newtonsoft.Json.Linq;

namespace Marsey.Game.Resources;

/// <summary>
/// Metadata class for Resource Packs
/// </summary>
public class ResourcePack
{
    private const string DefaultIcon = "avares://SS14.Launcher/Assets/marsey-icons/resourcepacks.png";

    public string Dir { get; }
    public string? Name { get; private set; }
    public string? Desc { get; private set; }
    public string? Target { get; private set; } // Specify fork for which this is used
    public bool Enabled { get; set; } = true;
    public string IconPath { get; private set; } = DefaultIcon;

    public ResourcePack(string dir)
    {
        Dir = dir ?? throw new ArgumentNullException(nameof(dir));
    }

    public void ParseMeta()
    {
        string metaPath = Path.Combine(Dir, "meta.json");
        if (!File.Exists(metaPath))
            throw new RPackException($"Found folder {Dir}, but it didn't have a meta.json");

        string jsonData = File.ReadAllText(metaPath);

        JObject j;
        try
        {
            j = JObject.Parse(jsonData);
        }
        catch (Exception e)
        {
            throw new RPackException($"Meta.json is incorrectly formatted (invalid JSON): {e.Message}");
        }

        // Try several common key variants for robustness
        Name = GetStringIgnoreCase(j, "name", "Name", "title") ?? string.Empty;
        Desc = GetStringIgnoreCase(j, "description", "Description", "desc") ?? string.Empty;
        Target = GetStringIgnoreCase(j, "target", "Target") ?? string.Empty;
        IconPath = ResolveIconPath();

        // If the pack requires certain required fields, validate here.
        // Current original code required Name, Description and Target non-null.
        // Usually target can be empty (applies to all forks). Adjust as needed:
        if (string.IsNullOrWhiteSpace(Name))
            throw new RPackException("Meta.json is incorrectly formatted: 'name' is missing or empty.");

        // If you *require* description/target, uncomment checks below:
        // if (string.IsNullOrWhiteSpace(Desc))
        //     throw new RPackException("Meta.json is incorrectly formatted: 'description' is missing or empty.");

        // if (Target == null)
        //     throw new RPackException("Meta.json is incorrectly formatted: 'target' is missing.");
    }

    private string ResolveIconPath()
    {
        try
        {
            string iconFile = Path.Combine(Dir, "icon.png");
            if (File.Exists(iconFile))
                return iconFile;
        }
        catch
        {
            // Keep fallback icon if path resolution fails.
        }

        return DefaultIcon;
    }

    private static string? GetStringIgnoreCase(JObject j, params string[] keys)
    {
        foreach (var k in keys)
        {
            var t = j.Property(k, StringComparison.Ordinal) ?? j.Properties().FirstOrDefault(p => string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase));
            if (t != null)
            {
                var v = t.Value.Type == JTokenType.String ? t.Value.ToString() : t.Value.ToString();
                return v;
            }
        }

        return null;
    }
}
