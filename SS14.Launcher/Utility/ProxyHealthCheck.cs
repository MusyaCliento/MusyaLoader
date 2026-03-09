using System;
using System.Net.Sockets;
using System.Threading;

namespace SS14.Launcher.Utility;

public static class ProxyHealthCheck
{
    public static bool IsReachable(string host, int port, TimeSpan timeout, out string error)
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
