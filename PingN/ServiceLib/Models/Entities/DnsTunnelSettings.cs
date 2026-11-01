using System.Text.Json;

namespace ServiceLib.Models.Entities;

public sealed class DnsTunnelSettings
{
    public string Kind { get; set; } = "masterdns";
    public int ListenerPortVersion { get; set; }
    public string Resolvers { get; set; } = "1.1.1.1:53\n8.8.8.8:53";
    // Values are TOML literals, so numeric/bool/array types survive editing and backups.
    public Dictionary<string, string> MasterOptions { get; set; } = DefaultMasterOptions();
    private static Dictionary<string, string> DefaultMasterOptions()
    {
        using var stream = typeof(DnsTunnelSettings).Assembly.GetManifestResourceStream("ServiceLib.Resources.DnsTunnel.client_config.toml.simple")
            ?? throw new IOException("Embedded MasterDNS configuration is missing.");
        using var reader = new StreamReader(stream);
        var options = ReadMasterFields(reader.ReadToEnd());
        options["DOMAINS"] = "[\"t.example.com\"]";
        return options;
    }
    public string Domain { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string Transport { get; set; } = "udp";
    public string Resolver { get; set; } = "1.1.1.1:53";
    public string TunnelPort { get; set; } = "17000";
    public string Utls { get; set; } = "none";
    public string SshUser { get; set; } = string.Empty;
    public string SshPassword { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string PrivateKeyPassphrase { get; set; } = string.Empty;
    public string HostKeySha256 { get; set; } = string.Empty;
    public string ConnectTimeoutSeconds { get; set; } = "120";
    public string KeepAliveSeconds { get; set; } = "30";

    public static Dictionary<string, string> ReadMasterFields(string text)
    {
        var result = new Dictionary<string, string>();
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^([A-Z][A-Z0-9_]*)\s*=\s*(.+)$");
            if (match.Success) result[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }
        return result;
    }

    public void Validate(int socksPort)
    {
        if (socksPort is < 1 or > 65535) throw new InvalidDataException("Local SOCKS port must be 1..65535.");
        if (Kind == "masterdns")
        {
            foreach (var (key, value) in MasterOptions)
            {
                if (!Regex.IsMatch(key, @"^[A-Z][A-Z0-9_]*$") || value.Contains('\n') || value.Contains('\r'))
                    throw new InvalidDataException($"Invalid TOML field: {key}");
                // The upstream sample uses JSON-compatible TOML scalar/array literals.
                using var parsed = JsonDocument.Parse(value);
            }
            using var domains = JsonDocument.Parse(MasterOptions.GetValueOrDefault("DOMAINS", "[]"));
            if (domains.RootElement.ValueKind != JsonValueKind.Array || domains.RootElement.GetArrayLength() == 0
                || domains.RootElement.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String
                    || Uri.CheckHostName(x.GetString()) != UriHostNameType.Dns || x.GetString()!.EndsWith("example.com")))
                throw new InvalidDataException("Enter your delegated server domain in DOMAINS.");
            var method = MasterOptions.GetValueOrDefault("DATA_ENCRYPTION_METHOD", "2");
            if (!int.TryParse(method, out var encryption) || encryption is < 0 or > 5)
                throw new InvalidDataException("DATA_ENCRYPTION_METHOD must be 0..5.");
            if (encryption != 0 && string.IsNullOrWhiteSpace(JsonSerializer.Deserialize<string>(MasterOptions.GetValueOrDefault("ENCRYPTION_KEY", "\"\""))))
                throw new InvalidDataException("Enter the server encryption key.");
            if (string.IsNullOrWhiteSpace(Resolvers)) throw new InvalidDataException("Enter at least one DNS resolver.");
        }
        else if (Kind == "dnstt-ssh")
        {
            if (Uri.CheckHostName(Domain) != UriHostNameType.Dns) throw new InvalidDataException("Enter the DNSTT tunnel domain.");
            if (!Regex.IsMatch(PublicKey, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("DNSTT public key must be 64 hexadecimal characters.");
            if (Transport is not ("udp" or "doh" or "dot")) throw new InvalidDataException("Transport must be udp, doh or dot.");
            if (string.IsNullOrWhiteSpace(Resolver)) throw new InvalidDataException("Enter the resolver address.");
            if (!int.TryParse(TunnelPort, out var port) || port is < 1 or > 65535 || port == socksPort)
                throw new InvalidDataException("Tunnel TCP port must be 1..65535 and different from the SOCKS port.");

            if (!int.TryParse(ConnectTimeoutSeconds, out var timeout) || timeout is < 5 or > 600) throw new InvalidDataException("Timeout must be 5..600 seconds.");
            if (!int.TryParse(KeepAliveSeconds, out var keepAlive) || keepAlive is < 5 or > 300) throw new InvalidDataException("Keepalive must be 5..300 seconds.");
            if (string.IsNullOrWhiteSpace(SshUser)) throw new InvalidDataException("Enter the SSH username.");
            if (string.IsNullOrEmpty(SshPassword) && !File.Exists(PrivateKeyPath)) throw new InvalidDataException("Enter the SSH password or a valid private key path.");
            if (!string.IsNullOrWhiteSpace(HostKeySha256) && !Regex.IsMatch(HostKeySha256, @"^SHA256:[A-Za-z0-9+/]{43}=?$"))
                throw new InvalidDataException("Enter the SSH server host key fingerprint (SHA256:...). Obtain it from your server administrator.");
        }
        else throw new InvalidDataException("Unknown DNS tunnel type.");
    }
}
