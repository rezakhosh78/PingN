using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ServiceLib.Manager;

public sealed record SniSpoofingPlusLaunchPlan(
    string ExecutablePath,
    string[] Arguments,
    string WorkingDirectory,
    string RuntimeDirectory);

/// <summary>Connects one Xray profile to its own SNI Spoofing Plus TCP listener.</summary>
public static class SniSpoofingPlusRuntimeManager
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static NamedPipeClientStream? _pipe;
    private static StreamReader? _pipeReader;
    private static StreamWriter? _pipeWriter;
    private static Process? _hostProcess;

    public static async Task<SniSpoofingPlusLaunchPlan?> PrepareAsync(ProfileItem profile, string xrayConfigPath, ECoreType coreType)
    {
        var extra = profile.GetProtocolExtra();
        if (extra.SniSpoofingPlusEnabled != true)
        {
            Logging.SaveLog($"SNI Spoofing Plus: skipped for profile='{profile.Remarks}' because it is disabled.");
            return null;
        }
        if (!Utils.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new InvalidOperationException("SNI Spoofing Plus is currently available on Windows x64 only.");
        if (coreType != ECoreType.Xray || profile.ConfigType is EConfigType.WireGuard or EConfigType.MASQUE or EConfigType.Outbound or EConfigType.PolicyGroup or EConfigType.ProxyChain)
            throw new InvalidOperationException("SNI Spoofing Plus requires an individual Xray TCP/TLS profile. WARP WireGuard and MASQUE are not supported.");

        if (!PingNSniSpoofingSettings.TryNormalizeFakeSni(extra.SniSpoofingPlusFakeSni, out var fakeSni, out var fakeSniError))
            throw new InvalidOperationException(fakeSniError);
        var utls = NormalizeUtls(extra.SniSpoofingPlusUtls);
        var options = JsonUtils.Deserialize<PingNSniSpoofingSettings>(extra.PingNSniSpoofingJson) ?? new();
        if (!options.TryValidate(out var optionError)) throw new InvalidOperationException(optionError);
        if (!PingNSniSpoofingSettings.TryNormalizeConnect(options.Connect, out var configuredEndpoints, out var connectError))
            throw new InvalidOperationException(connectError);
        var toolsDirectory = Path.Combine(Utils.StartupPath(), "Tools", "SniSpoofingPlus");
        var executablePath = Path.Combine(toolsDirectory, "sni-spoofing-plus-windows-amd64.exe");
        if (!File.Exists(executablePath)) throw new FileNotFoundException("SNI Spoofing Plus CLI is missing from the PingN tools folder.", executablePath);
        foreach (var required in new[] { "WinDivert.dll", "WinDivert64.sys" })
        {
            var path = Path.Combine(toolsDirectory, required);
            if (!File.Exists(path))
                throw new FileNotFoundException($"SNI Spoofing Plus needs {required} from a WinDivert distribution you are authorized to use. Place it beside the CLI in Tools/SniSpoofingPlus.", path);
        }

        var root = JsonNode.Parse(await File.ReadAllTextAsync(xrayConfigPath)) as JsonObject
            ?? throw new InvalidOperationException("Could not read the generated Xray config for SNI Spoofing Plus.");
        var outbounds = root["outbounds"] as JsonArray;
        var outbound = FindPrimaryOutbound(outbounds)
            ?? throw new InvalidOperationException($"The selected Xray profile has no usable primary outbound. Outbounds: {DescribeOutbounds(outbounds)}");
        var stream = outbound["streamSettings"] as JsonObject
            ?? throw new InvalidOperationException("SNI Spoofing Plus requires a TCP-based TLS Xray outbound.");
        var security = stream["security"]?.GetValue<string>() ?? string.Empty;
        if (!security.Equals("tls", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SNI Spoofing Plus supports standard TLS profiles. Reality and non-TLS profiles cannot use this local injector.");
        var network = stream["network"]?.GetValue<string>() ?? "tcp";
        if (network is not ("raw" or "tcp" or "ws" or "httpupgrade" or "grpc" or "xhttp" or "splithttp"))
            throw new InvalidOperationException($"SNI Spoofing Plus does not support the Xray '{network}' transport.");

        var server = FindPrimaryServer(outbound["settings"] as JsonObject)
            ?? throw new InvalidOperationException($"Could not identify the Xray server address and port for SNI Spoofing Plus. Outbound tag='{outbound["tag"]?.GetValue<string>() ?? "(untagged)"}', protocol='{outbound["protocol"]?.GetValue<string>() ?? "(unknown)"}', settings=[{DescribeSettings(outbound["settings"] as JsonObject)}].");
        var address = server["address"]?.GetValue<string>()?.Trim() ?? string.Empty;
        var port = server["port"]?.GetValue<int>() ?? 0;
        if (address.Length == 0 || port is < 1 or > 65535)
            throw new InvalidOperationException("The Xray server address or port is invalid for SNI Spoofing Plus.");
        if (IPAddress.TryParse(address, out _) && fakeSni.Length == 0)
            throw new InvalidOperationException("Enter at least one Fake SNI name when the Xray server address is an IP address.");

        var listenPort = GetFreePort();
        var connectAddress = address.Contains(':') && !address.StartsWith("[", StringComparison.Ordinal)
            ? $"[{address}]:{port}"
            : $"{address}:{port}";
        if (configuredEndpoints.Length > 0) connectAddress = configuredEndpoints;
        server["address"] = "127.0.0.1";
        server["port"] = listenPort;
        await File.WriteAllTextAsync(xrayConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var arguments = new List<string>
        {
            "-listen", $"127.0.0.1:{listenPort}",
            "-connect", connectAddress,
            "-utls", utls,
            "-route-mode", options.RouteMode,
            "-ip-routing", options.IpRouting,
            "-sni-routing", options.SniRouting,
            "-injector", options.Injector,
            "-fake-repeat", options.FakeRepeat,
            "-fake-delay", options.FakeDelay,
            "-ack-timeout", options.AckTimeout,
            $"-enable-fragment={options.EnableFragment.ToString().ToLowerInvariant()}",
            "-fragment-delay", options.FragmentDelay,
            "-sni-chunk", options.SniChunk,
        };
        if (fakeSni.Length > 0)
        {
            arguments.Add("-fake-sni");
            arguments.Add(fakeSni);
        }

        var runtimeDirectory = Path.Combine(Utils.GetBinPath(""), "sni-spoofing-plus-runtime");
        Directory.CreateDirectory(runtimeDirectory);
        Logging.SaveLog($"SNI Spoofing Plus: prepared profile='{profile.Remarks}', server={FormatEndpoint(address, port)}, connect=[{connectAddress}], local listener=127.0.0.1:{listenPort}, transport='{network}', uTLS='{utls}', fake SNI=[{fakeSni}], repeat={options.FakeRepeat}, delay={options.FakeDelay}, ack timeout={options.AckTimeout}, injector={options.Injector}, fragment={options.EnableFragment}, fragment delay={options.FragmentDelay}, SNI chunk={options.SniChunk}, runtime='{executablePath}'.");
        return new SniSpoofingPlusLaunchPlan(executablePath, [.. arguments], toolsDirectory, runtimeDirectory);
    }

    public static async Task StartAsync(SniSpoofingPlusLaunchPlan? plan)
    {
        if (plan is null) return;
        await Gate.WaitAsync();
        try
        {
            await StopCoreAsync();
            var hostScript = Path.Combine(Utils.StartupPath(), "Tools", "SniSpoofingPlus", "sni-host.ps1");
            if (!File.Exists(hostScript)) throw new FileNotFoundException("The PingN SNI Spoofing Plus elevation helper is missing.", hostScript);

            var pipeName = "PingNSni-" + Guid.NewGuid().ToString("N");
            var requestPath = Path.Combine(plan.RuntimeDirectory, "launch-request.json");
            var request = new SniHostRequest
            {
                PipeName = pipeName,
                ExecutablePath = plan.ExecutablePath,
                WorkingDirectory = plan.WorkingDirectory,
                Arguments = string.Join(" ", plan.Arguments.Select(QuoteWindowsArgument)),
                StdoutPath = Path.Combine(plan.RuntimeDirectory, "sni-stdout.log"),
                StderrPath = Path.Combine(plan.RuntimeDirectory, "sni-stderr.log"),
            };
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request), new UTF8Encoding(false));
            Logging.SaveLog($"SNI Spoofing Plus: requesting elevated CLI start; args={string.Join(" ", plan.Arguments)}; runtime='{plan.RuntimeDirectory}'. Windows may show a UAC prompt.");

            var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var powershellPath = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                Arguments = string.Join(" ", new[]
                {
                    "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden",
                    "-File", QuoteWindowsArgument(hostScript), "-RequestPath", QuoteWindowsArgument(requestPath),
                }),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var host = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the elevated SNI Spoofing Plus host.");
            _hostProcess = host;
            host.EnableRaisingEvents = true;
            host.Exited += (_, _) =>
            {
                try { Logging.SaveLog($"SNI Spoofing Plus: elevated host process exited; inspect captured stdout/stderr under '{plan.RuntimeDirectory}'."); }
                catch (Exception ex) { Logging.SaveLog("SNI Spoofing Plus: could not record elevated host exit", ex); }
            };

            _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await _pipe.ConnectAsync(connectTimeout.Token);
            _pipeReader = new StreamReader(_pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            _pipeWriter = new StreamWriter(_pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            var message = await _pipeReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (message != "READY") throw new InvalidOperationException($"SNI Spoofing Plus failed to start. {message}");
            Logging.SaveLog("SNI Spoofing Plus: CLI reported READY; local TLS interception listener is active.");
        }
        catch (Exception ex)
        {
            Logging.SaveLog("SNI Spoofing Plus: start failed", ex);
            await StopCoreAsync();
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task StopAsync()
    {
        await Gate.WaitAsync();
        try { await StopCoreAsync(); }
        finally { Gate.Release(); }
    }

    private static async Task StopCoreAsync()
    {
        try
        {
            if (_pipeWriter is not null && _pipe?.IsConnected == true)
            {
                await _pipeWriter.WriteLineAsync("STOP");
                var response = await _pipeReader!.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Logging.SaveLog($"SNI Spoofing Plus: stop response='{response ?? "pipe closed"}'.");
            }
        }
        catch (Exception ex) { Logging.SaveLog("SNI Spoofing Plus: stop/pipe cleanup failed", ex); }
        finally
        {
            _pipeWriter?.Dispose();
            _pipeReader?.Dispose();
            _pipe?.Dispose();
            _pipeWriter = null;
            _pipeReader = null;
            _pipe = null;
        }

        var host = _hostProcess;
        _hostProcess = null;
        if (host is null) return;
        try
        {
            if (!host.HasExited) await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (host.HasExited)
            {
                Logging.SaveLog($"SNI Spoofing Plus: elevated host shutdown completed with code {host.ExitCode}; runtime logs are in '{Path.Combine(Utils.GetBinPath(""), "sni-spoofing-plus-runtime")}'.");
                LogCapturedOutput("SNI Spoofing Plus CLI stdout", Path.Combine(Utils.GetBinPath(""), "sni-spoofing-plus-runtime", "sni-stdout.log"));
                LogCapturedOutput("SNI Spoofing Plus CLI stderr", Path.Combine(Utils.GetBinPath(""), "sni-spoofing-plus-runtime", "sni-stderr.log"));
            }
        }
        catch (Exception ex) { Logging.SaveLog("SNI Spoofing Plus: host shutdown failed", ex); }
        finally { host.Dispose(); }
    }

    private static JsonObject? FindPrimaryOutbound(JsonArray? outbounds)
    {
        if (outbounds is null) return null;
        var candidates = outbounds.OfType<JsonObject>().ToArray();
        var preferred = candidates.FirstOrDefault(item => item["tag"]?.GetValue<string>() == Global.ProxyTag
            && FindPrimaryServer(item["settings"] as JsonObject) is not null);
        return preferred ?? candidates.FirstOrDefault(item =>
        {
            var protocol = item["protocol"]?.GetValue<string>() ?? string.Empty;
            return protocol is not ("freedom" or "blackhole" or "dns") && FindPrimaryServer(item["settings"] as JsonObject) is not null;
        });
    }

    private static JsonObject? FindPrimaryServer(JsonObject? settings)
    {
        if (settings is null) return null;
        var server = FindFirstValidServer(settings["vnext"] as JsonArray)
            ?? FindFirstValidServer(settings["servers"] as JsonArray);
        if (server is not null) return server;
        return HasValidEndpoint(settings) ? settings : null;
    }

    private static JsonObject? FindFirstValidServer(JsonArray? list) =>
        list?.OfType<JsonObject>().FirstOrDefault(HasValidEndpoint);

    private static bool HasValidEndpoint(JsonObject? server)
    {
        if (server is null) return false;
        var address = server["address"]?.GetValue<string>();
        var port = server["port"]?.GetValue<int>() ?? 0;
        return !string.IsNullOrWhiteSpace(address) && port is >= 1 and <= 65535;
    }

    private static string DescribeOutbounds(JsonArray? outbounds) => outbounds is null
        ? "(missing)"
        : string.Join("; ", outbounds.OfType<JsonObject>().Select(item =>
        {
            var tag = item["tag"]?.GetValue<string>() ?? "(untagged)";
            var protocol = item["protocol"]?.GetValue<string>() ?? "(unknown)";
            return $"tag={tag}, protocol={protocol}, settings=[{DescribeSettings(item["settings"] as JsonObject)}]";
        }));

    private static string DescribeSettings(JsonObject? settings) =>
        settings is null ? "none" : string.Join(",", settings.Select(pair => pair.Key));

    private static string FormatEndpoint(string address, int port) =>
        address.Contains(':') && !address.StartsWith("[", StringComparison.Ordinal) ? $"[{address}]:{port}" : $"{address}:{port}";

    private static void LogCapturedOutput(string label, string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var truncated = stream.Length > 6000;
            if (truncated) stream.Seek(-6000, SeekOrigin.End);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            if (truncated) _ = reader.ReadLine();
            var content = reader.ReadToEnd().Trim();
            if (content.Length > 0) Logging.SaveLog($"{label} ({path}):{Environment.NewLine}{content}");
        }
        catch (Exception ex) { Logging.SaveLog("SNI Spoofing Plus: could not read captured output", ex); }
    }

    private static string NormalizeUtls(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "firefox" => "firefox",
            "chrome" => "chrome",
            "edge" => "edge",
            "safari" => "safari",
            "ios" => "ios",
            "qq" => "qq",
            "360browser" => "360browser",
            "rotate" => "rotate",
            "none" => "none",
            _ => "rotate",
        };
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string QuoteWindowsArgument(string value)
    {
        if (value.Length > 0 && !value.Any(char.IsWhiteSpace) && !value.Contains('"')) return value;
        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { backslashes++; continue; }
            if (character == '"')
            {
                result.Append('\\', (backslashes * 2) + 1).Append('"');
                backslashes = 0;
                continue;
            }
            result.Append('\\', backslashes).Append(character);
            backslashes = 0;
        }
        result.Append('\\', backslashes * 2).Append('"');
        return result.ToString();
    }

    private sealed record SniHostRequest
    {
        public required string PipeName { get; init; }
        public required string ExecutablePath { get; init; }
        public required string WorkingDirectory { get; init; }
        public required string Arguments { get; init; }
        public required string StdoutPath { get; init; }
        public required string StderrPath { get; init; }
    }
}
