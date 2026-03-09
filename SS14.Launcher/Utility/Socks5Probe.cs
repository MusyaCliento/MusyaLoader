using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

public static class Socks5Probe
{
    public static async Task<long> ProbeTcpConnectOnlyAsync(Socks5ProxyConfig proxy, CancellationToken cancel)
    {
        var sw = Stopwatch.StartNew();
        using var client = await ConnectTcpPreferIpv4(proxy.Host, proxy.Port, cancel);
        sw.Stop();
        var ms = (long)Math.Round(sw.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero);
        return Math.Max(1, ms);
    }

    public static async Task<(bool Ok, string Error)> ProbeHandshakeAsync(Socks5ProxyConfig proxy, CancellationToken cancel)
    {
        try
        {
            using var client = await ConnectTcpPreferIpv4(proxy.Host, proxy.Port, cancel);
            using var stream = client.GetStream();
            await Socks5HandshakeAsync(stream, proxy, cancel);
            return (true, "");
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }

    public static async Task<long> ProbeTcpAsync(Socks5ProxyConfig proxy, CancellationToken cancel)
    {
        var sw = Stopwatch.StartNew();
        using var client = await ConnectTcpPreferIpv4(proxy.Host, proxy.Port, cancel);
        using var stream = client.GetStream();

        // Lightweight RTT probe: SOCKS greeting request + server method response (single roundtrip).
        var hasUserPass = !string.IsNullOrWhiteSpace(proxy.Username);
        var methods = hasUserPass ? new byte[] { 0x00, 0x02 } : new byte[] { 0x00 };
        var greeting = new byte[2 + methods.Length];
        greeting[0] = 0x05;
        greeting[1] = (byte)methods.Length;
        Buffer.BlockCopy(methods, 0, greeting, 2, methods.Length);
        await stream.WriteAsync(greeting, cancel);

        var response = new byte[2];
        await ReadExactAsync(stream, response, cancel);
        if (response[0] != 0x05)
            throw new InvalidOperationException("SOCKS5 invalid handshake version.");

        sw.Stop();
        var ms = (long)Math.Round(sw.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero);
        return Math.Max(1, ms);
    }

    public static async Task<(bool Ok, string Error)> ProbeUdpAssociateAsync(Socks5ProxyConfig proxy, CancellationToken cancel)
    {
        try
        {
            using var client = await ConnectTcpPreferIpv4(proxy.Host, proxy.Port, cancel);
            using var stream = client.GetStream();
            await Socks5HandshakeAsync(stream, proxy, cancel);
            await SendUdpAssociateAsync(stream, cancel);
            return (true, "");
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }

    private static async Task Socks5HandshakeAsync(NetworkStream stream, Socks5ProxyConfig proxy, CancellationToken cancel)
    {
        var hasUserPass = !string.IsNullOrWhiteSpace(proxy.Username);
        var methods = hasUserPass ? new byte[] { 0x00, 0x02 } : new byte[] { 0x00 };

        var greeting = new byte[2 + methods.Length];
        greeting[0] = 0x05;
        greeting[1] = (byte)methods.Length;
        Buffer.BlockCopy(methods, 0, greeting, 2, methods.Length);
        await stream.WriteAsync(greeting, cancel);

        var response = new byte[2];
        await ReadExactAsync(stream, response, cancel);
        if (response[0] != 0x05)
            throw new InvalidOperationException("SOCKS5 invalid handshake version.");
        if (response[1] == 0xFF)
            throw new InvalidOperationException("SOCKS5 rejected auth methods.");

        if (response[1] == 0x02)
        {
            if (!hasUserPass)
                throw new InvalidOperationException("SOCKS5 requires username/password.");

            var user = Encoding.UTF8.GetBytes(proxy.Username);
            var pass = Encoding.UTF8.GetBytes(proxy.Password ?? "");
            if (user.Length > 255 || pass.Length > 255)
                throw new InvalidOperationException("SOCKS5 credentials are too long.");

            var auth = new byte[3 + user.Length + pass.Length];
            auth[0] = 0x01;
            auth[1] = (byte)user.Length;
            Buffer.BlockCopy(user, 0, auth, 2, user.Length);
            auth[2 + user.Length] = (byte)pass.Length;
            Buffer.BlockCopy(pass, 0, auth, 3 + user.Length, pass.Length);
            await stream.WriteAsync(auth, cancel);

            var authResponse = new byte[2];
            await ReadExactAsync(stream, authResponse, cancel);
            if (authResponse[1] != 0x00)
                throw new InvalidOperationException("SOCKS5 auth failed.");
        }
    }

    private static async Task SendUdpAssociateAsync(NetworkStream stream, CancellationToken cancel)
    {
        // UDP ASSOCIATE with INADDR_ANY:0
        var request = new byte[]
        {
            0x05, 0x03, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        };
        await stream.WriteAsync(request, cancel);

        var header = new byte[4];
        await ReadExactAsync(stream, header, cancel);
        if (header[0] != 0x05)
            throw new InvalidOperationException("SOCKS5 invalid UDP ASSOCIATE response.");
        if (header[1] != 0x00)
            throw new InvalidOperationException($"SOCKS5 UDP ASSOCIATE rejected: 0x{header[1]:X2}.");

        await ReadAddressAsync(stream, header[3], cancel);

        var portBytes = new byte[2];
        await ReadExactAsync(stream, portBytes, cancel);
    }

    private static async Task SendConnectAsync(NetworkStream stream, string host, int port, CancellationToken cancel)
    {
        var hostBytes = Encoding.ASCII.GetBytes(host);
        if (hostBytes.Length is < 1 or > 255)
            throw new InvalidOperationException("Probe host is invalid.");

        var request = new byte[7 + hostBytes.Length];
        request[0] = 0x05; // VER
        request[1] = 0x01; // CONNECT
        request[2] = 0x00; // RSV
        request[3] = 0x03; // DOMAIN
        request[4] = (byte)hostBytes.Length;
        Buffer.BlockCopy(hostBytes, 0, request, 5, hostBytes.Length);
        var off = 5 + hostBytes.Length;
        request[off] = (byte)(port >> 8);
        request[off + 1] = (byte)(port & 0xFF);

        await stream.WriteAsync(request, cancel);

        var header = new byte[4];
        await ReadExactAsync(stream, header, cancel);
        if (header[0] != 0x05)
            throw new InvalidOperationException("SOCKS5 invalid CONNECT response.");
        if (header[1] != 0x00)
            throw new InvalidOperationException($"SOCKS5 CONNECT rejected: 0x{header[1]:X2}.");

        await ReadAddressAsync(stream, header[3], cancel);
        var portBytes = new byte[2];
        await ReadExactAsync(stream, portBytes, cancel);
    }

    private static async Task ReadAddressAsync(NetworkStream stream, byte atyp, CancellationToken cancel)
    {
        switch (atyp)
        {
            case 0x01:
                await ReadExactAsync(stream, new byte[4], cancel);
                break;
            case 0x04:
                await ReadExactAsync(stream, new byte[16], cancel);
                break;
            case 0x03:
                var length = new byte[1];
                await ReadExactAsync(stream, length, cancel);
                await ReadExactAsync(stream, new byte[length[0]], cancel);
                break;
            default:
                throw new InvalidOperationException($"Unsupported SOCKS5 ATYP: {atyp}");
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancel)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancel);
            if (n == 0)
                throw new IOException("SOCKS5 connection closed.");
            read += n;
        }
    }

    private static async Task<TcpClient> ConnectTcpPreferIpv4(string host, int port, CancellationToken cancel)
    {
        if (IPAddress.TryParse(host, out var parsedIp))
            return await ConnectToSingleAddress(parsedIp, port, cancel);

        var addresses = await Dns.GetHostAddressesAsync(host, cancel);
        if (addresses.Length == 0)
            throw new SocketException((int)SocketError.HostNotFound);

        var ordered = addresses
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .ToArray();

        Exception? lastError = null;
        foreach (var address in ordered)
        {
            try
            {
                return await ConnectToSingleAddress(address, port, cancel);
            }
            catch (Exception e) when (e is SocketException or IOException or OperationCanceledException)
            {
                lastError = e;
                if (cancel.IsCancellationRequested)
                    throw;
            }
        }

        throw lastError ?? new IOException("Failed to connect to proxy endpoint.");
    }

    private static async Task<TcpClient> ConnectToSingleAddress(IPAddress address, int port, CancellationToken cancel)
    {
        var client = new TcpClient(address.AddressFamily);
        try
        {
            await client.ConnectAsync(address, port, cancel);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
