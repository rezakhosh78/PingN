namespace ServiceLib.Manager;

public static class DnsTunnelPortManager
{
    public const int MasterDnsPort = 18080;
    public const int DnsttSocksPort = 18081;

    public static bool IsPortAvailable(int port)
    {
        if (port is < 1 or > 65535) return false;
        try
        {
            using var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { ExclusiveAddressUse = true };
            tcp.Bind(new IPEndPoint(IPAddress.Loopback, port));
            using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { ExclusiveAddressUse = true };
            udp.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException) { return false; }
    }

    public static int FindFreePort(int preferred, IEnumerable<int> excluded)
    {
        var blocked = excluded.ToHashSet();
        for (var port = preferred; port < 65000; port++)
            if (!blocked.Contains(port) && IsPortAvailable(port)) return port;
        throw new IOException("No free local DNS tunnel port was found.");
    }

    public static async Task<bool> PrepareAsync(ProfileItem profile, Config config, Func<bool, string, Task>? log)
    {
        var settings = profile.GetProtocolExtra().DnsTunnel;
        if (settings is null) return false;
        var label = settings.Kind == "masterdns" ? "MasterDNS" : "DNSTT";
        var inbound = config.Inbound.First(x => x.Protocol == nameof(EInboundProtocol.socks));
        var excluded = Enumerable.Range(inbound.LocalPort, 7).ToHashSet();
        if (int.TryParse(settings.TunnelPort, out var tunnelPort)) excluded.Add(tunnelPort);
        var previous = profile.Port;
        if (settings.ListenerPortVersion == 0 || excluded.Contains(profile.Port) || !IsPortAvailable(profile.Port))
        {
            profile.Port = FindFreePort(settings.Kind == "masterdns" ? MasterDnsPort : DnsttSocksPort, excluded);
            settings.ListenerPortVersion = 1;
            profile.SetProtocolExtra(profile.GetProtocolExtra() with { DnsTunnel = settings });
            var stored = await AppManager.Instance.GetProfileItem(profile.IndexId);
            if (stored is not null)
            {
                stored.Port = profile.Port;
                stored.SetProtocolExtra(stored.GetProtocolExtra() with { DnsTunnel = settings });
                if (await ConfigHandler.AddSocksServer(config, stored) != 0) throw new IOException("Could not save DNS tunnel listener port.");
            }
        }
        if (log is not null) await log(false, $"[{label}] Client SOCKS listener: 127.0.0.1:{profile.Port} (previous {previous}).");
        // A separate process may own the application's shared inbound port. Move only
        // when actually occupied, after stopping PingN's own core; never kill its owner.
        var moved = false;
        bool BlockFree(int start) => start is >= 1024 and < 64990 && Enumerable.Range(start, 7)
            .All(p => p != profile.Port && p != tunnelPort && IsPortAvailable(p));
        if (!BlockFree(inbound.LocalPort))
        {
            var old = inbound.LocalPort;
            var next = 18090;
            while (next < 64990 && !BlockFree(next)) next += 10;
            if (next >= 64990) throw new IOException("No free application inbound port block was found.");
            inbound.LocalPort = next;
            if (await ConfigHandler.SaveConfig(config) != 0) { inbound.LocalPort = old; throw new IOException("Could not save the new application inbound port."); }
            moved = true;
            if (log is not null) await log(false, $"[{label}] Application inbound port {old} is occupied; changed to {next}. System proxy will use the new port.");
        }
        return moved;
    }
}
