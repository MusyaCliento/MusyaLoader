using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models.EngineManager;

public sealed partial class EngineManagerDynamic
{
    // This part of the code is responsible for downloading and caching the Robust build manifest.
    private static readonly TimeSpan ManifestLoadTimeout = TimeSpan.FromSeconds(25);

    private readonly SemaphoreSlim _manifestSemaphore = new(1);
    private readonly Stopwatch _manifestStopwatch = Stopwatch.StartNew();

    private Dictionary<string, VersionInfo>? _cachedRobustVersionInfo;
    private TimeSpan _robustCacheValidUntil;
    private EntityTagHeaderValue? _cachedManifestEtag;
    private DateTimeOffset? _cachedManifestLastModified;

    /// <summary>
    /// Look up information about an engine version.
    /// </summary>
    /// <param name="version">The version number to look up.</param>
    /// <param name="followRedirects">Follow redirections in version info.</param>
    /// <param name="cancel">Cancellation token.</param>
    /// <returns>
    /// Information about the version, or null if it could not be found.
    /// The returned version may be different than what was requested if redirects were followed.
    /// </returns>
    private async ValueTask<FoundVersionInfo?> GetVersionInfo(
        string version,
        bool followRedirects = true,
        CancellationToken cancel = default)
    {
        await _manifestSemaphore.WaitAsync(cancel);
        try
        {
            return await GetVersionInfoCore(version, followRedirects, cancel);
        }
        finally
        {
            _manifestSemaphore.Release();
        }
    }

    private async ValueTask<FoundVersionInfo?> GetVersionInfoCore(
        string version,
        bool followRedirects,
        CancellationToken cancel)
    {
        // If we have a cached copy, and it's not expired, we check it.
        if (_cachedRobustVersionInfo != null && _robustCacheValidUntil > _manifestStopwatch.Elapsed)
        {
            // Check the version. If this fails, we immediately re-request the manifest as it may have changed.
            // (Connecting to a freshly-updated server with a new Robust version, within the cache window.)
            if (FindVersionInfoInCached(version, followRedirects) is { } foundVersionInfo)
                return foundVersionInfo;
        }

        await UpdateBuildManifest(cancel);

        return FindVersionInfoInCached(version, followRedirects);
    }

    private async Task UpdateBuildManifest(CancellationToken cancel)
    {
        Log.Debug("Loading manifest from {manifestUrl}...", ConfigConstants.RobustBuildsManifest);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeoutCts.CancelAfter(ManifestLoadTimeout);

        try
        {
            using var response = await ConfigConstants.RobustBuildsManifest.SendAsync(
                _http,
                url =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    if (_cachedManifestEtag != null)
                        req.Headers.IfNoneMatch.Add(_cachedManifestEtag);
                    if (_cachedManifestLastModified != null)
                        req.Headers.IfModifiedSince = _cachedManifestLastModified;
                    return req;
                },
                timeoutCts.Token);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && _cachedRobustVersionInfo != null)
            {
                Log.Debug("Manifest not modified, using cached copy");
                _robustCacheValidUntil = _manifestStopwatch.Elapsed + GetManifestCacheDuration(response);
                return;
            }

            response.EnsureSuccessStatusCode();

            _cachedManifestEtag = response.Headers.ETag;
            _cachedManifestLastModified = response.Content.Headers.LastModified;

            _cachedRobustVersionInfo =
                await response.Content.ReadFromJsonAsync<Dictionary<string, VersionInfo>>(timeoutCts.Token)
                ?? throw new InvalidOperationException("Robust manifest response was empty.");

            _robustCacheValidUntil = _manifestStopwatch.Elapsed + GetManifestCacheDuration(response);
        }
        catch (OperationCanceledException e) when (!cancel.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Timed out after {ManifestLoadTimeout.TotalSeconds:0}s while loading Robust build manifest. " +
                "Check your proxy/tunnel stability or disable proxy for launcher downloads.",
                e);
        }
    }

    private static TimeSpan GetManifestCacheDuration(HttpResponseMessage? response)
    {
        if (response?.Headers.CacheControl?.NoStore == true || response?.Headers.CacheControl?.NoCache == true)
            return TimeSpan.Zero;

        var maxAge = response?.Headers.CacheControl?.MaxAge;
        if (maxAge.HasValue)
            return maxAge.Value;

        if (response?.Content.Headers.Expires is { } expires)
        {
            var delta = expires - DateTimeOffset.UtcNow;
            if (delta > TimeSpan.Zero)
                return delta;
        }

        return ConfigConstants.RobustManifestCacheTime;
    }

    private FoundVersionInfo? FindVersionInfoInCached(string version, bool followRedirects)
    {
        Debug.Assert(_cachedRobustVersionInfo != null);

        if (!_cachedRobustVersionInfo.TryGetValue(version, out var versionInfo))
            return null;

        if (followRedirects)
        {
            while (versionInfo.RedirectVersion != null)
            {
                version = versionInfo.RedirectVersion;
                versionInfo = _cachedRobustVersionInfo[versionInfo.RedirectVersion];
            }
        }

        return new FoundVersionInfo(version, versionInfo);
    }

    private sealed record FoundVersionInfo(string Version, VersionInfo Info);

    private sealed record VersionInfo(
        bool Insecure,
        [property: JsonPropertyName("redirect")]
        string? RedirectVersion,
        Dictionary<string, BuildInfo> Platforms);

    private sealed class BuildInfo
    {
        [JsonInclude] [JsonPropertyName("url")]
        public string Url = default!;

        [JsonInclude] [JsonPropertyName("sha256")]
        public string Sha256 = default!;

        [JsonInclude] [JsonPropertyName("sig")]
        public string Signature = default!;
    }
}
