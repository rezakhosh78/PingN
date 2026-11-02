namespace ServiceLib.Models.Dto;

public static class ProfileDisplayAddress
{
    public static string Resolve(string address, ProtocolExtraItem? extra)
    {
        if (extra?.DnsTunnel is { } dns)
        {
            if (dns.Kind == "dnstt-ssh") return dns.Domain;
            if (dns.Kind == "masterdns")
            {
                try { return string.Join(", ", JsonSerializer.Deserialize<string[]>(dns.MasterOptions.GetValueOrDefault("DOMAINS", "[]")) ?? []); }
                catch (JsonException) { return dns.MasterOptions.GetValueOrDefault("DOMAINS", string.Empty); }
            }
        }
        if (extra?.WarpProfileType == OberonRuntimeManager.WarpProfileType)
            return WarpAmneziaConfigCodec.ReadFields(extra.WarpAmneziaConfig ?? string.Empty).GetValueOrDefault("Peer.Endpoint", "Pending registration");
        if (extra?.WarpProfileType == WarpMasqueRuntimeManager.WarpProfileType)
        {
            var endpoint = extra.WarpMasqueEndpoint ?? string.Empty;
            if (extra.WarpMasqueEndpointPort is not { } port) return endpoint;
            return endpoint.Contains(':') && !endpoint.StartsWith('[') ? $"[{endpoint}]:{port}" : $"{endpoint}:{port}";
        }
        return address;
    }
}
