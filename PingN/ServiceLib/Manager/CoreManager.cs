namespace ServiceLib.Manager;

/// <summary>
/// Core process processing class
/// </summary>
public class CoreManager
{
    private static readonly Lazy<CoreManager> _instance = new(() => new());
    public static CoreManager Instance => _instance.Value;
    private Config _config;

    [SupportedOSPlatform("windows")]
    private WindowsJobService? _processJob;

    private ProcessService? _processService;
    private ProcessService? _processPreService;
    private CancellationTokenSource? _featureLogCancellation;
    private Task? _featureLogTask;
    private bool _linuxSudo = false;
    private Func<bool, string, Task>? _updateFunc;
    private const string _tag = "CoreHandler";

    public async Task Init(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        //Copy the bin folder to the storage location (for init)
        if (Environment.GetEnvironmentVariable(Global.LocalAppData) == "1")
        {
            var fromPath = Utils.GetBaseDirectory("bin");
            var toPath = Utils.GetBinPath("");
            if (fromPath != toPath)
            {
                FileUtils.CopyDirectory(fromPath, toPath, true, false);
            }
        }

        if (Utils.IsNonWindows())
        {
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo();
            foreach (var it in coreInfo)
            {
                if (it.CoreType == ECoreType.PingN)
                {
                    if (Utils.UpgradeAppExists(out var upgradeFileName))
                    {
                        await Utils.SetLinuxChmod(upgradeFileName);
                    }
                    continue;
                }

                foreach (var name in it.CoreExes)
                {
                    var exe = Utils.GetBinPath(Utils.GetExeName(name), it.CoreType.ToString());
                    if (File.Exists(exe))
                    {
                        await Utils.SetLinuxChmod(exe);
                    }
                }
            }
        }
    }

    /// <param name="mainContext">Resolved main context (with pre-socks ports already merged if applicable).</param>
    /// <param name="preContext">Optional pre-socks context passed to <see cref="CoreStartPreService"/>.</param>
    public async Task LoadCore(CoreConfigContext? mainContext, CoreConfigContext? preContext)
    {
        if (mainContext == null)
        {
            await UpdateFunc(false, ResUI.CheckServerSettings);
            return;
        }

        var node = mainContext.Node;
        if (WarpMasqueRuntimeManager.NormalizeLocalPort(node))
        {
            var storedMasqueProfile = await AppManager.Instance.GetProfileItem(node.IndexId);
            if (storedMasqueProfile is not null)
            {
                storedMasqueProfile.Address = node.Address;
                storedMasqueProfile.Port = node.Port;
                storedMasqueProfile.SetProtocolExtra(node.GetProtocolExtra());
                await ConfigHandler.AddSocksServer(AppManager.Instance.Config, storedMasqueProfile);
            }
        }
        var fileName = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        var result = await CoreConfigHandler.GenerateClientConfig(mainContext, fileName);
        if (result.Success != true)
        {
            await UpdateFunc(true, result.Msg);
            return;
        }

        DesyncLaunchPlan? desyncPlan;
        try
        {
            desyncPlan = await DesyncRuntimeManager.PrepareAsync(node, fileName, mainContext.RunCoreType);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Desync: prepare failed", ex);
            await UpdateFunc(true, $"Desync preparation failed: {ex.Message}");
            return;
        }
        await UpdateFunc(false, desyncPlan is null
            ? "Desync: disabled for this profile."
            : $"Desync: configuration prepared; runtime logs: {desyncPlan.RuntimeDirectory}");

        SniSpoofingPlusLaunchPlan? sniPlan;
        try
        {
            sniPlan = await SniSpoofingPlusRuntimeManager.PrepareAsync(node, fileName, mainContext.RunCoreType);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("SNI Spoofing Plus: prepare failed", ex);
            await UpdateFunc(true, $"SNI Spoofing Plus preparation failed: {ex.Message}");
            return;
        }
        await UpdateFunc(false, sniPlan is null
            ? "SNI Spoofing Plus: disabled for this profile."
            : $"SNI Spoofing Plus: configuration prepared; runtime logs: {sniPlan.RuntimeDirectory}");

        OberonLaunchPlan? oberonPlan;
        try
        {
            oberonPlan = await OberonRuntimeManager.PrepareAsync(node);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Oberon: prepare failed", ex);
            await UpdateFunc(true, $"WARP AmneziaWG preparation failed: {ex.Message}");
            return;
        }
        await UpdateFunc(false, oberonPlan is null
            ? "WARP AmneziaWG: disabled for this profile."
            : oberonPlan.AutoScan
                ? "WARP AmneziaWG: configuration prepared; Oberon will scan endpoints on connection."
                : "WARP AmneziaWG: configuration prepared; the saved endpoint will be used without scanning.");

        WarpMasqueLaunchPlan? warpMasquePlan;
        try
        {
            warpMasquePlan = await WarpMasqueRuntimeManager.PrepareAsync(node);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("WARP MASQUE/H2: prepare failed", ex);
            await UpdateFunc(true, $"WARP MASQUE/H2 preparation failed: {ex.Message}");
            return;
        }
        await UpdateFunc(false, warpMasquePlan is null
            ? "WARP MASQUE/H2: disabled for this profile."
            : "WARP MASQUE/H2: core prepared; registering and connecting on startup.");

        await UpdateFunc(false, $"{node.GetSummary()}");
        await UpdateFunc(false, $"{Utils.GetRuntimeInfo()}");
        await UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
        await CoreStop();
        await Task.Delay(100);
        if (desyncPlan is not null || sniPlan is not null)
        {
            var files = new List<(string Feature, string Path)>();
            if (desyncPlan is not null)
                files.AddRange(new[] { "winws2-stdout.log", "winws2-stderr.log", "winws2.log" }
                    .Select(name => ("Desync", Path.Combine(desyncPlan.RuntimeDirectory, name))));
            if (sniPlan is not null)
                files.AddRange(new[] { "sni-stdout.log", "sni-stderr.log" }
                    .Select(name => ("SNI Spoofing Plus", Path.Combine(sniPlan.RuntimeDirectory, name))));
            foreach (var (_, path) in files)
            {
                try { File.WriteAllText(path, string.Empty); }
                catch (Exception ex) { Logging.SaveLog($"Feature log reset failed: {path}", ex); }
            }
            _featureLogCancellation = new CancellationTokenSource();
            _featureLogTask = MonitorFeatureLogs(files, _featureLogCancellation.Token);
        }

        try
        {
            if (Utils.IsWindows() && (mainContext.IsTunEnabled || preContext?.IsTunEnabled == true))
            {
                await Task.Delay(100);
                await WindowsUtils.RemoveTunDevice();
            }

            try
            {
                if (sniPlan is not null) await UpdateFunc(false, "SNI Spoofing Plus: starting elevated CLI...");
                await SniSpoofingPlusRuntimeManager.StartAsync(sniPlan);
                if (sniPlan is not null) await UpdateFunc(false, "SNI Spoofing Plus: READY; local listener started (traffic still needs verification).");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"SNI Spoofing Plus start failed: {ex.Message}", ex);
            }

            try
            {
                if (desyncPlan is not null) await UpdateFunc(false, "Desync: starting elevated winws2...");
                await DesyncRuntimeManager.StartAsync(desyncPlan);
                if (desyncPlan is not null) await UpdateFunc(false, "Desync: READY; packet interception started (traffic still needs verification).");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Desync start failed: {ex.Message}", ex);
            }

            try
            {
                if (oberonPlan is not null)
                    await UpdateFunc(false, oberonPlan.AutoScan
                        ? "WARP AmneziaWG: starting Oberon and scanning endpoints..."
                        : "WARP AmneziaWG: starting Oberon with the saved endpoint...");
                await OberonRuntimeManager.StartAsync(oberonPlan, _updateFunc);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"WARP AmneziaWG start failed: {ex.Message}", ex);
            }

            try
            {
                if (warpMasquePlan is not null) await UpdateFunc(false, "WARP MASQUE/H2: starting core and connecting...");
                await WarpMasqueRuntimeManager.StartAsync(warpMasquePlan, _updateFunc);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"WARP MASQUE/H2 start failed: {ex.Message}", ex);
            }

            await CoreStart(mainContext);
            if (_processService is null or { HasExited: true })
            {
                throw new InvalidOperationException(ResUI.FailedToRunCore);
            }

            await WaitForProxyPort(preContext);
            await CoreStartPreService(preContext);
            AppManager.Instance.RunningCoreType = preContext?.RunCoreType ?? mainContext.RunCoreType;
            await UpdateFunc(true, $"{node.GetSummary()}");
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await CoreStop();
            await UpdateFunc(true, ex.Message);
            if (desyncPlan is not null) await ReportRuntimeErrors("Desync", desyncPlan.RuntimeDirectory, "winws2-stderr.log", "winws2-stdout.log", "winws2.log");
            if (sniPlan is not null) await ReportRuntimeErrors("SNI Spoofing Plus", sniPlan.RuntimeDirectory, "sni-stderr.log", "sni-stdout.log");
        }
    }

    private async Task ReportRuntimeErrors(string feature, string directory, params string[] files)
    {
        foreach (var name in files)
        {
            var path = Path.Combine(directory, name);
            try
            {
                if (!File.Exists(path)) continue;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 3000) stream.Seek(-3000, SeekOrigin.End);
                using var reader = new StreamReader(stream);
                var output = (await reader.ReadToEndAsync()).Trim();
                if (output.Length > 0) await UpdateFunc(false, $"{feature} {name}: {output}");
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"{feature}: could not read {path}", ex);
            }
        }
    }

    private async Task MonitorFeatureLogs(List<(string Feature, string Path)> files, CancellationToken token)
    {
        var positions = new Dictionary<string, long>();
        while (!token.IsCancellationRequested)
        {
            foreach (var (feature, path) in files)
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var position = positions.GetValueOrDefault(path);
                    if (stream.Length < position) position = 0;
                    if (stream.Length - position > 12000)
                    {
                        position = stream.Length - 12000;
                        await UpdateFunc(false, $"{feature}: large debug output; displaying the latest 12 KB from {Path.GetFileName(path)}.");
                    }
                    stream.Seek(position, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                    var content = await reader.ReadToEndAsync();
                    positions[path] = stream.Position;
                    if (!string.IsNullOrWhiteSpace(content))
                        await UpdateFunc(false, $"{feature} [{Path.GetFileName(path)}]: {content.TrimEnd()}");
                }
                catch (Exception ex) { Logging.SaveLog($"{feature}: live log read failed for {path}", ex); }
            }
            try { await Task.Delay(750, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<ProcessService?> LoadCoreConfigSpeedtest(List<ServerTestItem> selecteds)
    {
        var coreType = selecteds.FirstOrDefault()?.CoreType == ECoreType.sing_box ? ECoreType.sing_box : ECoreType.Xray;
        var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
        var configPath = Utils.GetBinConfigPath(fileName);
        var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, configPath, selecteds, coreType);
        await UpdateFunc(false, result.Msg);
        if (result.Success != true)
        {
            return null;
        }

        await UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
        await UpdateFunc(false, configPath);

        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
        return await RunProcess(coreInfo, fileName, true, false);
    }

    public async Task<ProcessService?> LoadCoreConfigSpeedtest(ServerTestItem testItem)
    {
        var node = await AppManager.Instance.GetProfileItem(testItem.IndexId);
        if (node is null)
        {
            return null;
        }

        var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
        var configPath = Utils.GetBinConfigPath(fileName);
        var (context, _) = await CoreConfigContextBuilder.Build(_config, node);
        var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, context, testItem, configPath);
        if (result.Success != true)
        {
            return null;
        }

        var coreType = context.RunCoreType;
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
        return await RunProcess(coreInfo, fileName, true, false);
    }

    public async Task CoreStop()
    {
        try
        {
            _featureLogCancellation?.Cancel();
            if (_featureLogTask is not null) await _featureLogTask;
            _featureLogCancellation?.Dispose();
            _featureLogCancellation = null;
            _featureLogTask = null;
            await DesyncRuntimeManager.StopAsync();
            await SniSpoofingPlusRuntimeManager.StopAsync();

            if (_linuxSudo)
            {
                await CoreAdminManager.Instance.KillProcessAsLinuxSudo();
                _linuxSudo = false;
            }

            if (_processService != null)
            {
                await _processService.StopAsync();
                _processService.Dispose();
                _processService = null;
            }

            if (_processPreService != null)
            {
                await _processPreService.StopAsync();
                _processPreService.Dispose();
                _processPreService = null;
            }

            await OberonRuntimeManager.StopAsync();
            await WarpMasqueRuntimeManager.StopAsync();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    #region Private

    private async Task CoreStart(CoreConfigContext context)
    {
        var node = context.Node;
        var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType);
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);

        var displayLog = node.ConfigType != EConfigType.Custom || node.DisplayLog;
        var proc = await RunProcess(coreInfo, Global.CoreConfigFileName, displayLog, true, context.IsTunEnabled);
        if (proc is null)
        {
            return;
        }
        _processService = proc;
    }

    private async Task CoreStartPreService(CoreConfigContext? preContext)
    {
        if (_processService is { HasExited: false } && preContext != null)
        {
            var preCoreType = preContext?.Node?.CoreType ?? ECoreType.sing_box;
            var fileName = Utils.GetBinConfigPath(Global.CorePreConfigFileName);
            var result = await CoreConfigHandler.GenerateClientConfig(preContext, fileName);
            if (result.Success)
            {
                var coreInfo = CoreInfoManager.Instance.GetCoreInfo(preCoreType);
                var proc = await RunProcess(coreInfo, Global.CorePreConfigFileName, true, true, preContext.IsTunEnabled);
                if (proc is null)
                {
                    return;
                }
                _processPreService = proc;
            }
        }
    }

    private async Task UpdateFunc(bool notify, string msg)
    {
        await _updateFunc?.Invoke(notify, msg);
    }

    private static async Task WaitForProxyPort(CoreConfigContext? preContext)
    {
        if (preContext is null)
        {
            return;
        }
        if (!preContext.IsTunEnabled)
        {
            return;
        }

        using var rootCts = new CancellationTokenSource(Global.LocalFetch);
        var rootToken = rootCts.Token;

        var port = preContext.Node.Port;
        // SOCKS5 client greeting: VER=5, NMETHODS=1, METHOD=0x00 (no auth)
        ReadOnlyMemory<byte> greeting = new byte[] { 0x05, 0x01, 0x00 };
        var buf = new byte[2];

        while (!rootToken.IsCancellationRequested)
        {
            using var tcp = new TcpClient();
            using var attemptCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(rootToken, attemptCts.Token);
            var linkedToken = linkedCts.Token;
            try
            {
                await tcp.ConnectAsync(Global.Loopback, port, linkedToken);
                var stream = tcp.GetStream();

                await stream.WriteAsync(greeting, linkedToken);

                var read = await stream.ReadAsync(buf.AsMemory(0, 2), linkedToken);

                // Server selection: VER=5, METHOD=0x00 — proxy is fully ready
                if (read == 2 && buf[0] == 0x05)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                if (!rootToken.IsCancellationRequested)
                {
                    continue;
                }
                Logging.SaveLog($"WaitForProxyPort Timeout waiting for proxy port {port} to be ready.");
                return;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                // Connection refused, proxy not ready yet, wait 50ms before retrying
                try
                {
                    await Task.Delay(50, rootToken);
                }
                catch (OperationCanceledException)
                {
                    Logging.SaveLog($"WaitForProxyPort Timeout waiting for proxy port {port} to be ready.");
                    return;
                }
            }
            catch
            {
                // Ignore other exceptions and continue
            }
        }
    }

    #endregion Private

    #region Process

    /// <summary>
    ///     Decides whether a core launch must be elevated on non-Windows platforms.
    ///     The TUN state comes from the immutable <see cref="CoreConfigContext" /> snapshot that
    ///     generated the config, never from the live mutable config: the generated config and the
    ///     launch mode must always agree, even if TUN is toggled while a reload is in flight.
    /// </summary>
    public static bool ShouldRunAsSudo(bool isTunLaunch, ECoreType? coreType, bool isNonWindows)
    {
        return isTunLaunch
            && coreType is ECoreType.sing_box or ECoreType.mihomo or ECoreType.Xray
            && isNonWindows;
    }

    private async Task<ProcessService?> RunProcess(CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo, bool isTunLaunch = false)
    {
        var fileName = CoreInfoManager.Instance.GetCoreExecFile(coreInfo, out var msg);
        if (fileName.IsNullOrEmpty())
        {
            await UpdateFunc(false, msg);
            return null;
        }

        try
        {
            if (mayNeedSudo
                && ShouldRunAsSudo(isTunLaunch, coreInfo.CoreType, Utils.IsNonWindows()))
            {
                _linuxSudo = true;
                await CoreAdminManager.Instance.Init(_config, _updateFunc);
                return await CoreAdminManager.Instance.RunProcessAsLinuxSudo(fileName, coreInfo, configPath);
            }

            return await RunProcessNormal(fileName, coreInfo, configPath, displayLog);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await UpdateFunc(mayNeedSudo, ex.Message);
            return null;
        }
    }

    private async Task<ProcessService?> RunProcessNormal(string fileName, CoreInfo? coreInfo, string configPath, bool displayLog)
    {
        var environmentVars = new Dictionary<string, string>();
        foreach (var kv in coreInfo.Environment)
        {
            environmentVars[kv.Key] = string.Format(kv.Value, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath);
        }

        var procService = new ProcessService(
            fileName: fileName,
            arguments: string.Format(coreInfo.Arguments, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath),
            workingDirectory: Utils.GetBinConfigPath(),
            displayLog: displayLog,
            redirectInput: false,
            environmentVars: environmentVars,
            updateFunc: _updateFunc
        );

        await procService.StartAsync();

        await Task.Delay(100);

        if (procService is null or { HasExited: true })
        {
            throw new Exception(ResUI.FailedToRunCore);
        }
        AddProcessJob(procService.Handle);

        return procService;
    }

    private void AddProcessJob(nint processHandle)
    {
        if (Utils.IsWindows())
        {
            _processJob ??= new();
            try
            {
                _processJob?.AddProcess(processHandle);
            }
            catch { }
        }
    }

    #endregion Process
}
