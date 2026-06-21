using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SS14.ProxyService;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!TryParseArgs(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 2;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Task? parentWatchTask = null;
        if (options.ParentPid is > 0)
            parentWatchTask = WatchParent(options.ParentPid.Value, cts);

        try
        {
            var relay = new UdpSocksRelay(options);
            await relay.RunAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[PROXY] fatal: {e}");
            return 1;
        }
        finally
        {
            if (parentWatchTask != null)
                await parentWatchTask;
        }
    }

    private static async Task WatchParent(int pid, CancellationTokenSource cts)
    {
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    cts.Cancel();
                    return;
                }
            }
            catch
            {
                cts.Cancel();
                return;
            }

            try
            {
                await Task.Delay(1000, cts.Token);
            }
            catch
            {
                return;
            }
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SS14.ProxyService --listen-host 127.0.0.1 --listen-port 29000 --target-host game.example --target-port 1212 --socks-host 127.0.0.1 --socks-port 1080 [--socks-user user --socks-pass pass] [--parent-pid 123]");
    }

    private static bool TryParseArgs(string[] args, out ProxyOptions options, out string error)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
                continue;

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                dict[key] = args[++i];
            else
                dict[key] = "true";
        }

        options = default!;
        error = "";

        if (!dict.TryGetValue("--listen-host", out var listenHost) || string.IsNullOrWhiteSpace(listenHost))
        {
            error = "Missing required argument: --listen-host";
            return false;
        }
        if (!dict.TryGetValue("--listen-port", out var listenPortRaw) || string.IsNullOrWhiteSpace(listenPortRaw))
        {
            error = "Missing required argument: --listen-port";
            return false;
        }
        if (!dict.TryGetValue("--target-host", out var targetHost) || string.IsNullOrWhiteSpace(targetHost))
        {
            error = "Missing required argument: --target-host";
            return false;
        }
        if (!dict.TryGetValue("--target-port", out var targetPortRaw) || string.IsNullOrWhiteSpace(targetPortRaw))
        {
            error = "Missing required argument: --target-port";
            return false;
        }
        if (!dict.TryGetValue("--socks-host", out var socksHost) || string.IsNullOrWhiteSpace(socksHost))
        {
            error = "Missing required argument: --socks-host";
            return false;
        }
        if (!dict.TryGetValue("--socks-port", out var socksPortRaw) || string.IsNullOrWhiteSpace(socksPortRaw))
        {
            error = "Missing required argument: --socks-port";
            return false;
        }

        if (!int.TryParse(listenPortRaw, out var listenPort) || listenPort is < 1 or > 65535)
        {
            error = "--listen-port must be 1..65535";
            return false;
        }
        if (!int.TryParse(targetPortRaw, out var targetPort) || targetPort is < 1 or > 65535)
        {
            error = "--target-port must be 1..65535";
            return false;
        }
        if (!int.TryParse(socksPortRaw, out var socksPort) || socksPort is < 1 or > 65535)
        {
            error = "--socks-port must be 1..65535";
            return false;
        }

        int? parentPid = null;
        if (dict.TryGetValue("--parent-pid", out var parentRaw))
        {
            if (!int.TryParse(parentRaw, out var parsedPid) || parsedPid <= 0)
            {
                error = "--parent-pid must be a positive integer";
                return false;
            }

            parentPid = parsedPid;
        }

        options = new ProxyOptions(
            listenHost.Trim(),
            listenPort,
            targetHost.Trim(),
            targetPort,
            socksHost.Trim(),
            socksPort,
            dict.GetValueOrDefault("--socks-user", ""),
            dict.GetValueOrDefault("--socks-pass", ""),
            parentPid);
        return true;
    }
}

internal sealed record ProxyOptions(
    string ListenHost,
    int ListenPort,
    string TargetHost,
    int TargetPort,
    string SocksHost,
    int SocksPort,
    string SocksUser,
    string SocksPass,
    int? ParentPid);

internal sealed class UdpSocksRelay
{
    private readonly ProxyOptions _options;

    public UdpSocksRelay(ProxyOptions options)
    {
        _options = options;
    }

    public async Task RunAsync(CancellationToken cancel)
    {
        using var relayCts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var relayCancel = relayCts.Token;

        using var control = await ConnectTcpPreferIpv4(_options.SocksHost, _options.SocksPort, cancel);
        using var stream = control.GetStream();

        await Socks5Handshake(stream, _options, relayCancel);

        var requestedListen = IPAddress.Parse(_options.ListenHost);
        using var clientUdp = new UdpClient(new IPEndPoint(requestedListen, _options.ListenPort));
        clientUdp.Client.ReceiveBufferSize = 1024 * 1024;
        clientUdp.Client.SendBufferSize = 1024 * 1024;

        var controlLocal = (IPEndPoint)control.Client.LocalEndPoint!;
        using var relayUdp = CreateRelayUdpClient(controlLocal.Address);
        relayUdp.Client.ReceiveBufferSize = 1024 * 1024;
        relayUdp.Client.SendBufferSize = 1024 * 1024;
        if (relayUdp.Client.AddressFamily == AddressFamily.InterNetworkV6)
        {
            try
            {
                relayUdp.Client.DualMode = true;
            }
            catch (SocketException e)
            {
                Console.WriteLine($"[PROXY] warn: could not enable dual-mode on relay socket ({e.SocketErrorCode})");
            }
        }

        var relayLocalPort = ((IPEndPoint)relayUdp.Client.LocalEndPoint!).Port;
        var associateClientEndpoint = new IPEndPoint(controlLocal.Address, relayLocalPort);
        var relayEndpoint = await UdpAssociate(stream, control, associateClientEndpoint, relayCancel);
        var currentRelayEndpoint = relayEndpoint;
        var targetEndpoint = await ResolveTargetEndpoint(cancel);
        var tcpWatchTask = WatchControlConnectionAsync(stream, relayCts, relayCancel);

        Console.WriteLine($"[PROXY] READY udp://{_options.ListenHost}:{_options.ListenPort} -> {targetEndpoint} via {relayEndpoint} (client-bound {clientUdp.Client.LocalEndPoint}, relay-bound {relayUdp.Client.LocalEndPoint})");

        IPEndPoint? clientEndpoint = null;
        var seenClientPacket = false;
        var seenRelayPacket = false;
        var sentToRelayPackets = 0;
        var receivedFromRelayPackets = 0;
        var warnedNoRelayTraffic = false;
        DateTimeOffset? firstClientPacketAt = null;

        var clientReceiveTask = clientUdp.ReceiveAsync(relayCancel).AsTask();
        var relayReceiveTask = relayUdp.ReceiveAsync(relayCancel).AsTask();

        try
        {
            while (!relayCancel.IsCancellationRequested)
            {
                Task<UdpReceiveResult> completed;
                try
                {
                    completed = await Task.WhenAny(clientReceiveTask, relayReceiveTask);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (completed == relayReceiveTask)
                {
                    if (clientEndpoint == null)
                    {
                        relayReceiveTask = relayUdp.ReceiveAsync(relayCancel).AsTask();
                        continue;
                    }

                    var packet = await relayReceiveTask;
                    byte[] payload;
                    if (TryParseSocksUdpPacket(packet.Buffer, out _, out _, out var parsedPayload))
                    {
                        payload = parsedPayload;
                    }
                    else
                    {
                        // Some non-compliant SOCKS relays can return raw UDP payload without SOCKS framing.
                        payload = packet.Buffer;
                    }

                    if (!packet.RemoteEndPoint.Equals(currentRelayEndpoint))
                    {
                        Console.WriteLine($"[PROXY] relay endpoint changed: {currentRelayEndpoint} -> {packet.RemoteEndPoint}");
                        currentRelayEndpoint = packet.RemoteEndPoint;
                    }

                    if (!seenRelayPacket)
                    {
                        seenRelayPacket = true;
                        Console.WriteLine($"[PROXY] first relay packet from {packet.RemoteEndPoint}, payload {payload.Length} bytes");
                    }
                    receivedFromRelayPackets++;

                    await clientUdp.SendAsync(payload, clientEndpoint, relayCancel);
                    relayReceiveTask = relayUdp.ReceiveAsync(relayCancel).AsTask();
                }
                else
                {
                    var packet = await clientReceiveTask;
                    if (clientEndpoint == null)
                    {
                        clientEndpoint = packet.RemoteEndPoint;
                    }
                    else if (!clientEndpoint.Equals(packet.RemoteEndPoint))
                    {
                        Console.WriteLine($"[PROXY] client endpoint changed: {clientEndpoint} -> {packet.RemoteEndPoint}");
                        clientEndpoint = packet.RemoteEndPoint;
                    }

                    if (!seenClientPacket)
                    {
                        seenClientPacket = true;
                        firstClientPacketAt = DateTimeOffset.UtcNow;
                        Console.WriteLine($"[PROXY] first client packet from {clientEndpoint}, size {packet.Buffer.Length} bytes");
                    }

                    var framed = BuildSocksUdpPacket(targetEndpoint, packet.Buffer);
                    var relaySendEndpoint = NormalizeEndpointForSocket(currentRelayEndpoint, relayUdp.Client.AddressFamily);
                    await relayUdp.SendAsync(framed, relaySendEndpoint, relayCancel);
                    sentToRelayPackets++;

                    if (!warnedNoRelayTraffic &&
                        firstClientPacketAt.HasValue &&
                        sentToRelayPackets >= 3 &&
                        receivedFromRelayPackets == 0 &&
                        DateTimeOffset.UtcNow - firstClientPacketAt.Value > TimeSpan.FromSeconds(2))
                    {
                        warnedNoRelayTraffic = true;
                        Console.WriteLine($"[PROXY] warn: sent {sentToRelayPackets} UDP packet(s) to SOCKS relay ({currentRelayEndpoint}) but received no UDP responses. Proxy may not support UDP ASSOCIATE for this route/server.");
                    }

                    clientReceiveTask = clientUdp.ReceiveAsync(relayCancel).AsTask();
                }
            }
        }
        finally
        {
            relayCts.Cancel();
            try
            {
                await tcpWatchTask;
            }
            catch
            {
            }
        }
    }

    private static UdpClient CreateRelayUdpClient(IPAddress preferredAddress)
    {
        if (preferredAddress.IsIPv4MappedToIPv6)
            preferredAddress = preferredAddress.MapToIPv4();

        try
        {
            return new UdpClient(new IPEndPoint(preferredAddress, 0));
        }
        catch (SocketException e)
        {
            var fallback = preferredAddress.AddressFamily == AddressFamily.InterNetworkV6
                ? IPAddress.IPv6Any
                : IPAddress.Any;

            Console.WriteLine($"[PROXY] warn: failed to bind relay UDP on {preferredAddress} ({e.SocketErrorCode}), fallback to {fallback}");
            return new UdpClient(new IPEndPoint(fallback, 0));
        }
    }

    private static async Task<TcpClient> ConnectTcpPreferIpv4(string host, int port, CancellationToken cancel)
    {
        if (IPAddress.TryParse(host, out var parsed))
        {
            var direct = new TcpClient(parsed.AddressFamily);
            try
            {
                await direct.ConnectAsync(parsed, port, cancel);
                return direct;
            }
            catch
            {
                direct.Dispose();
                throw;
            }
        }

        var addresses = await Dns.GetHostAddressesAsync(host, cancel);
        var ordered = addresses
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .ToArray();

        Exception? lastError = null;
        foreach (var address in ordered)
        {
            var client = new TcpClient(address.AddressFamily);
            try
            {
                await client.ConnectAsync(address, port, cancel);
                return client;
            }
            catch (Exception e)
            {
                lastError = e;
                client.Dispose();
            }
        }

        throw new InvalidOperationException($"Failed to connect to SOCKS server {host}:{port}", lastError);
    }

    private static IPEndPoint NormalizeEndpointForSocket(IPEndPoint endpoint, AddressFamily socketFamily)
    {
        if (socketFamily == AddressFamily.InterNetworkV6 &&
            endpoint.AddressFamily == AddressFamily.InterNetwork)
        {
            return new IPEndPoint(endpoint.Address.MapToIPv6(), endpoint.Port);
        }

        if (socketFamily == AddressFamily.InterNetwork &&
            endpoint.AddressFamily == AddressFamily.InterNetworkV6 &&
            endpoint.Address.IsIPv4MappedToIPv6)
        {
            return new IPEndPoint(endpoint.Address.MapToIPv4(), endpoint.Port);
        }

        return endpoint;
    }

    private static async Task WatchControlConnectionAsync(NetworkStream stream, CancellationTokenSource relayCts, CancellationToken cancel)
    {
        try
        {
            var probe = new byte[1];
            var read = await stream.ReadAsync(probe.AsMemory(0, 1), cancel);
            if (read == 0)
                Console.WriteLine("[PROXY] SOCKS TCP connection closed by server.");
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            // Any control-channel failure should stop relay as well.
        }

        if (!relayCts.IsCancellationRequested)
            relayCts.Cancel();
    }

    private static async Task Socks5Handshake(NetworkStream stream, ProxyOptions options, CancellationToken cancel)
    {
        var methods = new List<byte> { 0x00 };
        var hasUserPass = !string.IsNullOrWhiteSpace(options.SocksUser);
        if (hasUserPass)
            methods.Add(0x02);

        var greeting = new byte[2 + methods.Count];
        greeting[0] = 0x05;
        greeting[1] = (byte)methods.Count;
        for (var i = 0; i < methods.Count; i++)
            greeting[2 + i] = methods[i];

        await stream.WriteAsync(greeting, cancel);
        var response = new byte[2];
        await ReadExact(stream, response, cancel);
        if (response[0] != 0x05)
            throw new InvalidOperationException("SOCKS5 invalid version in handshake.");
        if (response[1] == 0xFF)
            throw new InvalidOperationException("SOCKS5 server rejected authentication methods.");

        if (response[1] == 0x02)
        {
            if (!hasUserPass)
                throw new InvalidOperationException("SOCKS5 server requires username/password, but credentials were not provided.");

            var userBytes = Encoding.UTF8.GetBytes(options.SocksUser);
            var passBytes = Encoding.UTF8.GetBytes(options.SocksPass ?? "");
            if (userBytes.Length > 255 || passBytes.Length > 255)
                throw new InvalidOperationException("SOCKS5 username/password too long.");

            var auth = new byte[3 + userBytes.Length + passBytes.Length];
            auth[0] = 0x01;
            auth[1] = (byte)userBytes.Length;
            Buffer.BlockCopy(userBytes, 0, auth, 2, userBytes.Length);
            auth[2 + userBytes.Length] = (byte)passBytes.Length;
            Buffer.BlockCopy(passBytes, 0, auth, 3 + userBytes.Length, passBytes.Length);
            await stream.WriteAsync(auth, cancel);

            var authResp = new byte[2];
            await ReadExact(stream, authResp, cancel);
            if (authResp[1] != 0x00)
                throw new InvalidOperationException("SOCKS5 username/password authentication failed.");
        }
        else if (response[1] != 0x00)
        {
            throw new InvalidOperationException($"SOCKS5 unsupported auth method selected: 0x{response[1]:X2}");
        }
    }

    private async Task<IPEndPoint> UdpAssociate(NetworkStream stream, TcpClient control, IPEndPoint localClientUdpEndpoint, CancellationToken cancel)
    {
        var bindAddress = localClientUdpEndpoint.Address;
        if (IPAddress.IsLoopback(bindAddress))
        {
            bindAddress = bindAddress.AddressFamily == AddressFamily.InterNetworkV6
                ? IPAddress.IPv6Any
                : IPAddress.Any;
        }

        if (bindAddress.AddressFamily == AddressFamily.InterNetworkV6 && bindAddress.IsIPv4MappedToIPv6)
            bindAddress = bindAddress.MapToIPv4();

        byte atyp;
        byte[] addrBytes;
        if (bindAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            atyp = 0x04;
            addrBytes = bindAddress.GetAddressBytes();
        }
        else
        {
            atyp = 0x01;
            addrBytes = bindAddress.MapToIPv4().GetAddressBytes();
        }

        var req = new byte[4 + 1 + addrBytes.Length + 2];
        req[0] = 0x05; // VER
        req[1] = 0x03; // CMD UDP ASSOCIATE
        req[2] = 0x00; // RSV
        req[3] = atyp; // ATYP
        Buffer.BlockCopy(addrBytes, 0, req, 4, addrBytes.Length);
        var off = 4 + addrBytes.Length;
        req[off] = (byte)(localClientUdpEndpoint.Port >> 8);
        req[off + 1] = (byte)(localClientUdpEndpoint.Port & 0xFF);

        await stream.WriteAsync(req, cancel);
        Console.WriteLine($"[PROXY] UDP ASSOCIATE request client={bindAddress}:{localClientUdpEndpoint.Port}");

        var head = new byte[4];
        await ReadExact(stream, head, cancel);
        if (head[0] != 0x05)
            throw new InvalidOperationException("SOCKS5 invalid version in UDP ASSOCIATE response.");
        if (head[1] != 0x00)
            throw new InvalidOperationException($"SOCKS5 UDP ASSOCIATE failed with code 0x{head[1]:X2}");

        var relayAddress = await ReadAddress(stream, head[3], cancel);
        var relayPortBytes = new byte[2];
        await ReadExact(stream, relayPortBytes, cancel);
        var relayPort = (relayPortBytes[0] << 8) | relayPortBytes[1];

        var remoteTcpIp = ((IPEndPoint)control.Client.RemoteEndPoint!).Address;
        relayAddress = await NormalizeRelayAddress(relayAddress, remoteTcpIp, cancel);

        return new IPEndPoint(relayAddress, relayPort);
    }

    private async Task<IPAddress> NormalizeRelayAddress(IPAddress relayAddress, IPAddress remoteTcpIp, CancellationToken cancel)
    {
        if (relayAddress.IsIPv4MappedToIPv6)
            relayAddress = relayAddress.MapToIPv4();
        if (remoteTcpIp.IsIPv4MappedToIPv6)
            remoteTcpIp = remoteTcpIp.MapToIPv4();

        if (relayAddress.Equals(IPAddress.Any) || relayAddress.Equals(IPAddress.IPv6Any))
            return remoteTcpIp;

        if (!IsLikelyInternal(relayAddress))
            return relayAddress;

        // Typical docker-bridge case: SOCKS returns 172.17.x.x, but client can only reach the published endpoint.
        if (IPAddress.IsLoopback(remoteTcpIp) || !IsLikelyInternal(remoteTcpIp))
            return remoteTcpIp;

        if (IPAddress.TryParse(_options.SocksHost, out var parsedHost))
            return parsedHost;

        try
        {
            var hostAddresses = await Dns.GetHostAddressesAsync(_options.SocksHost, cancel);
            var preferred = hostAddresses.FirstOrDefault(a => a.AddressFamily == relayAddress.AddressFamily)
                            ?? hostAddresses.FirstOrDefault();
            if (preferred != null)
                return preferred;
        }
        catch
        {
        }

        return remoteTcpIp;
    }

    private async Task<IPEndPoint> ResolveTargetEndpoint(CancellationToken cancel)
    {
        if (IPAddress.TryParse(_options.TargetHost, out var parsed))
            return new IPEndPoint(parsed, _options.TargetPort);

        var addresses = await Dns.GetHostAddressesAsync(_options.TargetHost, cancel);
        var selected = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                       ?? addresses.FirstOrDefault();
        if (selected == null)
            throw new InvalidOperationException($"Unable to resolve target host '{_options.TargetHost}'.");

        return new IPEndPoint(selected, _options.TargetPort);
    }

    private static bool EndpointsEqual(IPEndPoint a, IPEndPoint b)
    {
        if (a.Port != b.Port)
            return false;

        if (a.Address.Equals(b.Address))
            return true;

        // Some SOCKS servers reply with 0.0.0.0 but send from actual host.
        return b.Address.Equals(IPAddress.Any) || b.Address.Equals(IPAddress.IPv6Any);
    }

    private static bool IsRelayPacket(IPEndPoint remote, IPEndPoint relayEndpoint, IPEndPoint? clientEndpoint)
    {
        if (EndpointsEqual(remote, relayEndpoint))
            return true;

        // Some SOCKS servers/NATs send reply datagrams from an address different from UDP ASSOCIATE,
        // but they generally keep the same relay port.
        if (remote.Port != relayEndpoint.Port)
            return false;

        if (clientEndpoint != null &&
            remote.Address.Equals(clientEndpoint.Address) &&
            remote.Port == clientEndpoint.Port)
        {
            return false;
        }

        // Client is expected to be loopback; non-loopback endpoint on relay port is likely SOCKS relay response.
        if (!IPAddress.IsLoopback(remote.Address))
            return true;

        return false;
    }

    private static bool IsLikelyInternal(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            // Link-local 169.254/16
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast;
        }

        return false;
    }

    private static byte[] BuildSocksUdpPacket(IPEndPoint target, byte[] payload)
    {
        byte atyp;
        byte[] addrBytes;
        if (target.AddressFamily == AddressFamily.InterNetworkV6)
        {
            atyp = 0x04;
            addrBytes = target.Address.GetAddressBytes();
        }
        else
        {
            atyp = 0x01;
            addrBytes = target.Address.MapToIPv4().GetAddressBytes();
        }

        var packet = new byte[3 + 1 + addrBytes.Length + 2 + payload.Length];
        packet[0] = 0x00;
        packet[1] = 0x00;
        packet[2] = 0x00; // FRAG = 0
        packet[3] = atyp;
        Buffer.BlockCopy(addrBytes, 0, packet, 4, addrBytes.Length);
        var off = 4 + addrBytes.Length;
        packet[off] = (byte)(target.Port >> 8);
        packet[off + 1] = (byte)(target.Port & 0xFF);
        Buffer.BlockCopy(payload, 0, packet, off + 2, payload.Length);
        return packet;
    }

    private static bool TryParseSocksUdpPacket(byte[] packet, out IPAddress sourceAddress, out int sourcePort, out byte[] payload)
    {
        sourceAddress = IPAddress.Any;
        sourcePort = 0;
        payload = Array.Empty<byte>();

        if (packet.Length < 4)
            return false;
        if (packet[2] != 0x00) // FRAG
            return false;

        var atyp = packet[3];
        var off = 4;
        if (atyp == 0x01)
        {
            if (packet.Length < off + 4 + 2)
                return false;
            sourceAddress = new IPAddress(packet.AsSpan(off, 4));
            off += 4;
        }
        else if (atyp == 0x04)
        {
            if (packet.Length < off + 16 + 2)
                return false;
            sourceAddress = new IPAddress(packet.AsSpan(off, 16));
            off += 16;
        }
        else if (atyp == 0x03)
        {
            if (packet.Length < off + 1)
                return false;
            var len = packet[off];
            off += 1;
            if (packet.Length < off + len + 2)
                return false;
            // Domain is ignored for relay back path.
            off += len;
        }
        else
        {
            return false;
        }

        sourcePort = (packet[off] << 8) | packet[off + 1];
        off += 2;
        payload = packet.AsSpan(off).ToArray();
        return true;
    }

    private static async Task ReadExact(NetworkStream stream, byte[] buffer, CancellationToken cancel)
    {
        var off = 0;
        while (off < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(off, buffer.Length - off), cancel);
            if (read == 0)
                throw new IOException("SOCKS control connection closed.");
            off += read;
        }
    }

    private static async Task<IPAddress> ReadAddress(NetworkStream stream, byte atyp, CancellationToken cancel)
    {
        switch (atyp)
        {
            case 0x01:
            {
                var ipv4 = new byte[4];
                await ReadExact(stream, ipv4, cancel);
                return new IPAddress(ipv4);
            }
            case 0x04:
            {
                var ipv6 = new byte[16];
                await ReadExact(stream, ipv6, cancel);
                return new IPAddress(ipv6);
            }
            case 0x03:
            {
                var lenBuf = new byte[1];
                await ReadExact(stream, lenBuf, cancel);
                var hostBuf = new byte[lenBuf[0]];
                await ReadExact(stream, hostBuf, cancel);
                var host = Encoding.ASCII.GetString(hostBuf);
                if (IPAddress.TryParse(host, out var ip))
                    return ip;

                var addresses = await Dns.GetHostAddressesAsync(host, cancel);
                return addresses.FirstOrDefault() ?? IPAddress.Any;
            }
            default:
                throw new InvalidOperationException($"Unsupported SOCKS address type: {atyp}");
        }
    }
}
