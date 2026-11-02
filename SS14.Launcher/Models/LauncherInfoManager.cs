using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models;

/// <summary>
/// Fetches and caches information from <see cref="ConfigConstants.UrlLauncherInfo"/>.
/// </summary>
public sealed class LauncherInfoManager(HttpClient httpClient)
{
    private readonly Random _messageRandom = new();
    private string[]? _messages;

    private LauncherInfoModel? _model;

    public LauncherInfoModel? Model
    {
        get
        {
            if (!LoadTask.IsCompleted)
                throw new InvalidOperationException("Data has not been loaded yet");

            return _model;
        }
    }

    public Task LoadTask { get; private set; } = default!;

    public void Initialize()
    {
        LoadTask = LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            Log.Debug("Loading launcher info... {Url}", ConfigConstants.UrlLauncherInfo);
            LauncherInfoModel? info = await ConfigConstants.UrlLauncherInfo.GetFromJsonAsync<LauncherInfoModel>(httpClient);
            if (info == null)
                Log.Warning("Launcher info response was null.");
            else
                _model = info;
        }
        catch (Exception e)
        {
            if (IsProxyError(e))
                Log.Warning("Loading launcher info failed due to proxy connectivity/authentication issue.");
            else
                Log.Warning(e, "Loading launcher info failed");
        }

        // Loading messages are local-only: they are shipped as an embedded resource
        // instead of being pulled from the remote launcher info feed.
        _messages = LoadLocalMessages();
    }

    private static string[] LoadLocalMessages()
    {
        const string resourceName = "SS14.Launcher.Models.loading-messages.txt";
        using Stream? stream = typeof(LauncherInfoManager).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Log.Warning("Local loading messages resource missing ({Resource}).", resourceName);
            return [];
        }

        using var reader = new StreamReader(stream);
        List<string> messages = [];
        while (reader.ReadLine() is { } line)
        {
            string message = line.Trim();
            if (message.Length > 0)
                messages.Add(message);
        }

        return messages.Count > 0 ? [.. messages] : [];
    }

    public string? GetRandomMessage()
    {
        if (_messages == null)
            return null;

        return _messages[_messageRandom.Next(_messages.Length)];
    }

    private static bool IsProxyError(Exception exception)
    {
        foreach (var ex in Flatten(exception))
        {
            var msg = ex.Message ?? string.Empty;
            if (msg.Contains("SOCKS", StringComparison.OrdinalIgnoreCase))
                return true;
            if (msg.Contains("proxy tunnel", StringComparison.OrdinalIgnoreCase))
                return true;
            if (msg.Contains("127.0.0.1:1080", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static System.Collections.Generic.IEnumerable<Exception> Flatten(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            yield return current;
            current = current.InnerException!;
        }
    }

    public sealed record LauncherInfoModel(
        Dictionary<string, string[]> Messages,
        string[] AllowedVersions,
        Dictionary<string, string?> OverrideAssets
    );
}
