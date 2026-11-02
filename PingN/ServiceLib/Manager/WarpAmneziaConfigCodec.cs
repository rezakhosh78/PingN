namespace ServiceLib.Manager;

/// <summary>Reads the editable values from an AmneziaWG INI configuration while preserving other options.</summary>
public static class WarpAmneziaConfigCodec
{
    private static readonly string[] Sections = ["Interface", "Peer"];

    public static Dictionary<string, string> ReadFields(string? configuration)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
        foreach (var sourceLine in NormalizeLineEndings(configuration).Split('\n'))
        {
            var trimmed = sourceLine.Trim().TrimStart('\uFEFF');
            var closeBracket = trimmed.IndexOf(']');
            if (trimmed.StartsWith('[') && closeBracket > 0)
            {
                section = trimmed[1..closeBracket].Trim();
                continue;
            }
            if (section.Length == 0 || trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
                continue;
            result[$"{section}.{trimmed[..separator].Trim()}"] = trimmed[(separator + 1)..].Trim();
        }
        return result;
    }

    public static string UpdateFields(string configuration, IReadOnlyDictionary<string, string> fields)
    {
        var lines = NormalizeLineEndings(configuration).Split('\n').ToList();
        if (lines.All(string.IsNullOrWhiteSpace))
            lines = ["[Interface]", "", "[Peer]"];

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].Trim().TrimStart('\uFEFF');
            var closeBracket = trimmed.IndexOf(']');
            if (trimmed.StartsWith('[') && closeBracket > 0)
            {
                section = trimmed[1..closeBracket].Trim();
                continue;
            }
            if (section.Length == 0 || trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
                continue;
            var key = trimmed[..separator].Trim();
            var compositeKey = $"{section}.{key}";
            if (fields.TryGetValue(compositeKey, out var value))
            {
                lines[index] = $"{key} = {value.Trim()}";
                found.Add(compositeKey);
            }
        }

        foreach (var targetSection in Sections)
        {
            var missing = fields
                .Where(pair => pair.Key.StartsWith(targetSection + ".", StringComparison.OrdinalIgnoreCase)
                    && !found.Contains(pair.Key)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                .ToArray();
            if (missing.Length == 0)
                continue;

            var sectionIndex = lines.FindIndex(line => HasSectionHeader(line, targetSection));
            if (sectionIndex < 0)
            {
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                    lines.Add(string.Empty);
                lines.Add($"[{targetSection}]");
                sectionIndex = lines.Count - 1;
            }

            var insertionIndex = sectionIndex + 1;
            while (insertionIndex < lines.Count
                && !HasAnySectionHeader(lines[insertionIndex]))
                insertionIndex++;
            var values = missing.Select(pair => $"{pair.Key[(targetSection.Length + 1)..]} = {pair.Value.Trim()}");
            lines.InsertRange(insertionIndex, values);
        }

        return string.Join(Environment.NewLine, lines).Trim() + Environment.NewLine;
    }

    private static string NormalizeLineEndings(string? configuration) =>
        (configuration ?? string.Empty).TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');

    private static bool HasSectionHeader(string line, string section)
    {
        var trimmed = line.Trim().TrimStart('\uFEFF');
        var closeBracket = trimmed.IndexOf(']');
        return trimmed.StartsWith('[') && closeBracket > 0
            && string.Equals(trimmed[1..closeBracket].Trim(), section, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnySectionHeader(string line)
    {
        var trimmed = line.Trim().TrimStart('\uFEFF');
        return trimmed.StartsWith('[') && trimmed.IndexOf(']') > 0;
    }
}
