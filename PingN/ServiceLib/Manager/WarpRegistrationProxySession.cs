using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using ServiceLib.Handler.Builder;

namespace ServiceLib.Manager;

public sealed record WarpRegistrationProxyChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Owns an isolated HTTP proxy used only by WARP registration subprocesses.</summary>
public sealed class WarpRegistrationProxySession : IAsyncDisposable
{
    private ProcessService? _process;
    private readonly string _directory;
    private readonly ConcurrentQueue<string> _output = new();
    public string ProxyUrl { get; }
    private WarpRegistrationProxySession(string directory, int port)
    {
        _directory = directory;
        ProxyUrl = $"http://127.0.0.1:{port}";
    }

    public static bool IsSupported(ProfileItem profile) =>
        profile.ConfigType is EConfigType.VLESS or EConfigType.VMess or EConfigType.Trojan or EConfigType.Shadowsocks or EConfigType.SOCKS or EConfigType.HTTP
        && string.IsNullOrEmpty(profile.GetProtocolExtra().WarpProfileType)
        && profile.GetProtocolExtra().DnsTunnel is null
        && AppManager.Instance.GetCoreType(profile, profile.ConfigType) is ECoreType.Xray or ECoreType.sing_box;

    public static void ApplyEnvironment(ProcessStartInfo startInfo, string? proxyUrl)
    {
        // Do not inherit an unrelated system proxy when Direct is selected.
        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy" })
            startInfo.Environment[name] = proxyUrl ?? string.Empty;
        startInfo.Environment["NO_PROXY"] = "localhost,127.0.0.1,::1";
        startInfo.Environment["no_proxy"] = "localhost,127.0.0.1,::1";
    }

    public static async Task<WarpRegistrationProxySession?> StartAsync(string? profileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return null;
        var profile = await AppManager.Instance.GetProfileItem(profileId)
            ?? throw new InvalidOperationException("The selected WARP registration proxy was removed.");
        if (!IsSupported(profile)) throw new InvalidOperationException("Select a supported proxy profile for WARP registration.");
        var config = JsonUtils.DeepCopy(AppManager.Instance.Config);
        config.TunModeItem.EnableTun = false;
        config.GuiItem.EnableStatistics = false;
        config.GuiItem.DisplayRealTimeSpeed = false;
        config.CoreBasicItem.LogEnabled = false;
        var built = await CoreConfigContextBuilder.Build(config, profile);
        if (!built.Success) throw new InvalidOperationException("The registration proxy configuration is invalid.");
        var generated = await CoreConfigHandler.GenerateClientConfig(built.Context with { FullConfigTemplate = null, RoutingItem = null }, null);
        if (generated.Success != true) throw new InvalidOperationException(generated.Msg);
        var json = JsonNode.Parse(generated.Data!.ToString()!)!.AsObject();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var singbox = built.Context.RunCoreType == ECoreType.sing_box;
        PrepareConfiguration(json, singbox, port);
        var directory = Path.Combine(Utils.GetConfigPath(), "WarpRegistration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var session = new WarpRegistrationProxySession(directory, port);
        try
        {
            var path = Path.Combine(directory, "proxy.json");
            await File.WriteAllTextAsync(path, json.ToJsonString(), cancellationToken);
            var core = CoreInfoManager.Instance.GetCoreInfo(built.Context.RunCoreType)!;
            var executable = CoreInfoManager.Instance.GetCoreExecFile(core, out var error);
            if (string.IsNullOrEmpty(executable)) throw new FileNotFoundException(error);
            var environment = CreateCoreEnvironment(core);
            session._process = new ProcessService(executable, string.Format(core.Arguments, path.AppendQuotes()), Utils.GetBinConfigPath(), true, false, environment,
                (isError, message) =>
                {
                    session._output.Enqueue(message.Trim());
                    while (session._output.Count > 30) session._output.TryDequeue(out _);
                    Logging.SaveLog($"WARP registration proxy: {message}");
                    NoticeManager.Instance.SendMessage($"[WARP Registration Proxy] {message}");
                    return Task.CompletedTask;
                });
            await session._process.StartAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (session._process.HasExited)
                {
                    await session._process.WaitForExitAsync(timeout.Token);
                    var details = string.Join(Environment.NewLine, session._output);
                    throw new InvalidOperationException($"The WARP registration proxy exited before becoming ready (exit {session._process.ExitCode}). {details}");
                }
                try { using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token); break; }
                catch (SocketException) { await Task.Delay(100, timeout.Token); }
            }
            return session;
        }
        catch { await session.DisposeAsync(); throw; }
    }

    public static Dictionary<string, string> CreateCoreEnvironment(CoreInfo core) =>
        core.Environment.ToDictionary(x => x.Key, x => x.Value ?? string.Empty);

    public static void PrepareConfiguration(JsonObject json, bool singbox, int port)
    {
        json["inbounds"] = JsonNode.Parse(singbox
            ? $$$"""[{"type":"http","tag":"warp-registration","listen":"127.0.0.1","listen_port":{{{port}}}}]"""
            : $$$"""[{"protocol":"http","tag":"warp-registration","listen":"127.0.0.1","port":{{{port}}},"settings":{}}]""");
        // Route every registration request through the chosen profile, independent of app rules.
        var proxyTag = json["outbounds"]![0]!["tag"]!.GetValue<string>();
        json.Remove("api"); json.Remove("stats"); json.Remove("metrics"); json.Remove("policy");
        if (singbox)
        {
            json.Remove("experimental");
            json["route"] = new JsonObject { ["final"] = proxyTag, ["auto_detect_interface"] = true };
        }
        else json["routing"] = new JsonObject { ["rules"] = new JsonArray(new JsonObject
            { ["type"] = "field", ["inboundTag"] = new JsonArray("warp-registration"), ["outboundTag"] = proxyTag }) };
        // Keep startup diagnostics on stdout/stderr, rather than the main core's files.
        json["log"] = singbox
            ? new JsonObject { ["level"] = "info", ["timestamp"] = true }
            : new JsonObject { ["loglevel"] = "info" };
    }

    public async ValueTask DisposeAsync()
    {
        var process = Interlocked.Exchange(ref _process, null);
        try { if (process is not null) await process.StopAsync(); }
        finally
        {
            process?.Dispose();
            try { Directory.Delete(_directory, true); } catch { }
        }
    }
}
