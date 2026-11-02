namespace ServiceLib.Models.Entities;

/// <summary>
/// Per-profile settings passed to the bundled BlackHole-compatible winws2
/// runtime. The settings live in ProtocolExtraItem.PingNDesyncJson.
/// </summary>
public sealed record PingNDesyncSettings
{
    public string Profile { get; init; } = "off";
    public string SplitMode { get; init; } = "multisplit";
    public string SplitPositions { get; init; } = "1,midsld";
    public string Fooling { get; init; } = "badseq";
    public int BadSequence { get; init; } = -10000;
    public int AutoTtlDelta { get; init; } = 2;
    public int AutoTtlMin { get; init; } = 3;
    public int AutoTtlMax { get; init; } = 20;
    public int Repeats { get; init; } = 1;
    public string FakeSni { get; init; } = string.Empty;

    public static bool TryCreateFromEditor(
        string? profile,
        string? splitMode,
        string? splitPositions,
        string? fooling,
        string? badSequence,
        string? repeats,
        string? autoTtlDelta,
        string? autoTtlMin,
        string? autoTtlMax,
        string? fakeSni,
        out PingNDesyncSettings settings,
        out string error)
    {
        var normalizedProfile = string.IsNullOrWhiteSpace(profile) ? "Off" : profile.Trim();
        var normalizedFooling = string.IsNullOrWhiteSpace(fooling) ? "badseq" : fooling.Trim().ToLowerInvariant();
        var isCustom = !normalizedProfile.Equals("Off", StringComparison.OrdinalIgnoreCase);

        if (isCustom && normalizedFooling == "badseq" && !int.TryParse(badSequence, out _))
        {
            settings = new();
            error = "Desync Bad sequence must be a whole number.";
            return false;
        }
        if (isCustom && normalizedFooling != "none" && !int.TryParse(repeats, out _))
        {
            settings = new();
            error = "Desync repeats must be a whole number.";
            return false;
        }
        if (isCustom && normalizedFooling == "ttl" &&
            (!int.TryParse(autoTtlDelta, out _) || !int.TryParse(autoTtlMin, out _) || !int.TryParse(autoTtlMax, out _)))
        {
            settings = new();
            error = "Desync AutoTTL values must be whole numbers.";
            return false;
        }

        settings = new PingNDesyncSettings
        {
            Profile = normalizedProfile,
            SplitMode = string.IsNullOrWhiteSpace(splitMode) ? "multisplit" : splitMode.Trim(),
            SplitPositions = string.IsNullOrWhiteSpace(splitPositions) ? "1,midsld" : splitPositions.Trim(),
            Fooling = normalizedFooling,
            BadSequence = int.TryParse(badSequence, out var parsedBadSequence) ? parsedBadSequence : -10000,
            Repeats = int.TryParse(repeats, out var parsedRepeats) ? parsedRepeats : 1,
            AutoTtlDelta = int.TryParse(autoTtlDelta, out var parsedDelta) ? parsedDelta : 2,
            AutoTtlMin = int.TryParse(autoTtlMin, out var parsedMin) ? parsedMin : 3,
            AutoTtlMax = int.TryParse(autoTtlMax, out var parsedMax) ? parsedMax : 20,
            FakeSni = fakeSni?.Trim() ?? string.Empty,
        };

        return settings.TryValidate(out error);
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (string.Equals(Profile, "Off", StringComparison.OrdinalIgnoreCase)) return true;
        if (Profile is not ("Custom" or "Speed - Light split" or "Balanced - BadSeq" or "Severe filtering - BadSeq" or "SNI spoof - WrongSeq" or "Severe filtering - AutoTTL"))
        {
            error = $"Unknown Desync profile '{Profile}'.";
            return false;
        }

        if (SplitMode is not ("multisplit" or "multidisorder"))
        {
            error = "Desync split method must be multisplit or multidisorder.";
            return false;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(SplitPositions, "^[a-zA-Z0-9,+-]+$"))
        {
            error = "Desync split positions contain unsupported characters.";
            return false;
        }
        if (Fooling is not ("none" or "badseq" or "badsum" or "md5sig" or "ttl"))
        {
            error = "Desync fake packet mode is invalid.";
            return false;
        }
        if (Fooling == "badseq" && (BadSequence >= 0 || BadSequence < -2_000_000_000))
        {
            error = "Desync Bad sequence must be a negative number greater than or equal to -2000000000.";
            return false;
        }
        if (Fooling != "none" && (Repeats is < 1 or > 10))
        {
            error = "Desync fake repeats must be between 1 and 10.";
            return false;
        }
        if (Fooling == "ttl" && (AutoTtlDelta is < 1 or > 10 || AutoTtlMin is < 1 or > 255 || AutoTtlMax is < 1 or > 255 || AutoTtlMin > AutoTtlMax))
        {
            error = "Desync AutoTTL requires delta 1–10 and a minimum/maximum TTL from 1–255 with minimum no greater than maximum.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(FakeSni) && Uri.CheckHostName(FakeSni) != UriHostNameType.Dns)
        {
            error = "Desync fake SNI must be a valid hostname.";
            return false;
        }
        return true;
    }

    public static PingNDesyncSettings DefaultsFor(string profile) => profile switch
    {
        "Speed - Light split" => new() { Profile = profile, SplitPositions = "midsld", Fooling = "none" },
        "Balanced - BadSeq" => new() { Profile = profile },
        "Severe filtering - BadSeq" => new() { Profile = profile, SplitMode = "multidisorder", SplitPositions = "1,sniext+1,midsld", Repeats = 2 },
        "SNI spoof - WrongSeq" => new() { Profile = profile, SplitMode = "multidisorder", SplitPositions = "1,sniext+1,midsld", BadSequence = -100000, Repeats = 2, FakeSni = "hcaptcha.com" },
        "Severe filtering - AutoTTL" => new() { Profile = profile, SplitMode = "multidisorder", Fooling = "ttl" },
        _ => new() { Profile = profile },
    };
}
