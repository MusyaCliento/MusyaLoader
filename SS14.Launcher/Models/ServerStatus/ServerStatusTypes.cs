using System;
using System.Collections.Generic;
using SS14.Launcher.Api;

namespace SS14.Launcher.Models.ServerStatus;

public enum RefreshListStatus
{
    NotUpdated,
    UpdatingMaster,
    Updated,
    PartialError,
    Error,
}

public sealed record HubServerListEntry(string Address, string HubAddress, ServerApi.ServerStatus StatusData);

public class ServerStatusDataWithFallbackName
{
    public readonly ServerStatusData Data;
    public readonly string? FallbackName;

    public ServerStatusDataWithFallbackName(ServerStatusData data, string? name)
    {
        Data = data;
        FallbackName = name;
    }
}
