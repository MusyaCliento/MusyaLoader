using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SS14.Launcher;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Utility;

public static class ProxyRouting
{
    private static readonly HashSet<string> RobustHosts = BuildHostSet(ConfigConstants.RobustBuildsManifest);
    private static readonly HashSet<string> UpdateHosts = BuildHostSet(
        ConfigConstants.UrlLauncherInfo,
        ConfigConstants.UrlAssetsBase);

    static ProxyRouting()
    {
        UpdateHosts.Add("api.github.com");
        UpdateHosts.Add("github.com");
        UpdateHosts.Add("objects.githubusercontent.com");
    }

    public static bool IsRobustHost(Uri uri)
        => RobustHosts.Contains(uri.Host);

    public static bool IsUpdateHost(Uri uri)
        => UpdateHosts.Contains(uri.Host);

    private static HashSet<string> BuildHostSet(params UrlFallbackSet[] sets)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in sets)
        {
            foreach (var url in set.Urls)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    hosts.Add(uri.Host);
            }
        }

        return hosts;
    }
}

public sealed class ProxyRoutingHandler : HttpMessageHandler
{
    private readonly DataManager _cfg;
    private readonly HttpMessageInvoker _direct;
    private readonly HttpMessageInvoker _proxy;

    public ProxyRoutingHandler(DataManager cfg, bool autoRedirect = true)
    {
        _cfg = cfg;

        var directHandler = HappyEyeballsHttp.CreateDirectHandler(autoRedirect);

        var proxy = new DynamicSocks5WebProxy(
            cfg,
            () => true,
            () => false);
        var proxyHandler = HappyEyeballsHttp.CreateProxyHandler(proxy, autoRedirect);

        _direct = new HttpMessageInvoker(directHandler, disposeHandler: true);
        _proxy = new HttpMessageInvoker(proxyHandler, disposeHandler: true);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri == null)
            return _direct.SendAsync(request, cancellationToken);

        var invoker = ShouldUseProxy(request.RequestUri) ? _proxy : _direct;
        return invoker.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _direct.Dispose();
            _proxy.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool ShouldUseProxy(Uri uri)
    {
        if (_cfg.GetCVar(CVars.LauncherProxyEnabled) && !LauncherProxyRuntimeState.DisableLauncherProxyForSession)
            return true;

        if (_cfg.GetCVar(CVars.LauncherProxyUpdatesEnabled)
            && !LauncherProxyRuntimeState.DisableUpdateProxyForSession
            && ProxyRouting.IsUpdateHost(uri))
        {
            return true;
        }

        if (_cfg.GetCVar(CVars.LauncherProxyBypassRegionEnabled)
            && !LauncherProxyRuntimeState.DisableBypassProxyForSession
            && ProxyRouting.IsRobustHost(uri))
        {
            return true;
        }

        return false;
    }
}
