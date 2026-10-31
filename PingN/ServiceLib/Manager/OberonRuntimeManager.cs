namespace ServiceLib.Manager;

public sealed record OberonLaunchPlan(
    string ExecutablePath,
    string ConfigPath,
    string ConfigText,
    int SocksPort,
    string ScanMode,
    string ScanFamily,
    bool AutoScan);
public sealed record OberonEndpointScanResult(string Endpoint);

/// <summary>Runs the bundled Oberon AmneziaWG client as a local SOCKS5 upstream.</summary>
public static class OberonRuntimeManager
{
    public const string WarpProfileType = "amneziawg";

    public static string NormalizeScanMode(string? value) =>
        string.Equals(value?.Trim(), "all", StringComparison.OrdinalIgnoreCase) ? "all" : "fast";

    public static string NormalizeScanFamily(string? value) =>
        string.Equals(value?.Trim(), "ipv6", StringComparison.OrdinalIgnoreCase) ? "ipv6" : "ipv4";

    private static readonly TimeSpan CreateTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EndpointScanTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan FullEndpointScanTimeout = TimeSpan.FromMinutes(20);
    private static ProcessService? _processService;
    private static string? _activeConfigPath;

    public static async Task<ProfileItem> CreateProfileAsync(string subId, CancellationToken cancellationToken = default)
    {
        EnsureSupportedPlatform();
        var executablePath = ResolveExecutablePath();
        var profileDirectory = Path.Combine(Utils.GetConfigPath(), "Oberon", "profiles");
        Directory.CreateDirectory(profileDirectory);
        var temporaryConfigPath = Path.Combine(profileDirectory, $"new-{Utils.GetGuid(false)}.conf");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = profileDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("create");
            startInfo.ArgumentList.Add("-out");
            startInfo.ArgumentList.Add(temporaryConfigPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Oberon could not be started.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CreateTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                if (!cancellationToken.IsCancellationRequested)
                    throw new TimeoutException("Oberon profile creation timed out.");
                throw;
            }

            await Task.WhenAll(stdoutTask, stderrTask);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Oberon profile creation failed (exit code {process.ExitCode}).");

            var profileConfig = await File.ReadAllTextAsync(temporaryConfigPath, cancellationToken);
            ValidateProfileConfig(profileConfig);

            var item = new ProfileItem
            {
                ConfigType = EConfigType.SOCKS,
                CoreType = ECoreType.Xray,
                IsSub = false,
                Subid = subId,
                Remarks = "WARP AmneziaWG",
                Address = Global.Loopback,
                Port = GetAvailablePort(),
                Network = string.Empty,
            };
            item.SetProtocolExtra(new ProtocolExtraItem
            {
                WarpProfileType = WarpProfileType,
                WarpAmneziaConfig = profileConfig,
                WarpAmneziaAutoScan = true,
            });
            return item;
        }
        finally
        {
            try { File.Delete(temporaryConfigPath); } catch { }
        }
    }

    public static void ValidateProfileConfig(string profileConfig)
    {
        var fields = WarpAmneziaConfigCodec.ReadFields(profileConfig);
        if (string.IsNullOrWhiteSpace(profileConfig) || profileConfig.Length > 65536
            || string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Interface.PrivateKey"))
            || string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Peer.PublicKey"))
            || string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Peer.Endpoint")))
        {
            throw new InvalidDataException("The AmneziaWG profile needs [Interface] and [Peer] sections with private key, peer public key, and endpoint values.");
        }
    }

    public static async Task<OberonEndpointScanResult> ScanFastestEndpointAsync(
        string profileConfig,
        string scanMode = "fast",
        string scanFamily = "ipv4",
        CancellationToken cancellationToken = default)
    {
        EnsureSupportedPlatform();
        ValidateProfileConfig(profileConfig);
        scanMode = NormalizeScanMode(scanMode);
        scanFamily = NormalizeScanFamily(scanFamily);
        var scanTimeout = scanMode == "all" ? FullEndpointScanTimeout : EndpointScanTimeout;

        var executablePath = ResolveExecutablePath();
        var scanDirectory = Path.Combine(Utils.GetConfigPath(), "Oberon", "editor-scan");
        Directory.CreateDirectory(scanDirectory);
        var scanId = Utils.GetGuid(false);
        var sourceConfigPath = Path.Combine(scanDirectory, $"{scanId}-source.conf");
        var selectedConfigPath = Path.Combine(scanDirectory, $"{scanId}-selected.conf");

        try
        {
            await File.WriteAllTextAsync(sourceConfigPath, profileConfig, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = scanDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("scan");
            startInfo.ArgumentList.Add("-config");
            startInfo.ArgumentList.Add(sourceConfigPath);
            startInfo.ArgumentList.Add("-mode");
            startInfo.ArgumentList.Add(scanMode);
            if (scanFamily == "ipv6")
            {
                startInfo.ArgumentList.Add("-family");
                startInfo.ArgumentList.Add(scanFamily);
            }
            startInfo.ArgumentList.Add("-P");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(selectedConfigPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Oberon could not be started for endpoint scanning.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(scanTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                try { await process.WaitForExitAsync(); } catch { }
                try { await Task.WhenAll(stdoutTask, stderrTask); } catch { }
                if (cancellationToken.IsCancellationRequested)
                    throw;
                throw new TimeoutException($"Oberon endpoint scanning timed out after {scanTimeout.TotalMinutes:0} minutes.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                var details = string.Join(Environment.NewLine, new[] { stderr.Trim(), stdout.Trim() }.Where(value => value.Length > 0));
                if (details.Length > 800) details = details[^800..];
                throw new InvalidOperationException(details.Length == 0
                    ? $"Oberon endpoint scan failed (exit code {process.ExitCode})."
                    : $"Oberon endpoint scan failed (exit code {process.ExitCode}): {details}");
            }

            if (!File.Exists(selectedConfigPath))
            {
                throw new InvalidDataException("Oberon completed the endpoint scan but did not create the selected profile.");
            }

            var selectedConfig = await File.ReadAllTextAsync(selectedConfigPath, cancellationToken);
            ValidateProfileConfig(selectedConfig);
            var endpoint = WarpAmneziaConfigCodec.ReadFields(selectedConfig)
                .GetValueOrDefault("Peer.Endpoint", string.Empty).Trim();
            if (endpoint.Length == 0)
                throw new InvalidDataException("Oberon selected profile does not contain a peer endpoint.");
            return new OberonEndpointScanResult(endpoint);
        }
        finally
        {
            try { File.Delete(sourceConfigPath); } catch { }
            try { File.Delete(selectedConfigPath); } catch { }
        }
    }

    public static string ApplyScannedEndpoint(string profileConfig, string scannedEndpoint)
    {
        ValidateProfileConfig(profileConfig);
        if (string.IsNullOrWhiteSpace(scannedEndpoint) || scannedEndpoint.IndexOfAny(['\r', '\n', '#', ';']) >= 0)
            throw new InvalidDataException("Oberon returned an invalid endpoint.");

        var updatedConfig = WarpAmneziaConfigCodec.UpdateFields(
            profileConfig,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Peer.Endpoint"] = scannedEndpoint.Trim(),
            });
        ValidateProfileConfig(updatedConfig);
        return updatedConfig;
    }

    public static async Task<OberonLaunchPlan?> PrepareAsync(ProfileItem profile)
    {
        var extra = profile.GetProtocolExtra();
        if (!string.Equals(extra.WarpProfileType, WarpProfileType, StringComparison.OrdinalIgnoreCase))
            return null;

        EnsureSupportedPlatform();
        if (profile.ConfigType != EConfigType.SOCKS
            || !string.Equals(profile.Address, Global.Loopback, StringComparison.OrdinalIgnoreCase)
            || profile.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("WARP AmneziaWG must use the saved local SOCKS5 address and port.");
        }
        if (string.IsNullOrWhiteSpace(extra.WarpAmneziaConfig))
            throw new InvalidOperationException("The saved Oberon AmneziaWG profile is missing. Add the WARP AmneziaWG profile again.");

        var executablePath = ResolveExecutablePath();
        var configPath = GetMaterializedConfigPath(profile.IndexId);
        return new OberonLaunchPlan(
            executablePath,
            configPath,
            extra.WarpAmneziaConfig,
            profile.Port,
            NormalizeScanMode(extra.WarpAmneziaScanMode),
            NormalizeScanFamily(extra.WarpAmneziaScanFamily),
            extra.WarpAmneziaAutoScan != false);
    }

    public static async Task StartAsync(OberonLaunchPlan? plan, Func<bool, string, Task>? updateFunc)
    {
        if (plan is null)
            return;

        await StopAsync();
        _activeConfigPath = plan.ConfigPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(plan.ConfigPath)!);
            await File.WriteAllTextAsync(plan.ConfigPath, plan.ConfigText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var scanFamily = NormalizeScanFamily(plan.ScanFamily);
            var arguments = $"connect -config {plan.ConfigPath.AppendQuotes()} -socks {Global.Loopback}:{plan.SocksPort}";
            if (plan.AutoScan)
            {
                arguments += $" -mode {NormalizeScanMode(plan.ScanMode)}" +
                    (scanFamily == "ipv6" ? " -family ipv6" : string.Empty);
            }
            else
            {
                arguments += " -no-scan";
            }
            var processService = new ProcessService(
                fileName: plan.ExecutablePath,
                arguments: arguments,
                workingDirectory: Path.GetDirectoryName(plan.ConfigPath) ?? Utils.GetConfigPath(),
                displayLog: true,
                redirectInput: false,
                environmentVars: null,
                updateFunc: updateFunc);

            _processService = processService;
            await processService.StartAsync();
            await WaitForSocksReady(processService, plan.SocksPort, plan.ScanMode, plan.AutoScan);
        }
        catch
        {
            await StopAsync();
            throw;
        }
    }

    public static async Task StopAsync()
    {
        var processService = _processService;
        _processService = null;
        if (processService is not null)
        {
            try { await processService.StopAsync(); }
            catch (Exception ex) { Logging.SaveLog("Oberon stop failed", ex); }
            try { processService.Dispose(); }
            catch (Exception ex) { Logging.SaveLog("Oberon dispose failed", ex); }
        }

        var configPath = _activeConfigPath;
        _activeConfigPath = null;
        if (!string.IsNullOrEmpty(configPath))
        {
            try { File.Delete(configPath); }
            catch (Exception ex) { Logging.SaveLog("Oberon temporary profile cleanup failed", ex); }
        }
    }

    public static void DeleteMaterializedConfig(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return;
        try { File.Delete(GetMaterializedConfigPath(profileId)); }
        catch (Exception ex) { Logging.SaveLog("Oberon profile cleanup failed", ex); }
    }

    private static async Task WaitForSocksReady(ProcessService processService, int port, string scanMode, bool autoScan)
    {
        var timeoutDuration = autoScan && NormalizeScanMode(scanMode) == "all"
            ? TimeSpan.FromMinutes(20)
            : ConnectTimeout;
        using var timeout = new CancellationTokenSource(timeoutDuration);
        var token = timeout.Token;
        var greeting = new byte[] { 0x05, 0x01, 0x00 };
        var response = new byte[2];

        while (!token.IsCancellationRequested)
        {
            if (processService.HasExited)
                throw new InvalidOperationException("Oberon exited before starting its local SOCKS5 listener.");

            using var client = new TcpClient();
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
            attempt.CancelAfter(TimeSpan.FromMilliseconds(750));
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, attempt.Token);
                var stream = client.GetStream();
                await stream.WriteAsync(greeting, attempt.Token);
                var count = await stream.ReadAsync(response.AsMemory(), attempt.Token);
                if (count == 2 && response[0] == 0x05 && response[1] == 0x00)
                    return;
            }
            catch (SocketException) { }
            catch (IOException) { }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }

            try { await Task.Delay(250, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
        }

        throw new TimeoutException($"Oberon did not start its SOCKS5 listener on {Global.Loopback}:{port}.");
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string GetMaterializedConfigPath(string profileId)
    {
        var safeId = Regex.Replace(profileId, @"[^A-Za-z0-9_-]", string.Empty);
        if (safeId.Length == 0)
            throw new InvalidOperationException("WARP AmneziaWG profile ID is invalid.");
        return Utils.GetConfigPath(Path.Combine("Oberon", "runtime", $"{safeId}.conf"));
    }

    private static string ResolveExecutablePath()
    {
        var candidates = new[]
        {
            Utils.GetBinPath("oberon.exe", "oberon"),
            Utils.GetBaseDirectory(Path.Combine("bin", "oberon", "oberon.exe")),
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("The bundled Oberon core was not found. Publish PingN for win-x64 and keep the bin\\oberon folder beside the application.");
    }

    private static void EnsureSupportedPlatform()
    {
        if (!Utils.IsWindows() || RuntimeInformation.OSArchitecture != Architecture.X64 || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The bundled Oberon core currently supports Windows x64 only.");
    }
}
