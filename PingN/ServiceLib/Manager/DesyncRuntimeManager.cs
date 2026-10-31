using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServiceLib.Manager;

public sealed record DesyncLaunchPlan(string ExecutablePath, string[] Arguments, string WorkingDirectory, string RuntimeDirectory);

/// <summary>Prepares and owns a per-profile bundled BlackHole-compatible winws2 process.</summary>
public static class DesyncRuntimeManager
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static NamedPipeClientStream? _pipe;
    private static StreamReader? _pipeReader;
    private static StreamWriter? _pipeWriter;
    private static Process? _hostProcess;

    public static async Task<DesyncLaunchPlan?> PrepareAsync(ProfileItem profile, string xrayConfigPath, ECoreType coreType)
    {
        var extra = profile.GetProtocolExtra();
        PingNDesyncSettings? settings = null;
        if (!string.IsNullOrWhiteSpace(extra.PingNDesyncJson))
        {
            try
            {
                settings = JsonSerializer.Deserialize<PingNDesyncSettings>(extra.PingNDesyncJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("The saved PingN Desync settings are not valid JSON.", ex);
            }
        }

        settings ??= new PingNDesyncSettings();
        var profileName = settings.Profile?.Trim() ?? "Off";
        if (profileName.Equals("Off", StringComparison.OrdinalIgnoreCase) || profileName.Length == 0)
        {
            Logging.SaveLog($"Desync: skipped for profile='{profile.Remarks}' because its preset is Off.");
            return null;
        }
        settings = settings with
        {
            Profile = profileName,
            SplitMode = string.IsNullOrWhiteSpace(settings.SplitMode) ? "multisplit" : settings.SplitMode,
            SplitPositions = string.IsNullOrWhiteSpace(settings.SplitPositions) ? "1,midsld" : settings.SplitPositions,
            Fooling = string.IsNullOrWhiteSpace(settings.Fooling) ? "badseq" : settings.Fooling,
            FakeSni = settings.FakeSni ?? string.Empty,
        };
        if (!Utils.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new InvalidOperationException("Desync integration is currently available on Windows x64 only.");
        }
        if (coreType != ECoreType.Xray || profile.ConfigType is EConfigType.WireGuard or EConfigType.MASQUE)
        {
            throw new InvalidOperationException("Desync requires an Xray TCP/TLS profile. WARP WireGuard and MASQUE use UDP/QUIC and cannot use TLS Desync.");
        }

        var toolsDirectory = Path.Combine(Utils.StartupPath(), "Tools", "Desync");
        var executablePath = Path.Combine(toolsDirectory, "winws2.exe");
        var luaLibraryPath = Path.Combine(toolsDirectory, "lua", "engine-lib.lua");
        var luaAntiDpiPath = Path.Combine(toolsDirectory, "lua", "engine-antidpi.lua");
        foreach (var required in new[]
        {
            executablePath,
            Path.Combine(toolsDirectory, "WinDivert.dll"),
            Path.Combine(toolsDirectory, "WinDivert64.sys"),
            Path.Combine(toolsDirectory, "cygwin1.dll"),
            luaLibraryPath,
            luaAntiDpiPath,
        })
        {
            if (!File.Exists(required))
            {
                var relativePath = Path.GetRelativePath(toolsDirectory, required);
                throw new FileNotFoundException($"The bundled Desync runtime is missing '{relativePath}'. Restore the complete Tools/Desync folder.", required);
            }
        }

        var root = JsonNode.Parse(await File.ReadAllTextAsync(xrayConfigPath)) as JsonObject
            ?? throw new InvalidOperationException("The generated Xray config could not be read for Desync.");
        var target = ReadTarget(root);
        var resolvedAddresses = await ResolveAddressesAsync(target.Address);
        var runtimeDirectory = Path.Combine(Utils.GetBinPath(""), "desync-runtime");
        Directory.CreateDirectory(runtimeDirectory);
        var hostListPath = Path.Combine(runtimeDirectory, "active-hosts.txt");
        var ipSetPath = Path.Combine(runtimeDirectory, "active-ips.txt");
        var debugPath = Path.Combine(runtimeDirectory, "winws2.log");
        var hosts = new[] { target.ServerName, target.HostHeader }
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (hosts.Length == 0)
        {
            throw new InvalidOperationException("The Xray profile has no TLS server name or host to scope Desync to.");
        }
        await File.WriteAllLinesAsync(hostListPath, hosts, new UTF8Encoding(false));
        await File.WriteAllLinesAsync(ipSetPath, resolvedAddresses, new UTF8Encoding(false));

        var values = settings;
        var args = BuildArguments(values, target.Port, hostListPath, ipSetPath, luaLibraryPath, luaAntiDpiPath, debugPath);
        Logging.SaveLog($"Desync: prepared profile='{profile.Remarks}', preset='{profileName}', server={FormatEndpoint(target.Address, target.Port)}, SNI scope=[{string.Join(", ", hosts)}], resolved IPs={resolvedAddresses.Length}, runtime='{executablePath}', debug log='{debugPath}'.");
        return new DesyncLaunchPlan(executablePath, args, toolsDirectory, runtimeDirectory);
    }

    public static async Task StartAsync(DesyncLaunchPlan? plan)
    {
        if (plan is null) return;
        await Gate.WaitAsync();
        try
        {
            await StopCoreAsync();
            var hostScript = Path.Combine(Utils.StartupPath(), "Tools", "Desync", "desync-host.ps1");
            if (!File.Exists(hostScript))
            {
                throw new FileNotFoundException("The PingN Desync elevation helper is missing.", hostScript);
            }

            var pipeName = "PingNDesync-" + Guid.NewGuid().ToString("N");
            var requestPath = Path.Combine(plan.RuntimeDirectory, "launch-request.json");
            var request = new DesyncHostRequest
            {
                PipeName = pipeName,
                ExecutablePath = plan.ExecutablePath,
                WorkingDirectory = plan.WorkingDirectory,
                Arguments = string.Join(" ", plan.Arguments.Select(QuoteWindowsArgument)),
                StdoutPath = Path.Combine(plan.RuntimeDirectory, "winws2-stdout.log"),
                StderrPath = Path.Combine(plan.RuntimeDirectory, "winws2-stderr.log"),
            };
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request), new UTF8Encoding(false));
            Logging.SaveLog($"Desync: requesting elevated winws2 start; args={string.Join(" ", plan.Arguments)}; runtime='{plan.RuntimeDirectory}'. Windows may show a UAC prompt.");

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
                ?? throw new InvalidOperationException("Could not start the elevated Desync host.");
            _hostProcess = host;
            host.EnableRaisingEvents = true;
            host.Exited += (_, _) =>
            {
                try { Logging.SaveLog($"Desync: elevated host process exited; inspect winws2.log and captured stdout/stderr under '{plan.RuntimeDirectory}'."); }
                catch (Exception ex) { Logging.SaveLog("Desync: could not record elevated host exit", ex); }
            };

            _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await _pipe.ConnectAsync(connectTimeout.Token);
            _pipeReader = new StreamReader(_pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            _pipeWriter = new StreamWriter(_pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            var message = await _pipeReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (message != "READY")
            {
                throw new InvalidOperationException($"The Desync engine failed to start. {message}");
            }
            Logging.SaveLog("Desync: winws2 reported READY; packet interception host is active.");
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Desync: start failed", ex);
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
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task StopCoreAsync()
    {
        try
        {
            if (_pipeWriter is not null && _pipe?.IsConnected == true)
            {
                await _pipeWriter.WriteLineAsync("STOP");
                var response = await _pipeReader!.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Logging.SaveLog($"Desync: stop response='{response ?? "pipe closed"}'.");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Desync: stop/pipe cleanup failed", ex);
        }
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
                Logging.SaveLog($"Desync: elevated host shutdown completed with code {host.ExitCode}; runtime logs are in '{Path.Combine(Utils.GetBinPath(""), "desync-runtime")}'.");
                LogCapturedOutput("Desync winws2 stdout", Path.Combine(Utils.GetBinPath(""), "desync-runtime", "winws2-stdout.log"));
                LogCapturedOutput("Desync winws2 stderr", Path.Combine(Utils.GetBinPath(""), "desync-runtime", "winws2-stderr.log"));
                LogCapturedOutput("Desync engine debug", Path.Combine(Utils.GetBinPath(""), "desync-runtime", "winws2.log"));
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("DesyncRuntime", ex);
        }
        finally
        {
            host.Dispose();
        }
    }

    private static string[] BuildArguments(PingNDesyncSettings settings, int port, string hostList, string ipSet, string luaLibrary, string luaAntiDpi, string debugPath)
    {
        var splitMode = settings.SplitMode is "multisplit" or "multidisorder" ? settings.SplitMode : throw new InvalidOperationException("Desync split mode must be multisplit or multidisorder.");
        if (!Regex.IsMatch(settings.SplitPositions, "^[a-zA-Z0-9,+-]+$")) throw new InvalidOperationException("Desync split positions contain unsupported characters.");
        var fooling = settings.Fooling is "none" or "badseq" or "badsum" or "md5sig" or "ttl" ? settings.Fooling : throw new InvalidOperationException("Desync fake packet mode is invalid.");
        if (fooling == "badseq" && settings.BadSequence is >= 0 or < -2000000000) throw new InvalidOperationException("Desync bad sequence must be a negative integer.");
        if (fooling != "none" && settings.Repeats is < 1 or > 10) throw new InvalidOperationException("Desync repeats must be between 1 and 10.");
        if (fooling == "ttl" && (settings.AutoTtlDelta is < 1 or > 10 || settings.AutoTtlMin is < 1 or > 255 || settings.AutoTtlMax is < 1 or > 255 || settings.AutoTtlMin > settings.AutoTtlMax))
            throw new InvalidOperationException("Desync AutoTTL settings are invalid.");
        var fakeSni = settings.FakeSni.Trim();
        if (fakeSni.Length > 0 && !IsValidHostname(fakeSni)) throw new InvalidOperationException("Desync fake SNI must be a valid hostname.");

        var args = new List<string>
        {
            $"--debug=@{debugPath}",
            $"--wf-tcp-out={port}",
            $"--lua-init=@{luaLibrary}",
            $"--lua-init=@{luaAntiDpi}",
            $"--filter-tcp={port}",
            "--filter-l7=tls",
            $"--hostlist={hostList}",
            $"--ipset={ipSet}",
            "--out-range=-d10",
            "--payload=tls_client_hello",
        };
        var tlsMod = fakeSni.Length == 0 ? "rnd,rndsni,dupsid" : $"rnd,sni={fakeSni},dupsid";
        if (fooling == "none")
        {
            args.Add($"--lua-desync={splitMode}:pos={settings.SplitPositions}");
            return [.. args];
        }
        if (fooling == "ttl")
        {
            args.Insert(1, $"--wf-tcp-in={port}");
            args.Add($"--lua-desync=fake:blob=fake_default_tls:ip_autottl=-{settings.AutoTtlDelta},{settings.AutoTtlMin}-{settings.AutoTtlMax}:ip6_autottl=-{settings.AutoTtlDelta},{settings.AutoTtlMin}-{settings.AutoTtlMax}:tls_mod={tlsMod}:repeats={settings.Repeats}");
        }
        else
        {
            var foolingArg = fooling switch
            {
                "badseq" => $"tcp_seq={settings.BadSequence}",
                "badsum" => "badsum",
                "md5sig" => "tcp_md5",
                _ => throw new InvalidOperationException("Unsupported Desync fake packet mode."),
            };
            args.Add($"--lua-desync=fake:blob=fake_default_tls:{foolingArg}:tls_mod={tlsMod}:repeats={settings.Repeats}");
        }
        args.Add($"--lua-desync={splitMode}:pos={settings.SplitPositions}");
        return [.. args];
    }

    private static XrayTarget ReadTarget(JsonObject root)
    {
        if (root["outbounds"] is not JsonArray outbounds) throw new InvalidOperationException("Xray config has no outbound list for Desync.");
        var candidates = outbounds.OfType<JsonObject>().ToArray();
        var outbound = candidates.FirstOrDefault(item => item["tag"]?.GetValue<string>() == Global.ProxyTag
                && FindPrimaryServer(item["settings"] as JsonObject) is not null)
            ?? candidates.FirstOrDefault(item => FindPrimaryServer(item["settings"] as JsonObject) is not null);
        if (outbound is null) throw new InvalidOperationException($"Xray config has no outbound with a valid server endpoint to scope Desync to. Outbounds: {DescribeOutbounds(candidates)}");
        var settings = outbound["settings"] as JsonObject;
        var server = FindPrimaryServer(settings);
        var address = server?["address"]?.GetValue<string>() ?? string.Empty;
        var port = server?["port"]?.GetValue<int>() ?? 0;
        if (address.Length == 0 || port is < 1 or > 65535)
            throw new InvalidOperationException($"Could not identify an Xray TCP server address and port for Desync. Outbounds: {DescribeOutbounds(candidates)}");

        var stream = outbound["streamSettings"] as JsonObject;
        var security = stream?["security"]?.GetValue<string>() ?? string.Empty;
        if (!security.Equals("tls", StringComparison.OrdinalIgnoreCase) && !security.Equals("reality", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Desync requires a TLS or Reality Xray outbound.");
        var network = stream?["network"]?.GetValue<string>() ?? "raw";
        if (network is not ("raw" or "tcp" or "ws" or "httpupgrade" or "grpc" or "xhttp" or "splithttp"))
            throw new InvalidOperationException($"Desync does not support the Xray '{network}' transport. Use a TCP-based transport.");
        var serverName = (security.Equals("reality", StringComparison.OrdinalIgnoreCase)
                ? stream?["realitySettings"]?["serverName"]?.GetValue<string>()
                : stream?["tlsSettings"]?["serverName"]?.GetValue<string>())
            ?? (IPAddress.TryParse(address, out _) ? string.Empty : address);
        var host = network switch
        {
            "ws" => stream?["wsSettings"]?["headers"]?["Host"]?.GetValue<string>(),
            "httpupgrade" => stream?["httpupgradeSettings"]?["host"]?.GetValue<string>(),
            "xhttp" or "splithttp" => stream?["xhttpSettings"]?["host"]?.GetValue<string>(),
            _ => null,
        };
        if (serverName.Length > 0 && !IsValidHostname(serverName)) serverName = string.Empty;
        if (host is not null && !IsValidHostname(host)) host = null;
        return new XrayTarget(address, port, serverName, host);
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

    private static string DescribeOutbounds(IEnumerable<JsonObject> outbounds) => string.Join("; ", outbounds.Select(item =>
    {
        var tag = item["tag"]?.GetValue<string>() ?? "(untagged)";
        var protocol = item["protocol"]?.GetValue<string>() ?? "(unknown)";
        var keys = item["settings"] is JsonObject settings ? string.Join(",", settings.Select(pair => pair.Key)) : "none";
        return $"tag={tag}, protocol={protocol}, settings=[{keys}]";
    }));

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
        catch (Exception ex) { Logging.SaveLog($"Desync: could not read captured output '{path}'", ex); }
    }

    private static async Task<string[]> ResolveAddressesAsync(string address)
    {
        if (IPAddress.TryParse(address, out var literal)) return [literal.ToString()];
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(address);
            if (addresses.Length == 0) throw new InvalidOperationException();
            return addresses.Select(ip => ip.ToString()).Distinct(StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Could not resolve the Xray server '{address}' for Desync.", ex);
        }
    }

    private static bool IsValidHostname(string hostname) =>
        hostname.Length <= 253 && Regex.IsMatch(hostname, "^(?=.{1,253}$)([a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\\.)+[a-zA-Z]{2,63}$");

    private static string QuoteWindowsArgument(string value)
    {
        if (value.Length > 0 && !value.Any(char.IsWhiteSpace) && !value.Contains('"')) return value;
        var result = new System.Text.StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }
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

    private sealed record XrayTarget(string Address, int Port, string ServerName, string? HostHeader);
    private sealed record DesyncHostRequest
    {
        public required string PipeName { get; init; }
        public required string ExecutablePath { get; init; }
        public required string WorkingDirectory { get; init; }
        public required string Arguments { get; init; }
        public required string StdoutPath { get; init; }
        public required string StderrPath { get; init; }
    }
}
