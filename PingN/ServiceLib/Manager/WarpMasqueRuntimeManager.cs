using System.IO.Compression;
using System.Net;

namespace ServiceLib.Manager;

public sealed record WarpMasqueLaunchPlan(ProfileItem Profile, string RuntimeDirectory);
public sealed record WarpMasqueEndpointScanResult(string Endpoint);

/// <summary>Runs the usque MASQUE/H2 core as a local SOCKS5 upstream, following PingNG's runtime model.</summary>
public static class WarpMasqueRuntimeManager
{
    public const string WarpProfileType = "masque-h2";
    public const string Description = "WARP MASQUE/H2";
    public const string DefaultEndpoint = "162.159.198.238";
    public const int DefaultEndpointPort = 443;
    public const int DefaultLocalPort = 20808;
    public const string DefaultCandidates = "162.159.198.0/24:443,162.159.199.0/24:443";
    public const string DefaultSni = "cloudflare.com";
    public const string DefaultDns = "1.1.1.1,1.0.0.1";

    private const string CoreVersion = "4.2.1";
    private static readonly TimeSpan RegisterTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(25);
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };
    private static Process? _process;
    private static string? _activeConfigPath;

    public static ProfileItem CreateProfile(string subId)
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.SOCKS,
            CoreType = ECoreType.Xray,
            IsSub = false,
            Subid = subId,
            Remarks = Description,
            Address = Global.Loopback,
            Port = GetAvailablePort(DefaultLocalPort),
            Network = string.Empty,
        };
        item.SetProtocolExtra(new ProtocolExtraItem
        {
            WarpProfileType = WarpProfileType,
            WarpMasqueLocalPortVersion = 1,
            WarpMasqueEndpoint = DefaultEndpoint,
            WarpMasqueEndpointPort = DefaultEndpointPort,
            WarpMasqueEndpointCandidates = DefaultCandidates,
            WarpMasqueAutoScan = true,
            WarpMasqueDeviceName = $"PingN-{Guid.NewGuid():N}"[..20],
            WarpMasqueSni = DefaultSni,
            WarpMasqueDns = DefaultDns,
            WarpMasqueHttp2Enabled = true,
        });
        return item;
    }

    /// <summary>Moves profiles created before the MASQUE local listener used a dedicated port.</summary>
    public static bool NormalizeLocalPort(ProfileItem profile)
    {
        var extra = profile.GetProtocolExtra();
        if (!string.Equals(extra.WarpProfileType, WarpProfileType, StringComparison.OrdinalIgnoreCase)
            || extra.WarpMasqueLocalPortVersion is >= 1)
            return false;

        profile.Port = GetAvailablePort(DefaultLocalPort);
        profile.SetProtocolExtra(extra with { WarpMasqueLocalPortVersion = 1 });
        return true;
    }

    public static async Task<WarpMasqueEndpointScanResult> ScanFastestEndpointAsync(
        string candidates, CancellationToken cancellationToken = default)
    {
        var endpoints = ParseCandidates(candidates);
        if (endpoints.Count == 0)
            throw new InvalidDataException("Enter at least one WARP MASQUE endpoint or IPv4 CIDR range.");

        using var concurrency = new SemaphoreSlim(64);
        var results = new ConcurrentBag<(Endpoint Endpoint, long Milliseconds)>();
        var scans = endpoints.Select(async endpoint =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(1300));
                using var client = new TcpClient(endpoint.Host.Contains(':') ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);
                var started = Stopwatch.GetTimestamp();
                await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token);
                results.Add((endpoint, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException or ArgumentException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;
            }
            finally
            {
                concurrency.Release();
            }
        });
        await Task.WhenAll(scans);
        var fastest = results.OrderBy(result => result.Milliseconds).FirstOrDefault();
        if (fastest.Endpoint is null)
            throw new InvalidOperationException("No candidate endpoint accepted a TCP connection on its configured port.");
        return new WarpMasqueEndpointScanResult(fastest.Endpoint.Rendered);
    }

    public static string[] SplitEndpoint(string value, int fallbackPort = DefaultEndpointPort)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.StartsWith('['))
        {
            var close = trimmed.IndexOf(']');
            if (close <= 1)
                throw new InvalidDataException("The scanned IPv6 endpoint is invalid.");
            var portText = trimmed[(close + 1)..].TrimStart(':');
            var port = portText.Length == 0 ? fallbackPort : int.TryParse(portText, out var parsed) ? parsed : -1;
            if (port is < 1 or > 65535)
                throw new InvalidDataException("The scanned endpoint port is invalid.");
            return [trimmed[1..close], port.ToString()];
        }
        if (trimmed.Count(character => character == ':') > 1)
            return [trimmed, fallbackPort.ToString()];
        var separator = trimmed.LastIndexOf(':');
        if (separator > 0 && int.TryParse(trimmed[(separator + 1)..], out var parsedPort))
        {
            if (parsedPort is < 1 or > 65535)
                throw new InvalidDataException("The scanned endpoint port is invalid.");
            return [trimmed[..separator], parsedPort.ToString()];
        }
        return [trimmed, fallbackPort.ToString()];
    }

    public static Task<WarpMasqueLaunchPlan?> PrepareAsync(ProfileItem profile)
    {
        var extra = profile.GetProtocolExtra();
        if (!string.Equals(extra.WarpProfileType, WarpProfileType, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<WarpMasqueLaunchPlan?>(null);
        EnsureSupportedPlatform();
        if (profile.ConfigType != EConfigType.SOCKS
            || !string.Equals(profile.Address, Global.Loopback, StringComparison.OrdinalIgnoreCase)
            || profile.Port is < 1 or > 65535)
            throw new InvalidOperationException("WARP MASQUE/H2 must use its saved local SOCKS5 address and port.");

        _ = ParseEndpoint(extra.WarpMasqueEndpoint, extra.WarpMasqueEndpointPort ?? DefaultEndpointPort);
        if (string.IsNullOrWhiteSpace(extra.WarpMasqueDeviceName))
            throw new InvalidOperationException("Set a WARP MASQUE device name before connecting.");
        if (string.IsNullOrWhiteSpace(extra.WarpMasqueSni))
            throw new InvalidOperationException("Set an SNI host before connecting.");

        var safeId = Regex.Replace(profile.IndexId ?? string.Empty, @"[^A-Za-z0-9_-]", string.Empty);
        if (safeId.Length == 0)
            throw new InvalidOperationException("The WARP MASQUE profile has not been saved yet.");
        var runtimeDirectory = Utils.GetConfigPath(Path.Combine("WarpMasque", safeId));
        return Task.FromResult<WarpMasqueLaunchPlan?>(new WarpMasqueLaunchPlan(profile, runtimeDirectory));
    }

    public static async Task StartAsync(WarpMasqueLaunchPlan? plan, Func<bool, string, Task>? updateFunc, CancellationToken cancellationToken = default)
    {
        if (plan is null)
            return;
        await StopAsync();
        var profile = plan.Profile;
        var extra = profile.GetProtocolExtra();
        Directory.CreateDirectory(plan.RuntimeDirectory);
        var executablePath = await ResolveExecutablePathAsync().WaitAsync(cancellationToken);
        var configPath = Path.Combine(plan.RuntimeDirectory, "config.json");
        var configJson = await GetRegisteredConfigAsync(executablePath, plan.RuntimeDirectory, configPath, profile, updateFunc, cancellationToken: cancellationToken);

        var candidates = new List<Endpoint>();
        if (extra.WarpMasqueAutoScan == true)
        {
            await Report(updateFunc, false, "WARP MASQUE/H2: scanning candidate endpoints...");
            try
            {
                candidates.AddRange(await ScanEndpointsAsync(extra.WarpMasqueEndpointCandidates ?? DefaultCandidates).WaitAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logging.SaveLog("WARP MASQUE/H2 endpoint scan failed; using the saved endpoint", ex);
            }
        }
        var primary = ParseEndpoint(extra.WarpMasqueEndpoint, extra.WarpMasqueEndpointPort ?? DefaultEndpointPort);
        if (candidates.Count == 0)
            candidates.Add(primary);
        var connectCandidates = candidates.Distinct().Take(8).ToList();
        if (!connectCandidates.Contains(primary))
            connectCandidates.Add(primary);

        var lastError = string.Empty;
        foreach (var endpoint in connectCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var endpointConfig = PatchEndpoint(configJson, endpoint);
                await File.WriteAllTextAsync(configPath, endpointConfig, new UTF8Encoding(false));
                await Report(updateFunc, false, $"WARP MASQUE/H2: connecting to {endpoint.Rendered}...");
                cancellationToken.ThrowIfCancellationRequested();
                var process = StartCore(executablePath, configPath, plan.RuntimeDirectory, profile, endpoint);
                _process = process;
                using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
                if (await WaitForSocksTunnelAsync(process, profile.Port, ConnectTimeout).WaitAsync(cancellationToken))
                {
                    profile.SetProtocolExtra(profile.GetProtocolExtra() with
                    {
                        WarpMasqueEndpoint = endpoint.Host,
                        WarpMasqueEndpointPort = endpoint.Port,
                        WarpMasqueConfigJson = endpointConfig,
                    });
                    var stored = await AppManager.Instance.GetProfileItem(profile.IndexId);
                    if (stored is not null)
                    {
                        stored.SetProtocolExtra(profile.GetProtocolExtra());
                        await ConfigHandler.AddSocksServer(AppManager.Instance.Config, stored);
                        BuiltinWarpManager.RegistrationUpdated.Publish();
                    }
                    _activeConfigPath = configPath;
                    await Report(updateFunc, false, $"WARP MASQUE/H2: connected through {endpoint.Rendered}.");
                    return;
                }
                lastError = await ReadProcessError(process);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await StopProcessAsync();
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Logging.SaveLog($"WARP MASQUE/H2 endpoint {endpoint.Rendered} failed", ex);
            }
            await StopProcessAsync();
        }

        await StopAsync();
        throw new InvalidOperationException("WARP MASQUE/H2 could not establish an HTTP/2 tunnel" +
            (string.IsNullOrWhiteSpace(lastError) ? "." : $": {lastError}"));
    }

    public static async Task StopAsync()
    {
        await StopProcessAsync();
        var configPath = _activeConfigPath;
        _activeConfigPath = null;
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            try { File.Delete(configPath); }
            catch (Exception ex) { Logging.SaveLog("WARP MASQUE/H2 runtime config cleanup failed", ex); }
        }
    }

    public static void DeleteMaterializedConfig(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return;
        var safeId = Regex.Replace(profileId, @"[^A-Za-z0-9_-]", string.Empty);
        if (safeId.Length == 0) return;
        try { Directory.Delete(Utils.GetConfigPath(Path.Combine("WarpMasque", safeId)), recursive: true); }
        catch (Exception ex) { Logging.SaveLog("WARP MASQUE/H2 profile cleanup failed", ex); }
    }

    private static async Task<List<Endpoint>> ScanEndpointsAsync(string candidates)
    {
        var endpoints = ParseCandidates(candidates);
        using var concurrency = new SemaphoreSlim(64);
        var results = new ConcurrentBag<(Endpoint Endpoint, long Milliseconds)>();
        var scans = endpoints.Select(async endpoint =>
        {
            await concurrency.WaitAsync();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1300));
                using var client = new TcpClient(endpoint.Host.Contains(':') ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);
                var started = Stopwatch.GetTimestamp();
                await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token);
                results.Add((endpoint, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException or ArgumentException) { }
            finally { concurrency.Release(); }
        });
        await Task.WhenAll(scans);
        return results.OrderBy(result => result.Milliseconds).Select(result => result.Endpoint).ToList();
    }

    private static List<Endpoint> ParseCandidates(string? raw)
    {
        var endpoints = new List<Endpoint>();
        foreach (var token in (raw ?? string.Empty).Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var slash = token.IndexOf('/');
            if (slash > 0)
            {
                var cidr = token[..slash];
                var suffix = token[(slash + 1)..];
                var colon = suffix.IndexOf(':');
                var prefix = int.TryParse(colon >= 0 ? suffix[..colon] : suffix, out var parsedPrefix) ? parsedPrefix : -1;
                var port = int.TryParse(colon >= 0 ? suffix[(colon + 1)..] : string.Empty, out var parsedPort) ? parsedPort : DefaultEndpointPort;
                if (IPAddress.TryParse(cidr, out var address) && address.AddressFamily == AddressFamily.InterNetwork && prefix is >= 24 and <= 32 && port is >= 1 and <= 65535)
                {
                    var bytes = address.GetAddressBytes();
                    var hostCount = 1 << (32 - prefix);
                    var baseAddress = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
                    var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
                    baseAddress &= mask;
                    for (var offset = 0; offset < hostCount; offset++)
                    {
                        var value = baseAddress + (uint)offset;
                        var host = new IPAddress(new byte[]
                        {
                            (byte)((value >> 24) & 0xFF),
                            (byte)((value >> 16) & 0xFF),
                            (byte)((value >> 8) & 0xFF),
                            (byte)(value & 0xFF),
                        });
                        endpoints.Add(new Endpoint(host.ToString(), port));
                    }
                }
                continue;
            }
            try { endpoints.Add(ParseEndpoint(token, DefaultEndpointPort)); }
            catch (InvalidDataException) { }
        }
        return endpoints.Distinct().ToList();
    }

    private static Endpoint ParseEndpoint(string? host, int port)
    {
        var value = (host ?? string.Empty).Trim();
        if (value.StartsWith('[') || value.Count(character => character == ':') > 1)
        {
            var parts = SplitEndpoint(value, port);
            value = parts[0];
            port = int.Parse(parts[1]);
        }
        else if (value.LastIndexOf(':') > 0 && int.TryParse(value[(value.LastIndexOf(':') + 1)..], out var parsedPort))
        {
            port = parsedPort;
            value = value[..value.LastIndexOf(':')];
        }
        if (value.Length == 0 || value.Any(char.IsWhiteSpace) || port is < 1 or > 65535)
            throw new InvalidDataException("The WARP MASQUE endpoint host or port is invalid.");
        return new Endpoint(value, port);
    }

    private static string PatchEndpoint(string configJson, Endpoint endpoint)
    {
        var root = JsonNode.Parse(configJson)?.AsObject()
            ?? throw new InvalidDataException("The WARP MASQUE registration data is invalid.");
        root[endpoint.Host.Contains(':') ? "endpoint_h2_v6" : "endpoint_h2_v4"] = endpoint.Host;
        root[endpoint.Host.Contains(':') ? "endpoint_v6" : "endpoint_v4"] = endpoint.Host;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static async Task<string> GenerateRegistrationAsync(ProfileItem profile, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Utils.GetConfigPath(), "WarpRegistration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var clone = JsonUtils.DeepCopy(profile);
            clone.SetProtocolExtra(clone.GetProtocolExtra() with { WarpMasqueConfigJson = null });
            var executable = await ResolveExecutablePathAsync().WaitAsync(cancellationToken);
            return await GetRegisteredConfigAsync(executable, directory, Path.Combine(directory, "config.json"), clone, null, false, cancellationToken);
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    private static async Task<string> GetRegisteredConfigAsync(
        string executablePath, string runtimeDirectory, string configPath, ProfileItem profile,
        Func<bool, string, Task>? updateFunc, bool persist = true, CancellationToken cancellationToken = default)
    {
        var existing = profile.GetProtocolExtra().WarpMasqueConfigJson?.Trim();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            ValidateRegisteredConfig(existing);
            await File.WriteAllTextAsync(configPath, existing, new UTF8Encoding(false));
            return existing;
        }

        return await WarpRegistrationCoordinator.RunAsync(profile.GetProtocolExtra().WarpRegistrationProxyId,
            (url, token) => RegisterConfigAttemptAsync(executablePath, runtimeDirectory, configPath, profile, updateFunc, url, persist, token), cancellationToken);
    }

    private static async Task<string> RegisterConfigAttemptAsync(string executablePath, string runtimeDirectory,
        string configPath, ProfileItem profile, Func<bool, string, Task>? updateFunc,
        string? proxyUrl, bool persist, CancellationToken cancellationToken)
    {
        await Report(updateFunc, false, "WARP MASQUE/H2: registering a WARP device...");
        if (File.Exists(configPath)) File.Delete(configPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = runtimeDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("register");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(profile.GetProtocolExtra().WarpMasqueDeviceName ?? "PingN");
        startInfo.ArgumentList.Add("--accept-tos");
        WarpRegistrationProxySession.ApplyEnvironment(startInfo, proxyUrl);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The WARP MASQUE core did not start.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RegisterTimeout);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("WARP MASQUE registration timed out.");
        }
        await Task.WhenAll(stdoutTask, stderrTask);
        if (process.ExitCode != 0 || !File.Exists(configPath))
        {
            var details = string.Join(Environment.NewLine, new[] { stderrTask.Result.Trim(), stdoutTask.Result.Trim() }.Where(value => value.Length > 0));
            throw new InvalidOperationException("WARP MASQUE registration failed" +
                (details.Length == 0 ? $" (exit code {process.ExitCode})." : $": {details}"));
        }

        var registered = await File.ReadAllTextAsync(configPath);
        ValidateRegisteredConfig(registered);
        profile.SetProtocolExtra(profile.GetProtocolExtra() with { WarpMasqueConfigJson = registered });
        var stored = persist ? await AppManager.Instance.GetProfileItem(profile.IndexId) : null;
        if (stored is not null)
        {
                        stored.Address = profile.Address;
                        stored.Port = profile.Port;
            stored.SetProtocolExtra(profile.GetProtocolExtra());
            await ConfigHandler.AddSocksServer(AppManager.Instance.Config, stored);
            BuiltinWarpManager.RegistrationUpdated.Publish();
        }
        return registered;
    }

    private static void ValidateRegisteredConfig(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("WARP MASQUE registration returned invalid data.");
        foreach (var name in new[] { "private_key", "access_token", "id" })
        {
            if (string.IsNullOrWhiteSpace(root[name]?.GetValue<string>()))
                throw new InvalidDataException($"WARP MASQUE registration data is missing '{name}'.");
        }
    }

    private static Process StartCore(string executablePath, string configPath, string runtimeDirectory, ProfileItem profile, Endpoint endpoint)
    {
        var extra = profile.GetProtocolExtra();
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = runtimeDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "-c", configPath, "socks", "-b", Global.Loopback, "-p", profile.Port.ToString(),
            "-P", endpoint.Port.ToString(), "-s", extra.WarpMasqueSni?.Trim() ?? DefaultSni,
        }) startInfo.ArgumentList.Add(argument);
        foreach (var dns in SplitDns(extra.WarpMasqueDns))
        {
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(dns);
        }
        if (extra.WarpMasqueHttp2Enabled != false)
            startInfo.ArgumentList.Add("--http2");

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The WARP MASQUE core did not start.");
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data)) Logging.SaveLog($"WARP MASQUE/H2: {args.Data}");
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data)) Logging.SaveLog($"WARP MASQUE/H2: {args.Data}");
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task<bool> WaitForSocksTunnelAsync(Process process, int port, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!cancellation.IsCancellationRequested && !process.HasExited)
        {
            using var client = new TcpClient();
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            attempt.CancelAfter(TimeSpan.FromMilliseconds(750));
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, attempt.Token);
                var stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, attempt.Token);
                var greeting = new byte[2];
                await stream.ReadExactlyAsync(greeting, attempt.Token);
                if (greeting[0] != 0x05 || greeting[1] != 0x00)
                    throw new IOException("The local WARP MASQUE SOCKS listener rejected negotiation.");
                using var tunnelAttempt = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                tunnelAttempt.CancelAfter(TimeSpan.FromSeconds(15));
                await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00, 0x01, 1, 1, 1, 1, 0x01, 0xBB }, tunnelAttempt.Token);
                var reply = new byte[10];
                await stream.ReadExactlyAsync(reply, tunnelAttempt.Token);
                if (reply[0] == 0x05 && reply[1] == 0x00)
                    return true;
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
            {
                if (cancellation.IsCancellationRequested) break;
            }
            try { await Task.Delay(250, cancellation.Token); }
            catch (OperationCanceledException) { break; }
        }
        return false;
    }

    private static async Task<string> ReadProcessError(Process process)
    {
        if (process.HasExited)
        {
            try { return $"core exited with code {process.ExitCode}"; }
            catch { }
        }
        await Task.Yield();
        return "SOCKS5 listener or MASQUE tunnel did not become ready.";
    }

    private static async Task StopProcessAsync()
    {
        var process = _process;
        _process = null;
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex) { Logging.SaveLog("WARP MASQUE/H2 process stop failed", ex); }
        finally { process.Dispose(); }
    }

    private static async Task<string> ResolveExecutablePathAsync()
    {
        EnsureSupportedPlatform();
        var executablePath = Utils.GetConfigPath(Path.Combine("WarpMasque", "core", "usque.exe"));
        if (File.Exists(executablePath)) return executablePath;

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException("WARP MASQUE/H2 requires a Windows x64 or ARM64 build."),
        };
        var archiveName = $"usque_{CoreVersion}_windows_{arch}.zip";
        var releaseBase = $"https://github.com/Diniboy1123/usque/releases/download/v{CoreVersion}";
        var checksums = await HttpClient.GetStringAsync($"{releaseBase}/checksums.txt");
        var expectedHash = checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && string.Equals(parts[^1].TrimStart('*'), archiveName, StringComparison.Ordinal))
            .Select(parts => parts[0])
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64)
            throw new InvalidDataException("The WARP MASQUE core checksum was not present in the release checksums.");

        var archive = await HttpClient.GetByteArrayAsync($"{releaseBase}/{archiveName}");
        var actualHash = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(expectedHash.ToLowerInvariant())))
            throw new InvalidDataException("The downloaded WARP MASQUE core failed its SHA-256 check.");

        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        var temporaryPath = executablePath + ".download";
        try
        {
            using var archiveStream = new MemoryStream(archive);
            using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            var core = zip.Entries.FirstOrDefault(entry => string.Equals(Path.GetFileName(entry.FullName), "usque.exe", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("The WARP MASQUE core archive does not contain usque.exe.");
            if (core.Length is < 1 or > 80 * 1024 * 1024)
                throw new InvalidDataException("The WARP MASQUE core size is invalid.");
            await using (var output = File.Create(temporaryPath))
            await using (var input = core.Open())
                await input.CopyToAsync(output);
            File.Move(temporaryPath, executablePath, overwrite: true);
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(executablePath)!, "usque-LICENSE.txt"), UsqueLicense);
        }
        finally
        {
            try { File.Delete(temporaryPath); } catch { }
        }
        return executablePath;
    }

    private static void EnsureSupportedPlatform()
    {
        if (!Utils.IsWindows() || !Environment.Is64BitProcess || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
            throw new PlatformNotSupportedException("WARP MASQUE/H2 currently requires 64-bit Windows.");
    }

    private static List<string> SplitDns(string? dns) => (dns ?? DefaultDns)
        .Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    private static int GetAvailablePort(int preferredPort)
    {
        try
        {
            using var preferred = new TcpListener(IPAddress.Loopback, preferredPort);
            preferred.Start();
            return preferredPort;
        }
        catch (SocketException)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }

    private static Task Report(Func<bool, string, Task>? updateFunc, bool isError, string message) =>
        updateFunc is null ? Task.CompletedTask : updateFunc(isError, message);

    private sealed record Endpoint(string Host, int Port)
    {
        public string Rendered => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";
    }

    private const string UsqueLicense = """
The MIT License (MIT)
=====================

Copyright © 2025, github.com/Diniboy1123

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the “Software”), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
""";
}

