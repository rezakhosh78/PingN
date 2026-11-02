namespace ServiceLib.Models.Dto;

public static class RuntimeLogCategories
{
    public static readonly string[] Names = ["All", "Xray", "Desync", "SNI Spoofing Plus", "MasterDNS", "DNSTT"];
    public static string Detect(string message)
    {
        var text = message.TrimStart();
        // Also handle dated messages emitted by NoticeManager.SendMessageEx.
        text = Regex.Replace(text, @"^\d{4}/\d{2}/\d{2}\s+\d{2}:\d{2}:\d{2}\s+", string.Empty);
        if (text.StartsWith("[MasterDNS]", StringComparison.OrdinalIgnoreCase) || text.StartsWith("MasterDNS:", StringComparison.OrdinalIgnoreCase)) return "MasterDNS";
        if (text.StartsWith("[DNSTT", StringComparison.OrdinalIgnoreCase) || text.StartsWith("DNSTT:", StringComparison.OrdinalIgnoreCase)) return "DNSTT";
        if (text.StartsWith("Desync", StringComparison.OrdinalIgnoreCase)) return "Desync";
        if (text.StartsWith("SNI Spoofing Plus", StringComparison.OrdinalIgnoreCase)) return "SNI Spoofing Plus";
        return "Xray";
    }
}
