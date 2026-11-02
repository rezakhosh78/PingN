using Renci.SshNet;

namespace ServiceLib.Manager;

public static class DnsTunnelRuntimeManager
{
    private static Process? _process;
    private static SshClient? _ssh;
    private static ForwardedPortDynamic? _forward;
    private static CancellationTokenSource? _lifetime;
    private static Task[] _drains = [];
    private static string? _activeLabel;
    private static Func<bool, string, Task>? _activeLog;
    private static DnsTunnelSettings? _activeSettings;

    public static ProfileItem CreateProfile(string kind, string subId)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var item = new ProfileItem { ConfigType = EConfigType.SOCKS, CoreType = ECoreType.Xray,
            Subid = subId, IsSub = false, Address = Global.Loopback, Port = port,
            Remarks = kind == "masterdns" ? "MasterDNS" : "DNSTT SSH", Network = string.Empty };
        var settings = new DnsTunnelSettings { Kind = kind };
        foreach (var (key, value) in DnsTunnelCoreInstaller.InstalledMasterDefaults())
            settings.MasterOptions[key] = value;
        item.SetProtocolExtra(new ProtocolExtraItem { DnsTunnel = settings });
        return item;
    }

    public static async Task StartAsync(ProfileItem profile, Func<bool, string, Task>? log, CancellationToken cancellationToken = default)
    {
        var settings = profile.GetProtocolExtra().DnsTunnel;
        if (settings is null) return;
        if (profile.ConfigType != EConfigType.SOCKS || profile.Address != Global.Loopback)
            throw new InvalidOperationException("DNS tunnels must use a local SOCKS profile.");
        settings.Validate(profile.Port);
        await StopAsync();
        var label = settings.Kind == "masterdns" ? "MasterDNS" : "DNSTT";
        await EmitAsync(label, settings, log, "Preparing client executable...");
        var exe = await DnsTunnelCoreInstaller.EnsureAsync(settings.Kind).WaitAsync(cancellationToken);
        await EmitAsync(label, settings, log, $"Using client: {exe}");
        var id = Regex.Replace(profile.IndexId, "[^a-zA-Z0-9_-]", string.Empty);
        if (id.Length == 0) throw new InvalidDataException("Invalid profile ID.");
        var directory = Path.Combine(Utils.GetConfigPath(), "DnsTunnelRuntime", id);
        Directory.CreateDirectory(directory);
        var info = new ProcessStartInfo(exe) { WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };

        if (settings.Kind == "masterdns")
        {
            var options = new Dictionary<string, string>(settings.MasterOptions)
            {
                ["PROTOCOL_TYPE"] = "\"SOCKS5\"", ["LISTEN_IP"] = "\"127.0.0.1\"",
                ["LISTEN_PORT"] = profile.Port.ToString(), ["SOCKS5_AUTH"] = "false",
            };
            var configPath = Path.Combine(directory, "client_config.toml");
            await File.WriteAllTextAsync(configPath, string.Join(Environment.NewLine, options.Select(x => $"{x.Key} = {x.Value}")));
            await File.WriteAllTextAsync(Path.Combine(directory, "client_resolvers.txt"), settings.Resolvers);
            info.ArgumentList.Add("-config"); info.ArgumentList.Add(configPath);
        }
        else
        {
            var pubkeyPath = Path.Combine(directory, "server.pub");
            await File.WriteAllTextAsync(pubkeyPath, settings.PublicKey);
            foreach (var arg in new[] { "-" + settings.Transport, settings.Resolver, "-pubkey-file", pubkeyPath,
                "-utls", settings.Utls, settings.Domain, $"127.0.0.1:{settings.TunnelPort}" }) info.ArgumentList.Add(arg);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var lifetime = _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var process = _process = new Process { StartInfo = info };
        _activeLabel = label; _activeLog = log; _activeSettings = settings;
        try
        {
            if (!process.Start()) throw new IOException($"Could not start {label}.");
            // Core output can contain configuration data; keep credentials out of the shared log.
            _drains = [DrainAsync(process.StandardOutput, label, "stdout", settings, log, lifetime.Token),
                DrainAsync(process.StandardError, label, "stderr", settings, log, lifetime.Token)];
            await EmitAsync(label, settings, log, $"Client process started (PID {process.Id}); local SOCKS port {profile.Port}.");
            if (settings.Kind == "dnstt-ssh")
            {
                await WaitPortAsync(int.Parse(settings.TunnelPort), process, TimeSpan.FromSeconds(15), lifetime.Token);
                var authentication = new List<AuthenticationMethod>();
                if (!string.IsNullOrEmpty(settings.SshPassword)) authentication.Add(new PasswordAuthenticationMethod(settings.SshUser, settings.SshPassword));
                PrivateKeyFile? key = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(settings.PrivateKeyPath))
                    {
                        key = string.IsNullOrEmpty(settings.PrivateKeyPassphrase) ? new PrivateKeyFile(settings.PrivateKeyPath)
                            : new PrivateKeyFile(settings.PrivateKeyPath, settings.PrivateKeyPassphrase);
                        authentication.Add(new PrivateKeyAuthenticationMethod(settings.SshUser, key));
                    }
                    var connection = new ConnectionInfo("127.0.0.1", int.Parse(settings.TunnelPort), settings.SshUser, authentication.ToArray())
                    { Timeout = TimeSpan.FromSeconds(int.Parse(settings.ConnectTimeoutSeconds)) };
                    var ssh = _ssh = new SshClient(connection) { KeepAliveInterval = TimeSpan.FromSeconds(int.Parse(settings.KeepAliveSeconds)) };
                    ssh.ErrorOccurred += (_, e) => { _ = EmitAsync(label, settings, log, "SSH error: " + e.Exception.Message); };
                    string? observedFingerprint = null;
                    ssh.HostKeyReceived += (_, e) =>
                    {
                        var fingerprint = "SHA256:" + Convert.ToBase64String(SHA256.HashData(e.HostKey)).TrimEnd('=');
                        observedFingerprint = fingerprint;
                        e.CanTrust = string.IsNullOrWhiteSpace(settings.HostKeySha256)
                            || fingerprint == settings.HostKeySha256.TrimEnd('=');
                    };
                    await EmitAsync(label, settings, log, "Connecting SSH through the DNS tunnel...");
                    var sshTimer = Stopwatch.StartNew();
                    await ssh.ConnectAsync(lifetime.Token);
                    await EmitAsync(label, settings, log, $"SSH connection completed in {sshTimer.Elapsed.TotalSeconds:F1} seconds.");
                    if (string.IsNullOrWhiteSpace(settings.HostKeySha256) && observedFingerprint is not null)
                    {
                        settings.HostKeySha256 = observedFingerprint;
                        profile.SetProtocolExtra(profile.GetProtocolExtra() with { DnsTunnel = settings });
                        var stored = await AppManager.Instance.GetProfileItem(profile.IndexId);
                        if (stored is not null)
                        {
                            stored.SetProtocolExtra(stored.GetProtocolExtra() with { DnsTunnel = settings });
                            if (await ConfigHandler.AddSocksServer(AppManager.Instance.Config, stored) != 0)
                                throw new IOException("Could not save the SSH host fingerprint.");
                        }
                        await EmitAsync(label, settings, log, "First successful SSH connection: saved server host fingerprint " + observedFingerprint);
                    }
                    await EmitAsync(label, settings, log, "SSH authentication succeeded; starting dynamic SOCKS forwarding.");
                    var forward = _forward = new ForwardedPortDynamic("127.0.0.1", (uint)profile.Port);
                    long lastForwardingErrorTicks = 0;
                    var suppressedForwardingErrors = 0;
                    forward.Exception += (_, e) =>
                    {
                        var now = Stopwatch.GetTimestamp();
                        while (true)
                        {
                            var last = Interlocked.Read(ref lastForwardingErrorTicks);
                            if (last != 0 && Stopwatch.GetElapsedTime(last, now) < TimeSpan.FromSeconds(5))
                            {
                                Interlocked.Increment(ref suppressedForwardingErrors);
                                return;
                            }

                            if (Interlocked.CompareExchange(ref lastForwardingErrorTicks, now, last) == last)
                            {
                                break;
                            }
                        }

                        var suppressed = Interlocked.Exchange(ref suppressedForwardingErrors, 0);
                        var message = "SOCKS forwarding error: " + e.Exception.Message;
                        if (suppressed > 0)
                        {
                            message += $" ({suppressed} similar errors suppressed in the previous 5 seconds)";
                        }
                        _ = EmitAsync(label, settings, log, message);
                    };
                    ssh.AddForwardedPort(forward);
                    forward.Start();
                }
                finally { key?.Dispose(); }
            }
            await WaitPortAsync(profile.Port, process, TimeSpan.FromSeconds(120), lifetime.Token);
            await EmitAsync(label, settings, log, $"Local SOCKS listener ready on 127.0.0.1:{profile.Port}.");
            _ = MonitorAsync(process, label, log, lifetime.Token);
        }
        catch (Exception ex)
        {
            await EmitAsync(label, settings, log, "Startup failed: " + ex.Message);
            await StopAsync();
            throw;
        }
    }

    private static async Task DrainAsync(StreamReader reader, string label, string streamName, DnsTunnelSettings settings,
        Func<bool, string, Task>? log, CancellationToken token)
    {
        try
        {
            while (await reader.ReadLineAsync(token) is { } line)
            {
                await EmitAsync(label, settings, log, $"[{streamName}] {line}");
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
    }

    private static async Task EmitAsync(string label, DnsTunnelSettings settings, Func<bool, string, Task>? log, string message)
    {
        string encryptionKey;
        try { encryptionKey = JsonSerializer.Deserialize<string>(settings.MasterOptions.GetValueOrDefault("ENCRYPTION_KEY", "\"\"")) ?? string.Empty; }
        catch (JsonException) { encryptionKey = settings.MasterOptions.GetValueOrDefault("ENCRYPTION_KEY", "").Trim('"'); }
        foreach (var secret in new[] { settings.SshPassword, settings.PrivateKeyPassphrase, encryptionKey })
            if (!string.IsNullOrEmpty(secret)) message = message.Replace(secret, "[redacted]", StringComparison.Ordinal);
        var line = $"[{label}] {message}";
        Logging.SaveLog(line);
        if (log is not null) await log(false, line);
    }

    private static async Task MonitorAsync(Process process, string label, Func<bool, string, Task>? log, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (process.HasExited || (_ssh is not null && !_ssh.IsConnected))
                {
                    var reason = process.HasExited ? $"Client exited with code {process.ExitCode}." : "SSH connection closed.";
                    if (_activeSettings is { } active) await EmitAsync(label, active, log, reason + " Disconnect and reconnect to retry.");
                    // Stop only this generation; a later connection has a different process instance.
                    if (ReferenceEquals(_process, process)) await StopAsync();
                    return;
                }
                await Task.Delay(1000, token);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or InvalidOperationException) { }
    }

    private static async Task WaitPortAsync(int port, Process process, TimeSpan timeout, CancellationToken token)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            token.ThrowIfCancellationRequested();
            if (process.HasExited) throw new IOException($"DNS core exited with code {process.ExitCode}.");
            try { using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port, token); return; }
            catch (SocketException) { await Task.Delay(200, token); }
        }
        throw new TimeoutException($"DNS tunnel listener on port {port} did not start.");
    }

    public static async Task StopAsync()
    {
        var process = _process; _process = null;
        var ssh = _ssh; _ssh = null;
        var forward = _forward; _forward = null;
        var lifetime = _lifetime; _lifetime = null;
        var drains = _drains; _drains = [];
        var settings = _activeSettings; _activeSettings = null;
        var label = _activeLabel; _activeLabel = null;
        var log = _activeLog; _activeLog = null;
        try { forward?.Stop(); } catch { }
        forward?.Dispose();
        try { ssh?.Disconnect(); } catch { }
        ssh?.Dispose();
        try { if (process is { HasExited: false }) process.Kill(true); } catch { }
        // Let stdout/stderr reach EOF, including the last startup error, before disposal.
        try { await Task.WhenAll(drains).WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        lifetime?.Cancel(); lifetime?.Dispose();
        process?.Dispose();
        if (label is not null && settings is not null) await EmitAsync(label, settings, log, "Client stopped; local listeners released.");
    }
}
