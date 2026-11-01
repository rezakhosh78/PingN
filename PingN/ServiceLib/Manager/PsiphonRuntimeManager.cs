
namespace ServiceLib.Manager;

public sealed record PsiphonLaunchPlan(string ProfileId, string Executable, string ConfigPath,
    string Directory, int UpstreamPort, int SocksPort);

/// <summary>
/// Application -> existing core -> Psiphon SOCKS -> private core inbound -> selected outbound.
/// The private inbound is always routed before user traffic. No direct fallback is installed.
/// </summary>
public static class PsiphonRuntimeManager
{
    private const string UpstreamTag = "pingn-psiphon-upstream";
    private const string ExitTag = "pingn-psiphon-exit";
    private const string BlockTag = "pingn-psiphon-block";
    private static readonly ConcurrentDictionary<string, PsiphonConnectionState> States = new();
    private static Process? _process;
    [SupportedOSPlatform("windows")] private static WindowsJobService? _job;
    private static CancellationTokenSource? _lifetime;
    private static Task? _monitor;
    private static string? _activeId;
    private static long _generation;
    private static readonly object StateGate = new();

    public static PsiphonConnectionState GetState(string id) => States.GetOrAdd(id, _ => new());

    private static void SetState(string id, string state, long generation)
    {
        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            lock (StateGate)
            {
                if (generation != _generation) return;
                var item = GetState(id);
                item.Color = state switch { "Connected" => "#16A34A", "Connecting" => "#D97706", _ => "#DC2626" };
                item.Text = state;
            }
        });
    }

    public static async Task<PsiphonLaunchPlan?> PrepareAsync(ProfileItem profile, string coreConfig, ECoreType coreType)
    {
        if (!profile.GetProtocolExtra().PsiphonOver) return null;
        if (coreType is not (ECoreType.Xray or ECoreType.sing_box))
            throw new InvalidOperationException("Psiphon Over requires Xray or sing-box. Select one of these cores for this profile.");

        var executable = Utils.GetBinPath(Utils.GetExeName("psiphon-tunnel-core"), "Psiphon");
        if (!File.Exists(executable))
            throw new FileNotFoundException("Bundled Psiphon core is missing. Reinstall the complete PingN package (bin/Psiphon).", executable);
        if (Utils.IsNonWindows()) await Utils.SetLinuxChmod(executable);
        var directory = Utils.GetConfigPath(Path.Combine("Psiphon", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile.IndexId)))[..24]));
        System.IO.Directory.CreateDirectory(directory);
        // Reserve both ports together so they cannot be identical. Core startup detects any subsequent race.
        var first = new TcpListener(IPAddress.Loopback, 0);
        var second = new TcpListener(IPAddress.Loopback, 0);
        int upstreamPort, socksPort;
        try
        {
            first.Start(); second.Start();
            upstreamPort = ((IPEndPoint)first.LocalEndpoint).Port;
            socksPort = ((IPEndPoint)second.LocalEndpoint).Port;
        }
        finally { first.Stop(); second.Stop(); }

        var root = JsonNode.Parse(await File.ReadAllTextAsync(coreConfig))?.AsObject()
            ?? throw new InvalidOperationException("Psiphon Over: invalid core JSON.");
        ConfigureRouting(root, coreType == ECoreType.sing_box, upstreamPort, socksPort);
        var template = JsonNode.Parse(EmbedUtils.GetEmbedText("ServiceLib.Resources.Psiphon.network.json"))!.AsObject();
        var region = profile.GetProtocolExtra().PsiphonEgressRegion?.Trim().ToUpperInvariant() ?? string.Empty;
        if (region.Length != 0 && !PsiphonRegionOption.All.Any(option => option.Code == region))
            throw new InvalidOperationException($"Psiphon Over: unsupported egress region '{region}'. Choose a country in the profile editor.");
        template["EgressRegion"] = region;
        template["DataRootDirectory"] = directory;
        template["LocalSocksProxyPort"] = socksPort;
        template["DisableLocalHTTPProxy"] = true;
        template["UpstreamProxyURL"] = $"socks5://127.0.0.1:{upstreamPort}";
        template["EmitDiagnosticNotices"] = true;
        template["EstablishTunnelTimeoutSeconds"] = 0;
        template["TunnelPoolSize"] = 1;
        template["SplitTunnelOwnRegion"] = false;
        var configPath = Path.Combine(directory, "psiphon.json");
        await File.WriteAllTextAsync(configPath, template.ToJsonString());
        await File.WriteAllTextAsync(coreConfig, root.ToJsonString());
        return new(profile.IndexId, executable, configPath, directory, upstreamPort, socksPort);
    }

    /// <summary>Kept independent of process launch so generated routing can be regression tested.</summary>
    public static void ConfigureRouting(JsonObject root, bool singbox, int upstreamPort, int socksPort)
    {
        var inbounds = root["inbounds"] as JsonArray ?? throw new InvalidOperationException("Psiphon Over needs local core inbounds.");
        var outbounds = root["outbounds"] as JsonArray ?? throw new InvalidOperationException("Psiphon Over needs core outbounds.");
        if (outbounds.OfType<JsonObject>().Any(x => x["tag"]?.GetValue<string>() == ExitTag))
            throw new InvalidOperationException("Psiphon Over was already applied to this configuration.");
        var allOutbounds = outbounds.OfType<JsonObject>().Concat((root["endpoints"] as JsonArray ?? []).OfType<JsonObject>()).ToList();
        var primary = allOutbounds.FirstOrDefault(x => x["tag"]?.GetValue<string>() == "proxy")
            ?? (singbox && root["route"]?["final"] is JsonValue final
                ? allOutbounds.FirstOrDefault(x => x["tag"]?.GetValue<string>() == final.GetValue<string>()) : null)
            ?? allOutbounds.FirstOrDefault();
        var primaryTag = primary?["tag"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(primaryTag)) throw new InvalidOperationException("Psiphon Over: the selected outbound must have a tag.");
        var userTags = new List<string>();
        foreach (var inbound in inbounds.OfType<JsonObject>())
        {
            var tag = inbound["tag"]?.GetValue<string>();
            if (tag is "api" or "dns" or "dns-in") continue;
            if (string.IsNullOrEmpty(tag)) { tag = $"pingn-user-{userTags.Count}"; inbound["tag"] = tag; }
            userTags.Add(tag);
        }
        if (userTags.Count == 0) throw new InvalidOperationException("Psiphon Over: no application inbound found.");
        JsonArray Tags() => new(userTags.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        if (singbox)
        {
            inbounds.Add(new JsonObject { ["type"] = "socks", ["tag"] = UpstreamTag, ["listen"] = "127.0.0.1", ["listen_port"] = upstreamPort });
            outbounds.Add(new JsonObject { ["type"] = "socks", ["tag"] = ExitTag, ["server"] = "127.0.0.1", ["server_port"] = socksPort, ["version"] = "5", ["network"] = "tcp" });
            var route = root["route"] as JsonObject ?? new JsonObject(); root["route"] = route;
            var rules = route["rules"] as JsonArray ?? new JsonArray(); route["rules"] = rules;
            // Core DNS remains available during bootstrap. QUIC and other UDP never fall back to the base IP.
            rules.Insert(0, new JsonObject { ["inbound"] = new JsonArray(UpstreamTag), ["action"] = "route", ["outbound"] = primaryTag });
            rules.Insert(1, new JsonObject { ["inbound"] = Tags(), ["port"] = 53, ["action"] = "hijack-dns" });
            rules.Insert(2, new JsonObject { ["inbound"] = Tags(), ["network"] = "udp", ["action"] = "reject" });
            rules.Insert(3, new JsonObject { ["inbound"] = Tags(), ["action"] = "route", ["outbound"] = ExitTag });
        }
        else
        {
            inbounds.Add(new JsonObject { ["protocol"] = "socks", ["tag"] = UpstreamTag, ["listen"] = "127.0.0.1", ["port"] = upstreamPort,
                ["settings"] = new JsonObject { ["auth"] = "noauth", ["udp"] = false } });
            outbounds.Add(new JsonObject { ["protocol"] = "socks", ["tag"] = ExitTag,
                ["settings"] = new JsonObject { ["servers"] = new JsonArray(new JsonObject { ["address"] = "127.0.0.1", ["port"] = socksPort }) } });
            outbounds.Add(new JsonObject { ["protocol"] = "blackhole", ["tag"] = BlockTag });
            var route = root["routing"] as JsonObject ?? new JsonObject(); root["routing"] = route;
            var rules = route["rules"] as JsonArray ?? new JsonArray(); route["rules"] = rules;
            rules.Insert(0, new JsonObject { ["type"] = "field", ["inboundTag"] = new JsonArray(UpstreamTag), ["outboundTag"] = primaryTag });
            // DNS is carried as TCP by Xray's DNS outbound through Psiphon, not raw UDP.
            outbounds.Add(new JsonObject { ["protocol"] = "dns", ["tag"] = "pingn-psiphon-dns",
                ["settings"] = new JsonObject { ["network"] = "tcp", ["address"] = "1.1.1.1", ["port"] = 53 },
                ["streamSettings"] = new JsonObject { ["sockopt"] = new JsonObject { ["dialerProxy"] = ExitTag } } });
            rules.Insert(1, new JsonObject { ["type"] = "field", ["inboundTag"] = Tags(), ["port"] = "53", ["outboundTag"] = "pingn-psiphon-dns" });
            rules.Insert(2, new JsonObject { ["type"] = "field", ["inboundTag"] = Tags(), ["network"] = "udp", ["outboundTag"] = BlockTag });
            rules.Insert(3, new JsonObject { ["type"] = "field", ["inboundTag"] = Tags(), ["outboundTag"] = ExitTag });
        }
    }

    public static async Task StartAsync(PsiphonLaunchPlan? plan, Func<bool> baseIsAlive)
    {
        if (plan is null) return;
        _activeId = plan.ProfileId;
        var generation = Interlocked.Increment(ref _generation);
        SetState(plan.ProfileId, "Connecting", generation);
        _lifetime = new CancellationTokenSource();
        var token = _lifetime.Token;
        // Wait for the isolated upstream listener, not an unrelated application's SOCKS listener.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (!baseIsAlive()) throw new IOException("Psiphon Over: base core exited before startup.");
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, plan.UpstreamPort, timeout.Token);
                break;
            }
            catch (SocketException) { await Task.Delay(100, timeout.Token); }
        }
        var process = new Process { StartInfo = new ProcessStartInfo(plan.Executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
            RedirectStandardOutput = true, WorkingDirectory = plan.Directory
        }};
        process.StartInfo.ArgumentList.Add("-config");
        process.StartInfo.ArgumentList.Add(plan.ConfigPath);
        _process = process;
        if (!process.Start()) throw new IOException("Psiphon Over: core could not start.");
        if (Utils.IsWindows())
        {
            _job = new WindowsJobService();
            if (!_job.AddProcess(process.Handle)) throw new IOException("Psiphon Over: could not attach core to application lifetime.");
        }
        _monitor = MonitorAsync(process, plan, generation, baseIsAlive, token);
    }

    private static async Task MonitorAsync(Process process, PsiphonLaunchPlan plan, long generation, Func<bool> baseIsAlive, CancellationToken token)
    {
        using var readerCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        async Task Read(StreamReader reader)
        {
            while (await reader.ReadLineAsync(readerCancellation.Token) is { } line)
            {
                // Psiphon diagnostics can contain IPs; retain them locally, never infer connection from arbitrary text.
                Logging.SaveLog($"Psiphon: {line}");
                try
                {
                    var notice = JsonNode.Parse(line);
                    if (notice?["noticeType"]?.GetValue<string>() == "Tunnels")
                        SetState(plan.ProfileId, notice["data"]?["count"]?.GetValue<int>() > 0 ? "Connected" : "Connecting", generation);
                }
                catch (JsonException) { }
            }
        }
        var readers = Task.WhenAll(Read(process.StandardOutput), Read(process.StandardError));
        try
        {
            while (!process.HasExited && !token.IsCancellationRequested)
            {
                if (!baseIsAlive()) { process.Kill(true); break; }
                await Task.Delay(250, token);
            }
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync();
            readerCancellation.Cancel();
            await readers;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Logging.SaveLog("Psiphon monitor failed", ex); }
        finally
        {
            SetState(plan.ProfileId, "Disconnect", generation);
            readerCancellation.Cancel();
            try { if (!process.HasExited) process.Kill(true); } catch { }
            try { await readers; } catch { }
        }
    }

    public static async Task StopAsync()
    {
        _lifetime?.Cancel();
        try { if (_process is { HasExited: false }) _process.Kill(true); }
        catch (Exception ex) { Logging.SaveLog("Psiphon stop failed", ex); }
        if (_monitor is not null) await _monitor;
        _process?.Dispose(); _process = null;
        if (Utils.IsWindows()) { _job?.Dispose(); _job = null; }
        _lifetime?.Dispose(); _lifetime = null; _monitor = null;
        if (_activeId is { } id)
            RxSchedulers.MainThreadScheduler.Schedule(() =>
            {
                var state = GetState(id);
                state.Color = "#DC2626";
                state.Text = "Disconnect";
            });
        _activeId = null;
    }
}
