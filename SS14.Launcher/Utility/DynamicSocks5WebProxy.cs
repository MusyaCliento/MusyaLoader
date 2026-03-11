using System;
using System.Net;
using System.Net.Sockets;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Utility;

public sealed class DynamicSocks5WebProxy : IWebProxy
{
    private readonly DataManager _cfg;
    private readonly DynamicSocksCredentials _dynamicCredentials;
    private readonly Func<bool> _isEnabled;
    private readonly Func<bool> _isSessionDisabled;

    public DynamicSocks5WebProxy(DataManager cfg)
        : this(cfg,
            () => cfg.GetCVar(CVars.LauncherProxyEnabled),
            () => LauncherProxyRuntimeState.DisableLauncherProxyForSession)
    {
    }

    public DynamicSocks5WebProxy(DataManager cfg, Func<bool> isEnabled, Func<bool> isSessionDisabled)
    {
        _cfg = cfg;
        _isEnabled = isEnabled ?? (() => true);
        _isSessionDisabled = isSessionDisabled ?? (() => false);
        _dynamicCredentials = new DynamicSocksCredentials(cfg, _isEnabled, _isSessionDisabled);
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
        if (IsLocalDestination(destination))
            return destination;

        if (_isSessionDisabled())
            return destination;

        if (!_isEnabled())
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
        if (IsLocalDestination(host))
            return true;

        if (_isSessionDisabled())
            return true;

        if (!_isEnabled())
            return true;

        return !Socks5ProxyHelper.TryReadProxyValues(_cfg, out _, out _);
    }

    private static bool IsLocalDestination(Uri destination)
    {
        if (destination.IsLoopback)
            return true;

        var host = destination.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                return true;
            return false;
        }

        var bytes = ip.GetAddressBytes();
        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;
        // 127.0.0.0/8
        if (bytes[0] == 127)
            return true;
        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;
        // 172.16.0.0 - 172.31.255.255
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        return false;
    }

    private sealed class DynamicSocksCredentials : ICredentials
    {
        private readonly DataManager _cfg;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isSessionDisabled;

        public DynamicSocksCredentials(DataManager cfg, Func<bool> isEnabled, Func<bool> isSessionDisabled)
        {
            _cfg = cfg;
            _isEnabled = isEnabled ?? (() => true);
            _isSessionDisabled = isSessionDisabled ?? (() => false);
        }

        public NetworkCredential? GetCredential(Uri uri, string authType)
        {
            if (_isSessionDisabled())
                return null;

            if (!_isEnabled())
                return null;

            if (!Socks5ProxyHelper.TryReadProxyValues(_cfg, out var proxy, out _))
                return null;

            if (string.IsNullOrWhiteSpace(proxy.Username))
                return null;

            return new NetworkCredential(proxy.Username, proxy.Password ?? "");
        }
    }
}
