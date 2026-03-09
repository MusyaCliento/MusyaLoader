using System;
using System.Net;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Utility;

public sealed record Socks5ProxyConfig(
    string Host,
    int Port,
    string Username,
    string Password)
{
    public string ToProxyUriString(bool includeCredentials)
    {
        var builder = new UriBuilder("socks5", Host, Port);
        if (includeCredentials && !string.IsNullOrWhiteSpace(Username))
        {
            builder.UserName = Username;
            builder.Password = Password ?? "";
        }

        return builder.Uri.AbsoluteUri;
    }

    public IWebProxy ToWebProxy()
    {
        var builder = new UriBuilder("socks5", Host, Port);
        var proxy = new WebProxy(builder.Uri);
        if (!string.IsNullOrWhiteSpace(Username))
            proxy.Credentials = new NetworkCredential(Username, Password ?? "");

        return proxy;
    }
}

public static class Socks5ProxyHelper
{
    public static bool TryReadProxyValues(DataManager cfg, out Socks5ProxyConfig config, out string error)
    {
        config = null!;
        error = "";

        var selected = LauncherProxyProfiles.GetSelected(cfg);
        if (selected != null)
        {
            config = new Socks5ProxyConfig(selected.Host, selected.Port, selected.Username, selected.Password);
            return true;
        }

        var host = (cfg.GetCVar(CVars.LauncherProxyHost) ?? "").Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Proxy host is empty.";
            return false;
        }

        var port = cfg.GetCVar(CVars.LauncherProxyPort);
        if (port is < 1 or > 65535)
        {
            error = $"Proxy port is out of range: {port}.";
            return false;
        }

        config = new Socks5ProxyConfig(
            host,
            port,
            cfg.GetCVar(CVars.LauncherProxyUsername) ?? "",
            cfg.GetCVar(CVars.LauncherProxyPassword) ?? "");
        return true;
    }

    public static bool TryReadFromConfig(DataManager cfg, out Socks5ProxyConfig config, out string error)
    {
        if (!cfg.GetCVar(CVars.LauncherProxyEnabled))
        {
            config = null!;
            error = "";
            return false;
        }

        return TryReadProxyValues(cfg, out config, out error);
    }
}
