using System.Net;

namespace ServiceLib.Manager;

public sealed record DesyncScanResult(PingNDesyncSettings Settings, int Delay)
{
    public string Profile => Settings.Profile;

    public override string ToString()
    {
        var fake = Settings.Fooling switch
        {
            "badseq" => $"badseq {Settings.BadSequence}, x{Settings.Repeats}",
            "ttl" => $"TTL {Settings.AutoTtlDelta}:{Settings.AutoTtlMin}-{Settings.AutoTtlMax}, x{Settings.Repeats}",
            "none" => "no fake",
            _ => $"{Settings.Fooling} x{Settings.Repeats}"
        };
        return $"{Settings.SplitMode} / {Settings.SplitPositions} / {fake}: {(Delay > 0 ? $"{Delay} ms" : "failed")}";
    }
}

public static class DesyncPresetScanner
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<List<DesyncScanResult>> ScanAsync(
        ProfileItem source, Config config, PingNDesyncSettings selectedSettings, IProgress<string> progress)
    {
        if (!Utils.IsWindows()) throw new InvalidOperationException("Desync scanning requires Windows.");
        if (selectedSettings.Profile.Equals("Off", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select a Desync profile before scanning its settings.");
        if (!selectedSettings.TryValidate(out var error)) throw new InvalidOperationException(error);
        if (!await Gate.WaitAsync(0)) throw new InvalidOperationException("Desync scan is already running.");
        var results = new List<DesyncScanResult>();
        var previousId = config.IndexId;
        var candidates = BuildCandidates(selectedSettings);
        try
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                var settings = candidates[index];
                progress.Report($"Testing {selectedSettings.Profile} ({index + 1}/{candidates.Count}): {settings.SplitMode}, {settings.SplitPositions}, {settings.Fooling}...");
                try
                {
                    var node = JsonUtils.DeepCopy(source);
                    node.SetProtocolExtra(node.GetProtocolExtra() with
                    {
                        PingNDesyncJson = JsonSerializer.Serialize(settings),
                        SniSpoofingPlusEnabled = false
                    });
                    var context = await CoreConfigContextBuilder.BuildAll(config, node);
                    if (!context.Success)
                    {
                        results.Add(new(settings, -1));
                        progress.Report($"{settings.Profile}: invalid configuration");
                        continue;
                    }
                    await CoreManager.Instance.LoadCore(context.MainResult.Context, context.PreSocksResult?.Context);
                    await Task.Delay(600);
                    var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
                    var delay = await ConnectionHandler.GetRealPingTime(new WebProxy($"socks5://{Global.Loopback}:{port}"));
                    results.Add(new(settings, delay));
                    progress.Report(results[^1].ToString());
                }
                catch (Exception ex)
                {
                    results.Add(new(settings, -1));
                    Logging.SaveLog($"Desync scan: candidate {index + 1}/{candidates.Count} failed", ex);
                    progress.Report($"{settings.Profile} ({index + 1}/{candidates.Count}): {ex.Message}");
                }
            }
            return results;
        }
        finally
        {
            try
            {
                var previous = await AppManager.Instance.GetProfileItem(previousId);
                if (previous is null) await CoreManager.Instance.CoreStop();
                else
                {
                    var context = await CoreConfigContextBuilder.BuildAll(config, previous);
                    if (context.Success)
                        await CoreManager.Instance.LoadCore(context.MainResult.Context, context.PreSocksResult?.Context);
                    else await CoreManager.Instance.CoreStop();
                }
                progress.Report("Scan finished; previous connection restored.");
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Desync scan: could not restore previous connection", ex);
                progress.Report($"Scan finished; restore failed: {ex.Message}");
            }
            Gate.Release();
        }
    }

    public static List<PingNDesyncSettings> BuildCandidates(PingNDesyncSettings selected)
    {
        var result = new List<PingNDesyncSettings>();
        var seen = new HashSet<PingNDesyncSettings>();
        void Add(PingNDesyncSettings candidate)
        {
            if (candidate.TryValidate(out _) && seen.Add(candidate)) result.Add(candidate);
        }

        Add(selected);
        var positions = new[] { selected.SplitPositions, "1,midsld", "midsld", "1,sniext+1,midsld" }
            .Distinct(StringComparer.Ordinal).Take(3).ToArray();
        var modes = new[] { selected.SplitMode, selected.SplitMode == "multisplit" ? "multidisorder" : "multisplit" };
        foreach (var mode in modes)
        foreach (var position in positions)
        {
            var basic = selected with { SplitMode = mode, SplitPositions = position };
            if (selected.Fooling == "badseq")
            {
                foreach (var sequence in new[] { selected.BadSequence, -10000, -100000 }.Distinct().Take(3))
                foreach (var repeats in new[] { selected.Repeats, 1, 2 }.Distinct().Take(2))
                    Add(basic with { BadSequence = sequence, Repeats = repeats });
            }
            else if (selected.Fooling == "ttl")
            {
                var ttlOptions = new[]
                {
                    (selected.AutoTtlDelta, selected.AutoTtlMin, selected.AutoTtlMax),
                    (1, 3, 20), (2, 3, 20), (3, 5, 30)
                }.Distinct().Take(3);
                foreach (var (delta, min, max) in ttlOptions)
                foreach (var repeats in new[] { selected.Repeats, 1, 2 }.Distinct().Take(2))
                    Add(basic with { AutoTtlDelta = delta, AutoTtlMin = min, AutoTtlMax = max, Repeats = repeats });
            }
            else if (selected.Fooling == "none") Add(basic);
            else
            {
                foreach (var repeats in new[] { selected.Repeats, 1, 2 }.Distinct().Take(2))
                    Add(basic with { Repeats = repeats });
            }
        }
        return result;
    }
}
