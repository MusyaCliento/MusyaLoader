using System;
using System.Net;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Utility;

public sealed class DynamicSocks5WebProxy : IWebProxy
{
    private readonly DataManager _cfg;
    private readonly DynamicSocksCredentials _dynamicCredentials;

    public DynamicSocks5WebProxy(DataManager cfg)
    {
        _cfg = cfg;
        _dynamicCredentials = new DynamicSocksCredentials(cfg);
    }

    public ICredentials? Credentials
    {
        get => _dynamicCredentials;
        set
        {
            // Intentionally ignored: credentials are sourced from launcher config.
        }
    }

    public Uri GetProxy(Uri destination)
    {
        if (LauncherProxyRuntimeState.DisableLauncherProxyForSession)
            return destination;

        if (!_cfg.GetCVar(CVars.LauncherProxyEnabled))
            return destination;

        if (!Socks5ProxyHelper.TryReadProxyValues(_cfg, out var proxy, out _))
            return destination;

        var builder = new UriBuilder("socks5", proxy.Host, proxy.Port);
        if (!string.IsNullOrWhiteSpace(proxy.Username))
        {
            builder.UserName = proxy.Username;
            builder.Password = proxy.Password ?? "";
        }

        return builder.Uri;
    }

    public bool IsBypassed(Uri host)
    {
        if (LauncherProxyRuntimeState.DisableLauncherProxyForSession)
            return true;

        if (!_cfg.GetCVar(CVars.LauncherProxyEnabled))
            return true;

        return !Socks5ProxyHelper.TryReadProxyValues(_cfg, out _, out _);
    }

    private sealed class DynamicSocksCredentials : ICredentials
    {
        private readonly DataManager _cfg;

        public DynamicSocksCredentials(DataManager cfg)
        {
            _cfg = cfg;
        }

        public NetworkCredential? GetCredential(Uri uri, string authType)
        {
            if (LauncherProxyRuntimeState.DisableLauncherProxyForSession)
                return null;

            if (!_cfg.GetCVar(CVars.LauncherProxyEnabled))
                return null;

            if (!Socks5ProxyHelper.TryReadProxyValues(_cfg, out var proxy, out _))
                return null;

            if (string.IsNullOrWhiteSpace(proxy.Username))
                return null;

            return new NetworkCredential(proxy.Username, proxy.Password ?? "");
        }
    }
}
