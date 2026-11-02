namespace ServiceLib.Models.Entities;

public sealed record PingNSniSpoofingSettings
{
    public const string DefaultFakeSni = "hcaptcha.com,dash.cloudflare.com,api.hcaptcha.com";

    // Always allocate a free local port; keep this property for older saved profiles.
    public string ListenPort { get; init; } = "";
    public string Connect { get; init; } = "104.19.229.21:443,104.17.110.184:443,104.19.230.21:443";
    public string FakeRepeat { get; init; } = "1";
    public string FakeDelay { get; init; } = "2ms";
    public string AckTimeout { get; init; } = "2s";
    public string Injector { get; init; } = "active";
    public bool EnableFragment { get; init; } = false;
    public string FragmentDelay { get; init; } = "500ms";
    public string SniChunk { get; init; } = "3";
    public string RouteMode { get; init; } = "independent";
    public string IpRouting { get; init; } = "round-robin";
    public string SniRouting { get; init; } = "round-robin";

    public static bool IsUntouchedLegacyDefault(PingNSniSpoofingSettings settings, string? fakeSni) =>
        string.IsNullOrEmpty(fakeSni) && settings == new PingNSniSpoofingSettings
        {
            Connect = "",
            EnableFragment = false,
            IpRouting = "sticky",
            SniRouting = "sticky"
        };

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (!TryNormalizeConnect(Connect, out _, out error)) return false;
        if (!int.TryParse(FakeRepeat, out var repeat) || repeat is < 1 or > 100) error = "SNI fake repeat must be between 1 and 100.";
        else if (!ValidDuration(FakeDelay) || !ValidDuration(AckTimeout) || !ValidDuration(FragmentDelay)) error = "SNI delays and timeout must use Go durations such as 2ms, 500ms or 2s.";
        else if (Injector is not ("active" or "passive")) error = "SNI injector must be active or passive.";
        else if (!int.TryParse(SniChunk, out var chunk) || chunk is < 1 or > 255) error = "SNI chunk size must be between 1 and 255.";
        else if (RouteMode is not ("independent" or "pair-round-robin" or "pair-balanced")) error = "SNI route mode is invalid.";
        else if (IpRouting is not ("round-robin" or "sticky" or "failover")) error = "SNI IP routing is invalid.";
        else if (SniRouting is not ("round-robin" or "sticky")) error = "SNI routing is invalid.";
        return error.Length == 0;
    }

    public static bool TryNormalizeFakeSni(string? value, out string normalized, out string error)
    {
        var names = SplitEntries(value);
        normalized = string.Join(",", names);
        error = names.Any(name => Uri.CheckHostName(name) != UriHostNameType.Dns)
            ? "Fake SNI names must be valid hostnames separated by commas, spaces or new lines."
            : string.Empty;
        return error.Length == 0;
    }

    public static bool TryNormalizeConnect(string? value, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return true;

        var entries = SplitEntries(System.Text.RegularExpressions.Regex.Replace(value, @"\s*:\s*", ":"));
        var endpoints = new List<string>(entries.Length);
        int? commonPort = null;
        foreach (var entry in entries)
        {
            var separator = entry.LastIndexOf(':');
            var host = separator > 0 ? entry[..separator] : string.Empty;
            if (separator < 1 || !int.TryParse(entry[(separator + 1)..], out var port) || port is < 1 or > 65535)
            {
                error = "SNI connect endpoints must be host:port values separated by commas, spaces or new lines.";
                return false;
            }
            var validHost = host.StartsWith('[') && host.EndsWith(']')
                ? System.Net.IPAddress.TryParse(host[1..^1], out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                : (Uri.CheckHostName(host) is UriHostNameType.Dns or UriHostNameType.IPv4);
            if (!validHost || (commonPort.HasValue && commonPort.Value != port))
            {
                error = "SNI connect endpoints need valid hosts and the same port for every endpoint.";
                return false;
            }
            commonPort = port;
            endpoints.Add($"{host}:{port}");
        }
        normalized = string.Join(",", endpoints);
        return true;
    }

    private static bool ValidDuration(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d+(?:\.\d+)?(?:ns|us|µs|ms|s|m|h)$");

    private static string[] SplitEntries(string? value) =>
        (value ?? string.Empty).Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
