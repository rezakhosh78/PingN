using System.Globalization;

namespace ServiceLib.Models.Entities;

public sealed record DnsFieldChoice(string Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Wire values follow the official MasterDnsVPN client configuration.</summary>
public static class MasterDnsEditorSchema
{
    public static string Label(string key) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' ').ToLowerInvariant());
    private static IReadOnlyList<DnsFieldChoice> Numbered(params string[] names)
        => names.Select((label, number) => new DnsFieldChoice(number.ToString(), $"{number} — {label}")).ToArray();
    public static IReadOnlyList<DnsFieldChoice>? Choices(string key, string currentValue) => key switch
    {
        "DATA_ENCRYPTION_METHOD" => Numbered("None", "XOR", "ChaCha20", "AES-128-GCM", "AES-192-GCM", "AES-256-GCM"),
        "RESOLVER_BALANCING_STRATEGY" => Numbered("Default (Round Robin)", "Random", "Round Robin", "Least Loss", "Lowest Latency",
            "Hybrid Score", "Loss Then Latency", "Least Loss Top Random", "Least Loss Top Round Robin"),
        "UPLOAD_COMPRESSION_TYPE" or "DOWNLOAD_COMPRESSION_TYPE" => Numbered("Off", "ZSTD", "LZ4", "ZLIB"),
        "LOG_LEVEL" => new[] { "DEBUG", "INFO", "WARN", "ERROR" }
            .Select(x => new DnsFieldChoice(JsonSerializer.Serialize(x), x)).ToArray(),
        _ when currentValue is "true" or "false" => new[] { new DnsFieldChoice("false", "Disabled"), new DnsFieldChoice("true", "Enabled") },
        _ => null
    };
}
