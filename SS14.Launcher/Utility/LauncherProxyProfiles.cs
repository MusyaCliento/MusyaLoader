using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Utility;

public sealed record LauncherProxyProfile(
    string Id,
    string Name,
    string Host,
    int Port,
    string Username,
    string Password);

public static class LauncherProxyProfiles
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<LauncherProxyProfile> Load(DataManager cfg)
    {
        MigrateLegacyIfNeeded(cfg);

        var raw = cfg.GetCVar(CVars.LauncherProxyProfilesJson) ?? "";
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<LauncherProxyProfile>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<LauncherProxyProfile>>(raw, JsonOptions) ?? new List<LauncherProxyProfile>();
            return parsed
                .Where(p => !string.IsNullOrWhiteSpace(p.Id) &&
                            !string.IsNullOrWhiteSpace(p.Name) &&
                            !string.IsNullOrWhiteSpace(p.Host) &&
                            p.Port is >= 1 and <= 65535)
                .ToList();
        }
        catch
        {
            return Array.Empty<LauncherProxyProfile>();
        }
    }

    public static void Save(DataManager cfg, IReadOnlyList<LauncherProxyProfile> profiles, string? selectedId)
    {
        var normalized = profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.Id) &&
                        !string.IsNullOrWhiteSpace(p.Name) &&
                        !string.IsNullOrWhiteSpace(p.Host) &&
                        p.Port is >= 1 and <= 65535)
            .Select(p => p with
            {
                Id = p.Id.Trim(),
                Name = p.Name.Trim(),
                Host = p.Host.Trim(),
                Username = p.Username ?? "",
                Password = p.Password ?? ""
            })
            .ToList();

        cfg.SetCVar(CVars.LauncherProxyProfilesJson, JsonSerializer.Serialize(normalized, JsonOptions));
        cfg.SetCVar(CVars.LauncherProxySelectedProfileId, selectedId ?? "");
        cfg.CommitConfig();
    }

    public static LauncherProxyProfile? GetSelected(DataManager cfg)
    {
        var profiles = Load(cfg);
        if (profiles.Count == 0)
            return null;

        var selectedId = cfg.GetCVar(CVars.LauncherProxySelectedProfileId) ?? "";
        var selected = profiles.FirstOrDefault(p => string.Equals(p.Id, selectedId, StringComparison.Ordinal));
        return selected ?? profiles[0];
    }

    public static string EnsureUniqueName(IReadOnlyList<LauncherProxyProfile> profiles, string baseName)
    {
        var name = string.IsNullOrWhiteSpace(baseName) ? "Proxy" : baseName.Trim();
        if (profiles.All(p => !string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return name;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{name} {i}";
            if (profiles.All(p => !string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return $"{name} {Guid.NewGuid():N}".Substring(0, 14);
    }

    private static void MigrateLegacyIfNeeded(DataManager cfg)
    {
        var profilesRaw = cfg.GetCVar(CVars.LauncherProxyProfilesJson) ?? "";
        if (!string.IsNullOrWhiteSpace(profilesRaw))
            return;

        var host = (cfg.GetCVar(CVars.LauncherProxyHost) ?? "").Trim();
        var port = cfg.GetCVar(CVars.LauncherProxyPort);
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            return;

        var profile = new LauncherProxyProfile(
            Guid.NewGuid().ToString("N"),
            "Default SOCKS5",
            host,
            port,
            cfg.GetCVar(CVars.LauncherProxyUsername) ?? "",
            cfg.GetCVar(CVars.LauncherProxyPassword) ?? "");

        Save(cfg, new[] { profile }, profile.Id);
    }
}
