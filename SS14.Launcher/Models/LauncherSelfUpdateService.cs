using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

public sealed record LauncherSelfUpdateInfo(
    string VersionText,
    Version Version,
    string DownloadUrl,
    string ReleasePageUrl,
    string AssetName,
    bool InstallSupported,
    string ReleaseNotes,
    bool IsPreRelease,
    string ReleaseTag,
    DateTimeOffset PublishedAt);

public sealed class LauncherSelfUpdateService
{
    private readonly DataManager _cfg;

    public LauncherSelfUpdateService(DataManager cfg)
    {
        _cfg = cfg;
    }

    public async Task<LauncherSelfUpdateInfo?> CheckAsync(string repoInput, bool includePreRelease, CancellationToken cancel = default)
    {
        var releases = await GetAvailableAsync(repoInput, cancel);
        if (releases.Count == 0)
            return null;

        var currentVersion = LauncherVersion.Version ?? new Version(0, 0, 0, 0);
        var filtered = releases
            .Where(r => includePreRelease || !r.IsPreRelease)
            .Where(r => r.Version > currentVersion)
            .OrderByDescending(r => r.Version)
            .ThenBy(r => r.IsPreRelease)
            .ThenByDescending(r => r.PublishedAt)
            .ToList();

        if (filtered.Count == 0)
        {
            Log.Debug("Self-update: current version {CurrentVersion} is up-to-date", currentVersion);
            return null;
        }

        return filtered[0];
    }

    public async Task<IReadOnlyList<LauncherSelfUpdateInfo>> GetAvailableAsync(string repoInput, CancellationToken cancel = default)
    {
        if (!TryParseRepo(repoInput, out var owner, out var repo))
        {
            Log.Debug("Self-update: invalid repo input: {RepoInput}", repoInput);
            return Array.Empty<LauncherSelfUpdateInfo>();
        }

        var endpoint = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=100";
        using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
        req.Headers.UserAgent.ParseAdd("MusyaLoader-Updater");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var client = CreateHttpClientForUpdateChecks();
        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancel);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            Log.Debug("Self-update: no releases for {Owner}/{Repo}", owner, repo);
            return Array.Empty<LauncherSelfUpdateInfo>();
        }

        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cancel);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<LauncherSelfUpdateInfo>();

        var list = new List<LauncherSelfUpdateInfo>();
        foreach (var root in doc.RootElement.EnumerateArray())
        {
            if (!TryBuildReleaseInfo(root, owner, repo, out var info))
                continue;

            list.Add(info);
        }

        return list
            .OrderByDescending(r => r.Version)
            .ThenBy(r => r.IsPreRelease)
            .ThenByDescending(r => r.PublishedAt)
            .ToList();
    }

    private static bool TryBuildReleaseInfo(JsonElement root, string owner, string repo, out LauncherSelfUpdateInfo info)
    {
        info = default!;

        if (!root.TryGetProperty("tag_name", out var tagProp))
            return false;

        var tagName = (tagProp.GetString() ?? "").Trim();
        var isStable = string.Equals(tagName, "Release", StringComparison.OrdinalIgnoreCase);
        var isPre = string.Equals(tagName, "Pre-Release", StringComparison.OrdinalIgnoreCase);
        if (!isStable && !isPre)
            return false;

        string? releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        if (!TryParseVersionFromTitle(releaseName, out var versionText, out var parsedVersion))
        {
            Log.Debug("Self-update: could not parse release version from title '{Name}'", releaseName);
            return false;
        }

        var rawNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
        var formattedNotes = FormatMarkdownToText(rawNotes);

        var releasePage = root.TryGetProperty("html_url", out var htmlUrlProp)
            ? htmlUrlProp.GetString() ?? $"https://github.com/{owner}/{repo}/releases"
            : $"https://github.com/{owner}/{repo}/releases";

        var publishedAt = DateTimeOffset.MinValue;
        if (root.TryGetProperty("published_at", out var pubProp))
        {
            _ = DateTimeOffset.TryParse(pubProp.GetString(), out publishedAt);
        }

        if (OperatingSystem.IsMacOS())
        {
            info = new LauncherSelfUpdateInfo(
                versionText,
                parsedVersion,
                releasePage,
                releasePage,
                "",
                false,
                formattedNotes,
                isPre,
                tagName,
                publishedAt);
            return true;
        }

        string assetUrl = releasePage;
        string assetName = "";
        var installSupported = false;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            var wantedOs = OperatingSystem.IsWindows() ? "windows" : "linux";
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var assetNameProp) ? assetNameProp.GetString() ?? "" : "";
                var url = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                    continue;

                var lower = name.ToLowerInvariant();
                if (!lower.EndsWith(".zip", StringComparison.Ordinal))
                    continue;

                if (!AssetMatchesCurrentOs(lower, wantedOs))
                    continue;

                assetUrl = url;
                assetName = name;
                installSupported = true;
                break;
            }
        }

        info = new LauncherSelfUpdateInfo(
            versionText,
            parsedVersion,
            assetUrl,
            releasePage,
            assetName,
            installSupported,
            formattedNotes,
            isPre,
            tagName,
            publishedAt);
        return true;
    }

    private static bool TryParseVersionFromTitle(string? releaseName, out string normalizedText, out Version version)
    {
        static string? ExtractVersionText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var trimmed = input.Trim().TrimStart('v', 'V');
            if (Version.TryParse(trimmed, out _))
                return trimmed;

            var m = Regex.Match(input, @"(?<!\d)(\d+\.\d+\.\d+(?:\.\d+)?)(?!\d)");
            return m.Success ? m.Groups[1].Value : null;
        }

        var fromName = ExtractVersionText(releaseName ?? "");
        if (fromName != null && Version.TryParse(fromName, out var parsedNameVersion) && parsedNameVersion != null)
        {
            version = parsedNameVersion;
            normalizedText = fromName;
            return true;
        }

        normalizedText = "";
        version = new Version(0, 0, 0, 0);
        return false;
    }

    private static string FormatMarkdownToText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return "";

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();

            if (IsMarkdownTableHeader(line) && i + 1 < lines.Length && IsMarkdownTableSeparator(lines[i + 1]))
            {
                var headers = SplitMarkdownTableRow(line);
                i += 2; // skip header and separator

                for (; i < lines.Length; i++)
                {
                    var rowLine = lines[i].Trim();
                    if (!IsMarkdownTableRow(rowLine))
                    {
                        i--;
                        break;
                    }

                    var cells = SplitMarkdownTableRow(rowLine);
                    if (cells.Count == 0)
                        continue;

                    var parts = new List<string>();
                    for (var c = 0; c < Math.Min(headers.Count, cells.Count); c++)
                    {
                        if (string.IsNullOrWhiteSpace(cells[c]))
                            continue;

                        parts.Add($"{headers[c]}: {cells[c]}");
                    }

                    if (parts.Count > 0)
                        result.Add("• " + string.Join(" | ", parts));
                }

                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
                line = line[4..].Trim() + ":";
            else if (line.StartsWith("## ", StringComparison.Ordinal))
                line = line[3..].Trim() + ":";
            else if (line.StartsWith("# ", StringComparison.Ordinal))
                line = line[2..].Trim() + ":";
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                line = "• " + line[2..].Trim();

            line = line.Replace("**", "").Replace("__", "").Replace("`", "");
            result.Add(line);
        }

        return string.Join('\n', result).Trim();
    }

    private static bool IsMarkdownTableHeader(string line)
    {
        return IsMarkdownTableRow(line);
    }

    private static bool IsMarkdownTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('|', StringComparison.Ordinal))
            return false;

        var cells = SplitMarkdownTableRow(trimmed);
        if (cells.Count == 0)
            return false;

        return cells.All(c =>
        {
            var raw = c.Trim();
            return raw.Length > 0 && raw.All(ch => ch is '-' or ':' or ' ');
        });
    }

    private static bool IsMarkdownTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
            return false;

        return trimmed.Contains('|', StringComparison.Ordinal) &&
               (trimmed.StartsWith('|') || trimmed.EndsWith('|'));
    }

    private static List<string> SplitMarkdownTableRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed
            .Split('|')
            .Select(v => v.Trim())
            .ToList();
    }

    private static bool AssetMatchesCurrentOs(string assetNameLower, string wantedOs)
    {
        if (wantedOs == "windows")
            return assetNameLower.Contains("windows", StringComparison.Ordinal) || assetNameLower.Contains("win", StringComparison.Ordinal);

        return assetNameLower.Contains("linux", StringComparison.Ordinal) || assetNameLower.Contains("lin", StringComparison.Ordinal);
    }

    private static bool TryParseRepo(string input, out string owner, out string repo)
    {
        owner = "";
        repo = "";
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalized = input.Trim();
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "https://" + normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = uri.AbsolutePath.Trim('/');
        var match = Regex.Match(path, @"^(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$");
        if (!match.Success)
            return false;

        owner = match.Groups["owner"].Value;
        repo = match.Groups["repo"].Value;
        return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
    }

    private HttpClient CreateHttpClientForUpdateChecks()
    {
        if (LauncherProxyRuntimeState.DisableUpdateProxyForSession)
            return HappyEyeballsHttp.CreateHttpClient();

        if (_cfg.GetCVar(CVars.LauncherProxyUpdatesEnabled) &&
            Socks5ProxyHelper.TryReadProxyValues(_cfg, out var proxyCfg, out _))
        {
            if (IsProxyReachable(proxyCfg.Host, proxyCfg.Port, TimeSpan.FromMilliseconds(1200), out var error))
                return HappyEyeballsHttp.CreateHttpClient(proxy: proxyCfg.ToWebProxy());

            Log.Warning(
                "Update proxy is enabled but unreachable ({Host}:{Port}): {Error}. Falling back to direct update checks.",
                proxyCfg.Host,
                proxyCfg.Port,
                error);
        }

        return HappyEyeballsHttp.CreateHttpClient();
    }

    private static bool IsProxyReachable(string host, int port, TimeSpan timeout, out string error)
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(timeout);
            tcp.ConnectAsync(host, port, cts.Token).GetAwaiter().GetResult();
            error = "";
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
