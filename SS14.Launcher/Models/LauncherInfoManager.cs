using System;
using System.Collections.Generic;
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
        LauncherInfoModel? info;
        try
        {
            Log.Debug("Loading launcher info... {Url}", ConfigConstants.UrlLauncherInfo);
            info = await ConfigConstants.UrlLauncherInfo.GetFromJsonAsync<LauncherInfoModel>(httpClient);
            if (info == null)
            {
                Log.Warning("Launcher info response was null.");
                return;
            }

             string[] messagesEn = new string[]
            {
            "Meow! Siameses cat rule the station!",
            "Copium delivery incoming...",
            "Robust or be robusted!",
            "Pet the catl for good luck!",
            "Insert more catnip!",
            "Skill issue detected!",
            "The toolbox chooses the engineer.",
            "Cargonia will rise again!",
            "Don't forget to feed the corgi.",
            "Assistant spotted with a crowbar.",
            "Syndie? More like skill issue.",
            "Clown stole my shoes again...",
            "Atmos techs know true power.",
            "Mothroaches are plotting something.",
            "Copium levels critical!",
            "Siameses are best cats, fact.",
            "Just another shift gone wrong...",
            "Honk!",
            "The AI is definitely not rogue.",
            "You hear scratching in the vents...",
            "Siameses bring only chaos.",
            "Space carp are overrated.",
            "Toolbox meta never dies.",
            "Meow meow robust crew!",
            "Trust no mime.",
            "More cats, less plasma fires.",
            "Shuttle called due to cat nap time.",
            "Warden's gun cabinet is empty...",
            "Engineer forgot to turn on the SM.",
            "Vents are for cats, not you.",
            "Crew morale up thanks to cats!",
            "Copium tank refilled successfully.",
            "Robustness is a lifestyle."
            };
        }
        catch (Exception e)
        {
            if (IsProxyError(e))
                Log.Warning("Loading launcher info failed due to proxy connectivity/authentication issue.");
            else
                Log.Warning(e, "Loading launcher info failed");
            return;
        }

        // This is future-proofed to support multiple languages,
        // but for now the launcher only supports English so it'll have to do.
        info.Messages.TryGetValue("en-US", out _messages);

        _model = info;
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
