using System.Text.Json;

namespace ServiceLib.ViewModels;

public partial class AddServerViewModel : MyReactiveObject, ICloseable
{
    public ObservableCollection<WarpRegistrationProxyChoice> WarpRegistrationProxies { get; } = [new(WarpRegistrationCoordinator.Auto, "Auto"), new(WarpRegistrationCoordinator.Direct, "Direct")];
    [Reactive] public partial WarpRegistrationProxyChoice? SelectedWarpRegistrationProxy { get; set; }
    [Reactive] public partial string WarpKeyStatus { get; set; } = "";
    [Reactive] public partial bool IsGeneratingWarpKey { get; set; }
    public ReactiveCommand<RxVoid, RxVoid> GenerateWarpKeyCmd { get; }

    private async Task LoadWarpRegistrationProxiesAsync()
    {
        try
        {
            await WarpRegistrationCoordinator.EnsureProxy1Async();
            foreach (var profile in await AppManager.Instance.ProfileItems("") ?? [])
                if (profile.IndexId != SelectedSource.IndexId && WarpRegistrationProxySession.IsSupported(profile))
                    WarpRegistrationProxies.Add(new(profile.IndexId, profile.Remarks));
            var id = SelectedSource.GetProtocolExtra().WarpRegistrationProxyId;
            SelectedWarpRegistrationProxy = WarpRegistrationProxies.FirstOrDefault(x => x.Id == id) ?? WarpRegistrationProxies[0];
        }
        catch (Exception ex) { WarpKeyStatus = $"Cannot load proxy profiles: {ex.Message}"; }
    }

    private CancellationTokenSource? _warpKeyCancellation;
    public void CancelWarpKeyGeneration() => _warpKeyCancellation?.Cancel();

    private async Task GenerateWarpKeyAsync()
    {
        if (IsGeneratingWarpKey) return;
        IsGeneratingWarpKey = true;
        WarpKeyStatus = "Generating a new WARP key...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            _warpKeyCancellation = timeout;
            if (IsWarpAmneziaProfile)
            {
                var generated = await WarpRegistrationCoordinator.RunAsync(SelectedWarpRegistrationProxy?.Id,
                    (url, token) => OberonRuntimeManager.CreateProfileAsync(SelectedSource.Subid, token, url), timeout.Token);
                WarpAmneziaConfig = generated.GetProtocolExtra().WarpAmneziaConfig ?? "";
                LoadWarpAmneziaEditorFields(WarpAmneziaConfig);
            }
            else if (IsWarpMasqueProfile)
            {
                var clone = JsonUtils.DeepCopy(SelectedSource);
                clone.SetProtocolExtra(clone.GetProtocolExtra() with { WarpMasqueDeviceName = WarpMasqueDeviceName, WarpRegistrationProxyId = SelectedWarpRegistrationProxy?.Id });
                var json = await WarpMasqueRuntimeManager.GenerateRegistrationAsync(clone, timeout.Token);
                SelectedSource.SetProtocolExtra(SelectedSource.GetProtocolExtra() with { WarpMasqueConfigJson = json });
            }
            WarpKeyStatus = "New WARP credentials received. Temporary proxy stopped. Save to use them.";
        }
        catch (Exception ex) { WarpKeyStatus = $"WARP registration failed: {ex.Message}"; Logging.SaveLog("WARP registration failed", ex); }
        finally { _warpKeyCancellation = null; IsGeneratingWarpKey = false; }
    }

    public event EventHandler? RequestClose;
    public DnsTunnelSettings? DnsTunnel { get; private set; }

    [Reactive]
    public partial bool PsiphonOver { get; set; }

    [Reactive]
    public partial PsiphonRegionOption? SelectedPsiphonRegion { get; set; }

    public IReadOnlyList<PsiphonRegionOption> PsiphonRegions => PsiphonRegionOption.All;

    [Reactive]
    public partial ProfileItem SelectedSource { get; set; }

    [Reactive]
    public partial string? CoreType { get; set; }

    [Reactive]
    public partial bool AllowInsecure { get; set; }

    [Reactive]
    public partial bool MuxEnabled { get; set; }

    [Reactive]
    public partial string Cert { get; set; }

    [Reactive]
    public partial string CertTip { get; set; }

    [Reactive]
    public partial string CertSha { get; set; }

    [Reactive]
    public partial string SalamanderPass { get; set; }

    [Reactive]
    public partial int AlterId { get; set; }

    [Reactive]
    public partial string Ports { get; set; }

    [Reactive]
    public partial int? UpMbps { get; set; }

    [Reactive]
    public partial int? DownMbps { get; set; }

    [Reactive]
    public partial string HopInterval { get; set; }

    [Reactive]
    public partial string Flow { get; set; }

    [Reactive]
    public partial string VmessSecurity { get; set; }

    [Reactive]
    public partial string VlessEncryption { get; set; }

    [Reactive]
    public partial string SsMethod { get; set; }

    [Reactive]
    public partial string WgPublicKey { get; set; }

    [Reactive]
    public partial string WgPresharedKey { get; set; }

    [Reactive]
    public partial string WgInterfaceAddress { get; set; }

    [Reactive]
    public partial string WgReserved { get; set; }

    [Reactive]
    public partial int WgMtu { get; set; }

    [Reactive]
    public partial string WgDns { get; set; }

    [Reactive]
    public partial bool Uot { get; set; }

    [Reactive]
    public partial string WarpAmneziaConfig { get; set; }

    [Reactive]
    public partial string WarpAmneziaScanFamily { get; set; }

    [Reactive]
    public partial string WarpAmneziaScanMode { get; set; }

    [Reactive]
    public partial bool WarpAmneziaAutoScan { get; set; }

    public IReadOnlyList<string> WarpAmneziaScanFamilyOptions { get; } = ["IPv4", "IPv6"];
    public IReadOnlyList<string> WarpAmneziaScanModeOptions { get; } = ["Fast", "All"];

    [Reactive] public partial string WarpAwgPrivateKey { get; set; }
    [Reactive] public partial string WarpAwgAddress { get; set; }
    [Reactive] public partial string WarpAwgDns { get; set; }
    [Reactive] public partial string WarpAwgMtu { get; set; }
    [Reactive] public partial string WarpAwgListenPort { get; set; }
    [Reactive] public partial string WarpAwgJc { get; set; }
    [Reactive] public partial string WarpAwgJmin { get; set; }
    [Reactive] public partial string WarpAwgJmax { get; set; }
    [Reactive] public partial string WarpAwgS1 { get; set; }
    [Reactive] public partial string WarpAwgS2 { get; set; }
    [Reactive] public partial string WarpAwgH1 { get; set; }
    [Reactive] public partial string WarpAwgH2 { get; set; }
    [Reactive] public partial string WarpAwgH3 { get; set; }
    [Reactive] public partial string WarpAwgH4 { get; set; }
    [Reactive] public partial string WarpAwgPublicKey { get; set; }
    [Reactive] public partial string WarpAwgPresharedKey { get; set; }
    [Reactive] public partial string WarpAwgAllowedIPs { get; set; }
    [Reactive] public partial string WarpAwgEndpoint { get; set; }
    [Reactive] public partial string WarpAwgPersistentKeepalive { get; set; }

    [Reactive] public partial string WarpMasqueEndpoint { get; set; }
    [Reactive] public partial string WarpMasqueEndpointPort { get; set; }
    [Reactive] public partial string WarpMasqueEndpointCandidates { get; set; }
    [Reactive] public partial bool WarpMasqueAutoScan { get; set; }
    [Reactive] public partial string WarpMasqueDeviceName { get; set; }
    [Reactive] public partial string WarpMasqueSni { get; set; }
    [Reactive] public partial string WarpMasqueDns { get; set; }
    [Reactive] public partial bool WarpMasqueHttp2Enabled { get; set; }
    [Reactive] public partial string WarpMasqueScanStatus { get; set; }
    [Reactive] public partial string ScannedWarpMasqueEndpoint { get; set; }
    [Reactive] public partial bool IsWarpMasqueScanning { get; set; }
    [Reactive] public partial bool HasScannedWarpMasqueEndpoint { get; set; }

    [Reactive]
    public partial string ScannedWarpAmneziaEndpoint { get; set; }

    [Reactive]
    public partial string WarpAmneziaScanStatus { get; set; }

    [Reactive]
    public partial bool IsWarpAmneziaScanning { get; set; }

    [Reactive]
    public partial bool HasScannedWarpAmneziaEndpoint { get; set; }

    [Reactive]
    public partial string CongestionControl { get; set; }

    [Reactive]
    public partial int? InsecureConcurrency { get; set; }

    [Reactive]
    public partial bool NaiveQuic { get; set; }

    [Reactive]
    public partial string HttpHeadersJson { get; set; }

    [Reactive]
    public partial string Hy2RealmUrl { get; set; }

    [Reactive]
    public partial int GeckoMinPacketSize { get; set; }

    [Reactive]
    public partial int GeckoMaxPacketSize { get; set; }

    [Reactive]
    public partial string MasquePath { get; set; }

    [Reactive]
    public partial string MasqueHeaders { get; set; }

    [Reactive]
    public partial string RawHeaderType { get; set; }

    [Reactive]
    public partial string Host { get; set; }

    [Reactive]
    public partial string Path { get; set; }

    [Reactive]
    public partial string XhttpMode { get; set; }

    [Reactive]
    public partial string XhttpExtra { get; set; }

    [Reactive]
    public partial string GrpcAuthority { get; set; }

    [Reactive]
    public partial string GrpcServiceName { get; set; }

    [Reactive]
    public partial string GrpcMode { get; set; }

    [Reactive]
    public partial string KcpHeaderType { get; set; }

    [Reactive]
    public partial string KcpSeed { get; set; }

    [Reactive]
    public partial int? KcpMtu { get; set; }

    [Reactive]
    public partial string DesyncProfile { get; set; }

    [Reactive]
    public partial string DesyncSplitMode { get; set; }

    [Reactive]
    public partial string DesyncSplitPositions { get; set; }

    [Reactive]
    public partial string DesyncFooling { get; set; }

    [Reactive]
    public partial string DesyncBadSequence { get; set; }

    [Reactive]
    public partial string DesyncRepeats { get; set; }

    [Reactive]
    public partial string DesyncAutoTtlDelta { get; set; }

    [Reactive]
    public partial string DesyncAutoTtlMin { get; set; }

    [Reactive]
    public partial string DesyncAutoTtlMax { get; set; }

    [Reactive]
    public partial string DesyncFakeSni { get; set; }

    [Reactive]
    public partial bool SniSpoofingPlusEnabled { get; set; }

    [Reactive]
    public partial string SniSpoofingPlusFakeSni { get; set; }

    [Reactive]
    public partial string SniSpoofingPlusUtls { get; set; }

    public bool IsWindows => Utils.IsWindows();
    public bool IsWarpAmneziaProfile => SelectedSource is not null &&
        string.Equals(SelectedSource.GetProtocolExtra().WarpProfileType, OberonRuntimeManager.WarpProfileType, StringComparison.OrdinalIgnoreCase);
    public bool IsWarpMasqueProfile => SelectedSource is not null &&
        string.Equals(SelectedSource.GetProtocolExtra().WarpProfileType, WarpMasqueRuntimeManager.WarpProfileType, StringComparison.OrdinalIgnoreCase);
    public bool IsPingNFeatureAvailable => IsWindows && SelectedSource is not null && !IsWarpAmneziaProfile && !IsWarpMasqueProfile &&
        SelectedSource.ConfigType is not (EConfigType.WireGuard or EConfigType.MASQUE or EConfigType.Outbound or EConfigType.PolicyGroup or EConfigType.ProxyChain);
    public bool IsXrayCoreSelected
    {
        get
        {
            if (Enum.TryParse<ECoreType>(CoreType, true, out var selectedCore))
            {
                return selectedCore == ECoreType.Xray;
            }

            var configuredCore = _config?.CoreTypeItem?
                .FirstOrDefault(item => item.ConfigType == SelectedSource.ConfigType)?.CoreType
                ?? ECoreType.Xray;
            return configuredCore == ECoreType.Xray;
        }
    }
    public bool IsDesyncCustom => !string.Equals(DesyncProfile, "Off", StringComparison.OrdinalIgnoreCase);
    public bool IsDesyncBadSequence => IsDesyncCustom && string.Equals(DesyncFooling, "badseq", StringComparison.OrdinalIgnoreCase);
    public bool IsDesyncTtl => IsDesyncCustom && string.Equals(DesyncFooling, "ttl", StringComparison.OrdinalIgnoreCase);
    public bool IsDesyncRepeats => IsDesyncCustom && !string.Equals(DesyncFooling, "none", StringComparison.OrdinalIgnoreCase);
    [Reactive]
    public partial string SniListenPort { get; set; }

    [Reactive]
    public partial string SniConnect { get; set; }

    [Reactive]
    public partial string SniFakeRepeat { get; set; }

    [Reactive]
    public partial string SniFakeDelay { get; set; }

    [Reactive]
    public partial string SniAckTimeout { get; set; }

    [Reactive]
    public partial string SniInjector { get; set; }

    [Reactive]
    public partial bool SniEnableFragment { get; set; }

    [Reactive]
    public partial string SniFragmentDelay { get; set; }

    [Reactive]
    public partial string SniSniChunk { get; set; }


    [Reactive] public partial string SniRouteMode { get; set; }
    [Reactive] public partial string SniIpRouting { get; set; }
    [Reactive] public partial string SniSniRouting { get; set; }
    public System.Collections.ObjectModel.ObservableCollection<DesyncScanResult> DesyncScanResults { get; } = [];
    [Reactive] public partial DesyncScanResult? SelectedDesyncScanResult { get; set; }
    [Reactive] public partial string DesyncScanStatus { get; set; }
    [Reactive] public partial bool IsDesyncScanning { get; set; }
    public ReactiveCommand<RxVoid, RxVoid> ScanDesyncCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ApplyDesyncScanCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ScanWarpAmneziaEndpointCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ApplyWarpAmneziaEndpointCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ScanWarpMasqueEndpointCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ApplyWarpMasqueEndpointCmd { get; }
    public bool IsSniSpoofingFieldsEnabled => SniSpoofingPlusEnabled;
    public IReadOnlyList<string> DesyncProfileOptions { get; } =
    [
        "Off",
        "Speed - Light split",
        "Balanced - BadSeq",
        "Severe filtering - BadSeq",
        "SNI spoof - WrongSeq",
        "Severe filtering - AutoTTL",
        "Custom"
    ];
    public IReadOnlyList<string> DesyncSplitModeOptions { get; } = ["multisplit", "multidisorder"];
    public IReadOnlyList<string> DesyncFoolingOptions { get; } = ["none", "badseq", "badsum", "md5sig", "ttl"];
    public IReadOnlyList<string> SniSpoofingPlusUtlsOptions { get; } = ["firefox", "chrome", "edge", "safari", "ios", "qq", "360browser", "rotate", "none"];

    public string TransportHeaderType
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.raw) => RawHeaderType,
            nameof(ETransport.kcp) => KcpHeaderType,
            nameof(ETransport.xhttp) => XhttpMode,
            nameof(ETransport.grpc) => GrpcMode,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
            {
                case nameof(ETransport.raw):
                    RawHeaderType = value;
                    break;

                case nameof(ETransport.kcp):
                    KcpHeaderType = value;
                    break;

                case nameof(ETransport.xhttp):
                    XhttpMode = value;
                    break;

                case nameof(ETransport.grpc):
                    GrpcMode = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportHost
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.raw) => Host,
            nameof(ETransport.ws) => Host,
            nameof(ETransport.httpupgrade) => Host,
            nameof(ETransport.xhttp) => Host,
            nameof(ETransport.grpc) => GrpcAuthority,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
            {
                case nameof(ETransport.raw):
                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                case nameof(ETransport.xhttp):
                    Host = value;
                    break;

                case nameof(ETransport.grpc):
                    GrpcAuthority = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportPath
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.kcp) => KcpSeed,
            nameof(ETransport.ws) => Path,
            nameof(ETransport.httpupgrade) => Path,
            nameof(ETransport.xhttp) => Path,
            nameof(ETransport.grpc) => GrpcServiceName,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
            {
                case nameof(ETransport.kcp):
                    KcpSeed = value;
                    break;

                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                case nameof(ETransport.xhttp):
                    Path = value;
                    break;

                case nameof(ETransport.grpc):
                    GrpcServiceName = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportExtraText
    {
        get => SelectedSource.GetNetwork() == nameof(ETransport.xhttp)
            ? XhttpExtra
            : string.Empty;
        set
        {
            if (SelectedSource.GetNetwork() == nameof(ETransport.xhttp))
            {
                XhttpExtra = value;
            }
            this.RaisePropertyChanged();
        }
    }

    public ReactiveCommand<RxVoid, RxVoid> FetchCertCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> FetchCertChainCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    public AddServerViewModel(ProfileItem profileItem)
    {
        _config = AppManager.Instance.Config;
        GenerateWarpKeyCmd = ReactiveCommand.CreateFromTask(GenerateWarpKeyAsync, this.WhenAnyValue(x => x.IsGeneratingWarpKey).Select(x => !x));

        ScanWarpAmneziaEndpointCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!IsWarpAmneziaProfile || IsWarpAmneziaScanning) return;
            IsWarpAmneziaScanning = true;
            HasScannedWarpAmneziaEndpoint = false;
            ScannedWarpAmneziaEndpoint = string.Empty;
            var scanFamily = OberonRuntimeManager.NormalizeScanFamily(WarpAmneziaScanFamily);
            var scanMode = OberonRuntimeManager.NormalizeScanMode(WarpAmneziaScanMode);
            WarpAmneziaScanStatus = $"Scanning Oberon {scanFamily.ToUpperInvariant()} {scanMode} endpoint pool...";
            try
            {
                var result = await OberonRuntimeManager.ScanFastestEndpointAsync(BuildWarpAmneziaConfig(), scanMode, scanFamily);
                ScannedWarpAmneziaEndpoint = result.Endpoint;
                HasScannedWarpAmneziaEndpoint = true;
                WarpAmneziaScanStatus = $"Scan selected {result.Endpoint}. Apply the endpoint, then save the profile.";
            }
            catch (Exception ex)
            {
                Logging.SaveLog("WARP AmneziaWG endpoint scan failed", ex);
                WarpAmneziaScanStatus = $"Endpoint scan failed: {ex.Message}";
            }
            finally
            {
                IsWarpAmneziaScanning = false;
            }
        });
        ApplyWarpAmneziaEndpointCmd = ReactiveCommand.Create(() =>
        {
            if (!HasScannedWarpAmneziaEndpoint) return;
            try
            {
                var updated = OberonRuntimeManager.ApplyScannedEndpoint(BuildWarpAmneziaConfig(), ScannedWarpAmneziaEndpoint);
                WarpAwgEndpoint = WarpAmneziaConfigCodec.ReadFields(updated).GetValueOrDefault("Peer.Endpoint", string.Empty);
                HasScannedWarpAmneziaEndpoint = false;
                WarpAmneziaScanStatus = $"Applied {ScannedWarpAmneziaEndpoint} to the configuration. Save the profile to keep it.";
            }
            catch (Exception ex)
            {
                Logging.SaveLog("WARP AmneziaWG endpoint apply failed", ex);
                WarpAmneziaScanStatus = $"Could not apply the scanned endpoint: {ex.Message}";
            }
        });
        ScanWarpMasqueEndpointCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!IsWarpMasqueProfile || IsWarpMasqueScanning) return;
            IsWarpMasqueScanning = true;
            HasScannedWarpMasqueEndpoint = false;
            ScannedWarpMasqueEndpoint = string.Empty;
            WarpMasqueScanStatus = "Scanning WARP MASQUE/H2 endpoint candidates...";
            try
            {
                var result = await WarpMasqueRuntimeManager.ScanFastestEndpointAsync(WarpMasqueEndpointCandidates);
                ScannedWarpMasqueEndpoint = result.Endpoint;
                HasScannedWarpMasqueEndpoint = true;
                WarpMasqueScanStatus = $"Fastest reachable endpoint: {result.Endpoint}. Apply it, then save the profile.";
            }
            catch (Exception ex)
            {
                Logging.SaveLog("WARP MASQUE/H2 endpoint scan failed", ex);
                WarpMasqueScanStatus = $"Endpoint scan failed: {ex.Message}";
            }
            finally
            {
                IsWarpMasqueScanning = false;
            }
        });
        ApplyWarpMasqueEndpointCmd = ReactiveCommand.Create(() =>
        {
            if (!HasScannedWarpMasqueEndpoint) return;
            try
            {
                var endpoint = WarpMasqueRuntimeManager.SplitEndpoint(ScannedWarpMasqueEndpoint,
                    int.TryParse(WarpMasqueEndpointPort, out var port) ? port : WarpMasqueRuntimeManager.DefaultEndpointPort);
                WarpMasqueEndpoint = endpoint[0];
                WarpMasqueEndpointPort = endpoint[1];
                WarpMasqueAutoScan = false;
                HasScannedWarpMasqueEndpoint = false;
                WarpMasqueScanStatus = $"Applied {ScannedWarpMasqueEndpoint}. Save the profile to keep it.";
            }
            catch (Exception ex)
            {
                WarpMasqueScanStatus = $"Could not apply the scanned endpoint: {ex.Message}";
            }
        });

        ScanDesyncCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (IsDesyncScanning) return;
            IsDesyncScanning = true;
            DesyncScanResults.Clear();
            SelectedDesyncScanResult = null;
            try
            {
                var progress = new Progress<string>(message => DesyncScanStatus = message);
                if (!PingNDesyncSettings.TryCreateFromEditor(
                        NormalizeDesyncProfile(DesyncProfile), DesyncSplitMode, DesyncSplitPositions, DesyncFooling,
                        DesyncBadSequence, DesyncRepeats, DesyncAutoTtlDelta, DesyncAutoTtlMin, DesyncAutoTtlMax,
                        DesyncFakeSni, out var selectedSettings, out var scanError))
                    throw new InvalidOperationException(scanError);
                var results = await DesyncPresetScanner.ScanAsync(SelectedSource, _config, selectedSettings, progress);
                foreach (var result in results) DesyncScanResults.Add(result);
                SelectedDesyncScanResult = results.Where(item => item.Delay > 0).OrderBy(item => item.Delay).FirstOrDefault();
                DesyncScanStatus = SelectedDesyncScanResult is null
                    ? "No working Desync preset found."
                    : $"Best: {SelectedDesyncScanResult}. Select a result and press Apply, then Save.";
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Desync scan failed", ex);
                DesyncScanStatus = $"Desync scan failed: {ex.Message}";
            }
            finally { IsDesyncScanning = false; }
        });
        ApplyDesyncScanCmd = ReactiveCommand.Create(() =>
        {
            if (SelectedDesyncScanResult is null) return;
            DesyncProfile = SelectedDesyncScanResult.Profile;
            ApplyDesyncSettings(SelectedDesyncScanResult.Settings);
            DesyncScanStatus = $"Applied {DesyncProfile} in the editor. Press Save to keep it.";
        });
        FetchCertCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FetchCert();
        });
        FetchCertChainCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FetchCertChain();
        });
        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveServerAsync();
        });
        this.WhenAnyValue(x => x.Cert)
            .Subscribe(_ => UpdateCertTip());

        this.WhenAnyValue(x => x.CertSha)
            .Subscribe(_ => UpdateCertTip());

        this.WhenAnyValue(x => x.SelectedSource.Network)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TransportHeaderType));
                this.RaisePropertyChanged(nameof(TransportHost));
                this.RaisePropertyChanged(nameof(TransportPath));
                this.RaisePropertyChanged(nameof(TransportExtraText));
            });

        this.WhenAnyValue(x => x.Cert)
            .Subscribe(_ => UpdateCertSha());

        if (profileItem.IndexId.IsNullOrEmpty())
        {
            profileItem.Network = Global.DefaultNetwork;
            profileItem.StreamSecurity = "";
            SelectedSource = profileItem;
        }
        else
        {
            SelectedSource = JsonUtils.DeepCopy(profileItem);
        }
        CoreType = SelectedSource?.CoreType?.ToString();
        AllowInsecure = SelectedSource?.GetAllowInsecure() == true;
        MuxEnabled = SelectedSource?.MuxEnabled == true;
        Cert = SelectedSource?.Cert ?? string.Empty;
        CertSha = SelectedSource?.CertSha ?? string.Empty;

        var protocolExtra = SelectedSource?.GetProtocolExtra() ?? new();
        SelectedWarpRegistrationProxy = WarpRegistrationProxies[0];
        if (IsWarpAmneziaProfile || IsWarpMasqueProfile) _ = LoadWarpRegistrationProxiesAsync();
        DnsTunnel = protocolExtra.DnsTunnel;
        var transport = SelectedSource?.GetTransportExtra() ?? new();
        WarpAmneziaConfig = protocolExtra.WarpAmneziaConfig ?? string.Empty;
        WarpAmneziaScanFamily = OberonRuntimeManager.NormalizeScanFamily(protocolExtra.WarpAmneziaScanFamily) == "ipv6" ? "IPv6" : "IPv4";
        WarpAmneziaScanMode = OberonRuntimeManager.NormalizeScanMode(protocolExtra.WarpAmneziaScanMode) == "all" ? "All" : "Fast";
        WarpAmneziaAutoScan = protocolExtra.WarpAmneziaAutoScan != false;
        LoadWarpAmneziaEditorFields(WarpAmneziaConfig);
        WarpMasqueEndpoint = protocolExtra.WarpMasqueEndpoint ?? WarpMasqueRuntimeManager.DefaultEndpoint;
        WarpMasqueEndpointPort = (protocolExtra.WarpMasqueEndpointPort ?? WarpMasqueRuntimeManager.DefaultEndpointPort).ToString();
        WarpMasqueEndpointCandidates = protocolExtra.WarpMasqueEndpointCandidates ?? WarpMasqueRuntimeManager.DefaultCandidates;
        WarpMasqueAutoScan = protocolExtra.WarpMasqueAutoScan ?? true;
        WarpMasqueDeviceName = protocolExtra.WarpMasqueDeviceName ?? $"PingN-{Guid.NewGuid():N}"[..20];
        WarpMasqueSni = protocolExtra.WarpMasqueSni ?? WarpMasqueRuntimeManager.DefaultSni;
        WarpMasqueDns = protocolExtra.WarpMasqueDns ?? WarpMasqueRuntimeManager.DefaultDns;
        WarpMasqueHttp2Enabled = protocolExtra.WarpMasqueHttp2Enabled ?? true;
        WarpMasqueScanStatus = string.Empty;
        ScannedWarpMasqueEndpoint = string.Empty;
        Ports = protocolExtra.Ports ?? string.Empty;
        AlterId = int.TryParse(protocolExtra.AlterId, out var result) ? result : 0;
        Flow = protocolExtra.Flow ?? string.Empty;
        SalamanderPass = protocolExtra.SalamanderPass ?? string.Empty;
        UpMbps = protocolExtra.UpMbps;
        DownMbps = protocolExtra.DownMbps;
        HopInterval = protocolExtra.HopInterval ?? string.Empty;
        VmessSecurity = protocolExtra.VmessSecurity?.IsNullOrEmpty() == false ? protocolExtra.VmessSecurity : Global.DefaultSecurity;
        VlessEncryption = protocolExtra.VlessEncryption?.IsNullOrEmpty() == false ? protocolExtra.VlessEncryption : Global.None;
        SsMethod = protocolExtra.SsMethod ?? string.Empty;
        WgPublicKey = protocolExtra.WgPublicKey ?? string.Empty;
        WgPresharedKey = protocolExtra.WgPresharedKey ?? string.Empty;
        WgInterfaceAddress = protocolExtra.WgInterfaceAddress ?? string.Empty;
        WgReserved = protocolExtra.WgReserved ?? string.Empty;
        WgMtu = protocolExtra.WgMtu ?? 1280;
        WgDns = protocolExtra.WgDns ?? string.Empty;
        Uot = protocolExtra.Uot ?? false;
        CongestionControl = protocolExtra.CongestionControl ?? string.Empty;
        InsecureConcurrency = protocolExtra.InsecureConcurrency > 0 ? protocolExtra.InsecureConcurrency : null;
        NaiveQuic = protocolExtra.NaiveQuic ?? false;
        HttpHeadersJson = protocolExtra.HttpHeaders ?? string.Empty;
        Hy2RealmUrl = protocolExtra.Hy2RealmUrl ?? string.Empty;
        GeckoMinPacketSize = protocolExtra.GeckoMinPacketSize.ToInt();
        GeckoMaxPacketSize = protocolExtra.GeckoMaxPacketSize.ToInt();
        MasquePath = protocolExtra.MasquePath ?? string.Empty;
        MasqueHeaders = protocolExtra.MasqueHeaders ?? string.Empty;
        PingNDesyncSettings? desyncSettings = null;
        if (!string.IsNullOrWhiteSpace(protocolExtra.PingNDesyncJson))
        {
            try
            {
                desyncSettings = JsonSerializer.Deserialize<PingNDesyncSettings>(protocolExtra.PingNDesyncJson);
            }
            catch (JsonException ex)
            {
                Logging.SaveLog("AddServerViewModel", ex);
            }
        }
        DesyncProfile = NormalizeDesyncProfile(desyncSettings?.Profile);
        DesyncSplitMode = desyncSettings?.SplitMode ?? "multisplit";
        DesyncSplitPositions = desyncSettings?.SplitPositions ?? "1,midsld";
        DesyncFooling = desyncSettings?.Fooling ?? "badseq";
        DesyncBadSequence = (desyncSettings?.BadSequence ?? -10000).ToString();
        DesyncRepeats = (desyncSettings?.Repeats ?? 1).ToString();
        DesyncAutoTtlDelta = (desyncSettings?.AutoTtlDelta ?? 2).ToString();
        DesyncAutoTtlMin = (desyncSettings?.AutoTtlMin ?? 3).ToString();
        DesyncAutoTtlMax = (desyncSettings?.AutoTtlMax ?? 20).ToString();
        DesyncFakeSni = desyncSettings?.FakeSni ?? string.Empty;
        PsiphonOver = protocolExtra.PsiphonOver;
        SelectedPsiphonRegion = PsiphonRegionOption.All.FirstOrDefault(item => item.Code == protocolExtra.PsiphonEgressRegion) ?? PsiphonRegionOption.All[0];
        SniSpoofingPlusEnabled = protocolExtra.SniSpoofingPlusEnabled == true;
        SniSpoofingPlusFakeSni = protocolExtra.SniSpoofingPlusFakeSni ?? PingNSniSpoofingSettings.DefaultFakeSni;
        var sniSettings = JsonUtils.Deserialize<PingNSniSpoofingSettings>(protocolExtra.PingNSniSpoofingJson) ?? new();
        var legacyDefaults = PingNSniSpoofingSettings.IsUntouchedLegacyDefault(sniSettings, protocolExtra.SniSpoofingPlusFakeSni);
        if (legacyDefaults) sniSettings = new();
        SniSpoofingPlusUtls = legacyDefaults ? "rotate" : protocolExtra.SniSpoofingPlusUtls.NullIfEmpty() ?? "rotate";
        SniListenPort = "auto";
        SniConnect = sniSettings.Connect;
        SniFakeRepeat = sniSettings.FakeRepeat;
        SniFakeDelay = sniSettings.FakeDelay;
        SniAckTimeout = sniSettings.AckTimeout;
        SniInjector = sniSettings.Injector;
        SniEnableFragment = sniSettings.EnableFragment;
        SniFragmentDelay = sniSettings.FragmentDelay;
        SniSniChunk = sniSettings.SniChunk;
        SniRouteMode = sniSettings.RouteMode;
        SniIpRouting = sniSettings.IpRouting;
        SniSniRouting = sniSettings.SniRouting;

        RawHeaderType = transport.RawHeaderType ?? Global.None;
        Host = transport.Host ?? string.Empty;
        Path = transport.Path ?? string.Empty;
        XhttpMode = transport.XhttpMode ?? Global.DefaultXhttpMode;
        XhttpExtra = transport.XhttpExtra ?? string.Empty;
        GrpcAuthority = transport.GrpcAuthority ?? string.Empty;
        GrpcServiceName = transport.GrpcServiceName ?? string.Empty;
        GrpcMode = transport.GrpcMode.IsNullOrEmpty() ? Global.GrpcGunMode : transport.GrpcMode;
        KcpHeaderType = transport.KcpHeaderType.IsNullOrEmpty() ? Global.None : transport.KcpHeaderType;
        KcpSeed = transport.KcpSeed ?? string.Empty;
        KcpMtu = transport.KcpMtu;

        this.WhenAnyValue(x => x.DesyncProfile)
            .Skip(1).Subscribe(_ => { ApplyDesyncPreset(); NotifyPingNFeatureState(); });
        this.WhenAnyValue(x => x.DesyncFooling)
            .Subscribe(_ => NotifyPingNFeatureState());
        this.WhenAnyValue(x => x.SniSpoofingPlusEnabled)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsSniSpoofingFieldsEnabled)));
        this.WhenAnyValue(x => x.SelectedSource)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsWarpAmneziaProfile));
                this.RaisePropertyChanged(nameof(IsWarpMasqueProfile));
                this.RaisePropertyChanged(nameof(IsPingNFeatureAvailable));
            });
        this.WhenAnyValue(x => x.CoreType)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsPingNFeatureAvailable));
                this.RaisePropertyChanged(nameof(IsXrayCoreSelected));
            });
    }

    private void LoadWarpAmneziaEditorFields(string configuration)
    {
        var fields = WarpAmneziaConfigCodec.ReadFields(configuration);
        string Get(string key) => fields.GetValueOrDefault(key, string.Empty);

        WarpAwgPrivateKey = Get("Interface.PrivateKey");
        WarpAwgAddress = Get("Interface.Address");
        WarpAwgDns = Get("Interface.DNS");
        WarpAwgMtu = Get("Interface.MTU");
        WarpAwgListenPort = Get("Interface.ListenPort");
        WarpAwgJc = Get("Interface.Jc");
        WarpAwgJmin = Get("Interface.Jmin");
        WarpAwgJmax = Get("Interface.Jmax");
        WarpAwgS1 = Get("Interface.S1");
        WarpAwgS2 = Get("Interface.S2");
        WarpAwgH1 = Get("Interface.H1");
        WarpAwgH2 = Get("Interface.H2");
        WarpAwgH3 = Get("Interface.H3");
        WarpAwgH4 = Get("Interface.H4");
        WarpAwgPublicKey = Get("Peer.PublicKey");
        WarpAwgPresharedKey = Get("Peer.PresharedKey");
        WarpAwgAllowedIPs = Get("Peer.AllowedIPs");
        WarpAwgEndpoint = Get("Peer.Endpoint");
        WarpAwgPersistentKeepalive = Get("Peer.PersistentKeepalive");
    }

    private Dictionary<string, string> GetWarpAmneziaEditorFields() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Interface.PrivateKey"] = WarpAwgPrivateKey,
        ["Interface.Address"] = WarpAwgAddress,
        ["Interface.DNS"] = WarpAwgDns,
        ["Interface.MTU"] = WarpAwgMtu,
        ["Interface.ListenPort"] = WarpAwgListenPort,
        ["Interface.Jc"] = WarpAwgJc,
        ["Interface.Jmin"] = WarpAwgJmin,
        ["Interface.Jmax"] = WarpAwgJmax,
        ["Interface.S1"] = WarpAwgS1,
        ["Interface.S2"] = WarpAwgS2,
        ["Interface.H1"] = WarpAwgH1,
        ["Interface.H2"] = WarpAwgH2,
        ["Interface.H3"] = WarpAwgH3,
        ["Interface.H4"] = WarpAwgH4,
        ["Peer.PublicKey"] = WarpAwgPublicKey,
        ["Peer.PresharedKey"] = WarpAwgPresharedKey,
        ["Peer.AllowedIPs"] = WarpAwgAllowedIPs,
        ["Peer.Endpoint"] = WarpAwgEndpoint,
        ["Peer.PersistentKeepalive"] = WarpAwgPersistentKeepalive,
    };

    private string BuildWarpAmneziaConfig() => WarpAmneziaConfigCodec.UpdateFields(WarpAmneziaConfig, GetWarpAmneziaEditorFields());

    private async Task SaveServerAsync()
    {
        if (SelectedSource.Remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        if (SelectedSource.Address.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.FillServerAddress);
            return;
        }
        if (DnsTunnel is not null)
        {
            try { DnsTunnel.Validate(SelectedSource.Port); }
            catch (Exception ex) { NoticeManager.Instance.Enqueue(ex.Message); return; }
        }
        if (IsWarpAmneziaProfile)
        {
            try
            {
                var pending = SelectedSource.GetProtocolExtra().WarpAmneziaRegistrationPending;
                var hasKeyFields = !string.IsNullOrWhiteSpace(WarpAwgPrivateKey)
                    || !string.IsNullOrWhiteSpace(WarpAwgPublicKey)
                    || !string.IsNullOrWhiteSpace(WarpAwgEndpoint);
                // Allow scan/label edits before the first online registration.
                if (!pending || hasKeyFields)
                {
                    WarpAmneziaConfig = BuildWarpAmneziaConfig();
                    OberonRuntimeManager.ValidateProfileConfig(WarpAmneziaConfig);
                }
            }
            catch (Exception ex)
            {
                NoticeManager.Instance.Enqueue($"Invalid WARP AmneziaWG configuration: {ex.Message}");
                return;
            }
        }
        if (IsWarpMasqueProfile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(WarpMasqueEndpoint))
                    throw new InvalidDataException("Primary endpoint is required.");
                if (!int.TryParse(WarpMasqueEndpointPort, out var parsedMasquePort) || parsedMasquePort is < 1 or > 65535)
                    throw new InvalidDataException("Endpoint port must be between 1 and 65535.");
                if (WarpMasqueEndpoint.Any(char.IsWhiteSpace))
                    throw new InvalidDataException("Primary endpoint cannot contain spaces.");
                if (string.IsNullOrWhiteSpace(WarpMasqueDeviceName) || WarpMasqueDeviceName.Any(char.IsControl)
                    || string.IsNullOrWhiteSpace(WarpMasqueSni) || WarpMasqueSni.Any(char.IsWhiteSpace))
                    throw new InvalidDataException("Device name and SNI are required.");
                if (WarpMasqueDns.Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length == 0)
                    throw new InvalidDataException("Enter at least one DNS server.");
            }
            catch (Exception ex)
            {
                NoticeManager.Instance.Enqueue($"Invalid WARP MASQUE/H2 settings: {ex.Message}");
                return;
            }
        }
        var port = SelectedSource.Port.ToString();
        if (port.IsNullOrEmpty() || !Utils.IsNumeric(port)
            || SelectedSource.Port <= 0 || SelectedSource.Port >= Global.MaxPort)
        {
            NoticeManager.Instance.Enqueue(ResUI.FillCorrectServerPort);
            return;
        }
        if (SelectedSource.ConfigType == EConfigType.Shadowsocks)
        {
            if (SelectedSource.Password.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.FillPassword);
                return;
            }
            if (SsMethod.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.PleaseSelectEncryption);
                return;
            }
        }
        if (SelectedSource.ConfigType is not EConfigType.SOCKS and not EConfigType.HTTP)
        {
            if (SelectedSource.Password.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.FillUUID);
                return;
            }
        }
        HyRealm? realm = null;
        if (!Hy2RealmUrl.IsNullOrEmpty())
        {
            var realmResult = HyRealm.TryParse(Hy2RealmUrl, out realm);
            if (!realmResult)
            {
                NoticeManager.Instance.Enqueue(ResUI.InvalidHy2RealmUrl);
                return;
            }
        }
        if (HttpHeadersJson.IsNotEmpty() && JsonUtils.ParseJson(HttpHeadersJson) == null)
        {
            NoticeManager.Instance.Enqueue(ResUI.InvalidHttpOutboundHeaders);
            return;
        }
        if (!IsXrayCoreSelected &&
            (!string.Equals(DesyncProfile, "Off", StringComparison.OrdinalIgnoreCase) || SniSpoofingPlusEnabled))
        {
            NoticeManager.Instance.Enqueue("PingN Desync and SNI Spoofing Plus require the Xray core. Select Xray or turn both features off before saving.");
            return;
        }
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : Enum.Parse<ECoreType>(CoreType);
        SelectedSource.AllowInsecure = AllowInsecure ? Global.StringTrue : Global.StringFalse;
        SelectedSource.MuxEnabled = MuxEnabled;
        SelectedSource.Cert = Cert.IsNullOrEmpty() ? string.Empty : Cert;
        SelectedSource.CertSha = CertSha.IsNullOrEmpty() ? string.Empty : CertSha;
        if (!Global.Networks.Contains(SelectedSource.Network))
        {
            SelectedSource.Network = Global.DefaultNetwork;
        }

        var transport = new TransportExtraItem
        {
            RawHeaderType = RawHeaderType.NullIfEmpty(),
            Host = Host.NullIfEmpty(),
            Path = Path.NullIfEmpty(),
            XhttpMode = XhttpMode.NullIfEmpty(),
            XhttpExtra = XhttpExtra.NullIfEmpty(),
            GrpcAuthority = GrpcAuthority.NullIfEmpty(),
            GrpcServiceName = GrpcServiceName.NullIfEmpty(),
            GrpcMode = GrpcMode.NullIfEmpty(),
            KcpHeaderType = KcpHeaderType.NullIfEmpty(),
            KcpSeed = KcpSeed.NullIfEmpty(),
            KcpMtu = KcpMtu > 0 ? KcpMtu : null,
        };

        if (!PingNDesyncSettings.TryCreateFromEditor(
                NormalizeDesyncProfile(DesyncProfile), DesyncSplitMode, DesyncSplitPositions, DesyncFooling,
                DesyncBadSequence, DesyncRepeats, DesyncAutoTtlDelta, DesyncAutoTtlMin, DesyncAutoTtlMax,
                DesyncFakeSni, out var desyncSettings, out var desyncError))
        {
            NoticeManager.Instance.Enqueue(desyncError);
            return;
        }

        var sniOptions = new PingNSniSpoofingSettings
        {
            ListenPort = string.Empty,
            Connect = SniConnect.Trim(),
            FakeRepeat = SniFakeRepeat.Trim(),
            FakeDelay = SniFakeDelay.Trim(),
            AckTimeout = SniAckTimeout.Trim(),
            Injector = SniInjector.Trim(),
            EnableFragment = SniEnableFragment,
            FragmentDelay = SniFragmentDelay.Trim(),
            SniChunk = SniSniChunk.Trim(),
            RouteMode = SniRouteMode,
            IpRouting = SniIpRouting,
            SniRouting = SniSniRouting,
        };
        if (SniSpoofingPlusEnabled && !sniOptions.TryValidate(out var sniError))
        {
            NoticeManager.Instance.Enqueue(sniError);
            return;
        }
        var normalizedFakeSni = SniSpoofingPlusFakeSni.Trim();
        if (SniSpoofingPlusEnabled &&
            !PingNSniSpoofingSettings.TryNormalizeFakeSni(normalizedFakeSni, out normalizedFakeSni, out var fakeSniError))
        {
            NoticeManager.Instance.Enqueue(fakeSniError);
            return;
        }
        if (SniSpoofingPlusEnabled)
        {
            PingNSniSpoofingSettings.TryNormalizeConnect(sniOptions.Connect, out var normalizedConnect, out _);
            sniOptions = sniOptions with { Connect = normalizedConnect };
        }
        SelectedSource.SetProtocolExtra(SelectedSource.GetProtocolExtra() with
        {
            WarpRegistrationProxyId = SelectedWarpRegistrationProxy?.Id.NullIfEmpty(),
            DnsTunnel = DnsTunnel,
            Ports = Ports.NullIfEmpty(),
            AlterId = AlterId > 0 ? AlterId.ToString() : null,
            Flow = Flow.NullIfEmpty(),
            SalamanderPass = SalamanderPass.NullIfEmpty(),
            UpMbps = UpMbps,
            DownMbps = DownMbps,
            HopInterval = HopInterval.NullIfEmpty(),
            VmessSecurity = VmessSecurity.NullIfEmpty(),
            VlessEncryption = VlessEncryption.NullIfEmpty(),
            SsMethod = SsMethod.NullIfEmpty(),
            HttpHeaders = SelectedSource.ConfigType == EConfigType.HTTP ? HttpHeadersJson.NullIfEmpty() : null,
            WgPublicKey = WgPublicKey.NullIfEmpty(),
            WgPresharedKey = WgPresharedKey.NullIfEmpty(),
            WgInterfaceAddress = WgInterfaceAddress.NullIfEmpty(),
            WgReserved = WgReserved.NullIfEmpty(),
            WgMtu = WgMtu >= 576 ? WgMtu : null,
            WgDns = WgDns.NullIfEmpty(),
            WarpAmneziaRegistrationPending = SelectedSource.GetProtocolExtra().WarpAmneziaRegistrationPending
                && string.IsNullOrWhiteSpace(WarpAmneziaConfig),
            WarpAmneziaConfig = IsWarpAmneziaProfile
                ? WarpAmneziaConfig
                : SelectedSource.GetProtocolExtra().WarpAmneziaConfig,
            WarpAmneziaScanFamily = IsWarpAmneziaProfile
                ? OberonRuntimeManager.NormalizeScanFamily(WarpAmneziaScanFamily)
                : SelectedSource.GetProtocolExtra().WarpAmneziaScanFamily,
            WarpAmneziaScanMode = IsWarpAmneziaProfile
                ? OberonRuntimeManager.NormalizeScanMode(WarpAmneziaScanMode)
                : SelectedSource.GetProtocolExtra().WarpAmneziaScanMode,
            WarpAmneziaAutoScan = IsWarpAmneziaProfile
                ? WarpAmneziaAutoScan
                : SelectedSource.GetProtocolExtra().WarpAmneziaAutoScan,
            WarpMasqueEndpoint = IsWarpMasqueProfile ? WarpMasqueEndpoint.Trim() : SelectedSource.GetProtocolExtra().WarpMasqueEndpoint,
            WarpMasqueEndpointPort = IsWarpMasqueProfile && int.TryParse(WarpMasqueEndpointPort, out var masquePortValue)
                ? masquePortValue : SelectedSource.GetProtocolExtra().WarpMasqueEndpointPort,
            WarpMasqueEndpointCandidates = IsWarpMasqueProfile ? WarpMasqueEndpointCandidates.Trim() : SelectedSource.GetProtocolExtra().WarpMasqueEndpointCandidates,
            WarpMasqueAutoScan = IsWarpMasqueProfile ? WarpMasqueAutoScan : SelectedSource.GetProtocolExtra().WarpMasqueAutoScan,
            WarpMasqueDeviceName = IsWarpMasqueProfile ? WarpMasqueDeviceName.Trim() : SelectedSource.GetProtocolExtra().WarpMasqueDeviceName,
            WarpMasqueSni = IsWarpMasqueProfile ? WarpMasqueSni.Trim() : SelectedSource.GetProtocolExtra().WarpMasqueSni,
            WarpMasqueDns = IsWarpMasqueProfile ? WarpMasqueDns.Trim() : SelectedSource.GetProtocolExtra().WarpMasqueDns,
            WarpMasqueHttp2Enabled = IsWarpMasqueProfile ? WarpMasqueHttp2Enabled : SelectedSource.GetProtocolExtra().WarpMasqueHttp2Enabled,
            WarpMasqueConfigJson = SelectedSource.GetProtocolExtra().WarpMasqueConfigJson,
            Uot = Uot ? true : null,
            CongestionControl = CongestionControl.NullIfEmpty(),
            InsecureConcurrency = InsecureConcurrency > 0 ? InsecureConcurrency : null,
            NaiveQuic = NaiveQuic ? true : null,
            Hy2RealmUrl = realm?.ToUri().NullIfEmpty(),
            GeckoMinPacketSize = GeckoMinPacketSize > 0 ? GeckoMinPacketSize.ToString() : null,
            GeckoMaxPacketSize = GeckoMaxPacketSize > 0 ? GeckoMaxPacketSize.ToString() : null,
            MasquePath = MasquePath.NullIfEmpty(),
            MasqueHeaders = MasqueHeaders.NullIfEmpty(),
            WarpProfileType = SelectedSource.GetProtocolExtra().WarpProfileType,
            PingNDesyncJson = JsonSerializer.Serialize(desyncSettings),
            PsiphonOver = PsiphonOver,
            PsiphonEgressRegion = SelectedPsiphonRegion?.Code,
            SniSpoofingPlusEnabled = SniSpoofingPlusEnabled,
            PingNSniSpoofingJson = JsonSerializer.Serialize(sniOptions),
            SniSpoofingPlusFakeSni = normalizedFakeSni,
            SniSpoofingPlusUtls = SniSpoofingPlusUtls.NullIfEmpty() ?? "rotate",
        });
        SelectedSource.SetTransportExtra(transport);

        if (await ConfigHandler.AddServer(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    private static string NormalizeDesyncProfile(string? value) => value?.Trim() switch
    {
        "Speed - Light split" or "light" => "Speed - Light split",
        "Balanced - BadSeq" or "balanced" => "Balanced - BadSeq",
        "Severe filtering - BadSeq" or "severe" => "Severe filtering - BadSeq",
        "SNI spoof - WrongSeq" or "sni" => "SNI spoof - WrongSeq",
        "Severe filtering - AutoTTL" or "autottl" => "Severe filtering - AutoTTL",
        "Custom" or "custom" => "Custom",
        _ => "Off",
    };

    private void ApplyDesyncPreset()
    {
        ApplyDesyncSettings(PingNDesyncSettings.DefaultsFor(DesyncProfile));
    }

    private void ApplyDesyncSettings(PingNDesyncSettings settings)
    {
        DesyncSplitMode = settings.SplitMode;
        DesyncSplitPositions = settings.SplitPositions;
        DesyncFooling = settings.Fooling;
        DesyncBadSequence = settings.BadSequence.ToString();
        DesyncRepeats = settings.Repeats.ToString();
        DesyncAutoTtlDelta = settings.AutoTtlDelta.ToString();
        DesyncAutoTtlMin = settings.AutoTtlMin.ToString();
        DesyncAutoTtlMax = settings.AutoTtlMax.ToString();
        DesyncFakeSni = settings.FakeSni;
    }

    private void NotifyPingNFeatureState()
    {
        this.RaisePropertyChanged(nameof(IsDesyncCustom));
        this.RaisePropertyChanged(nameof(IsDesyncBadSequence));
        this.RaisePropertyChanged(nameof(IsDesyncTtl));
        this.RaisePropertyChanged(nameof(IsDesyncRepeats));
    }

    private void UpdateCertTip(string? errorMessage = null)
    {
        CertTip = errorMessage.IsNullOrEmpty()
            ? ((Cert.IsNullOrEmpty() && CertSha.IsNullOrEmpty()) ? ResUI.CertNotSet : ResUI.CertSet)
            : errorMessage;
    }

    private void UpdateCertSha()
    {
        if (Cert.IsNullOrEmpty())
        {
            return;
        }

        var certList = CertPemManager.ParsePemChain(Cert);
        if (certList.Count == 0)
        {
            return;
        }

        List<string> shaList = [];
        foreach (var cert in certList)
        {
            var sha = CertPemManager.GetCertSha256Thumbprint(cert);
            if (sha.IsNullOrEmpty())
            {
                return;
            }
            shaList.Add(sha);
        }
        CertSha = string.Join(',', shaList);
    }

    private async Task FetchCert()
    {
        if (SelectedSource.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = SelectedSource.Address;
        var serverName = SelectedSource.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = GetCurrentTransportHost();
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (SelectedSource.Port > 0)
        {
            domain += $":{SelectedSource.Port}";
        }

        (Cert, var certError) = await CertPemManager.Instance.GetCertPemAsync(domain, serverName,
            verifyPeerCertByName: Utils.String2List(SelectedSource.VerifyPeerCertByName));
        UpdateCertTip(certError);
    }

    private async Task FetchCertChain()
    {
        if (SelectedSource.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = SelectedSource.Address;
        var serverName = SelectedSource.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = GetCurrentTransportHost();
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (SelectedSource.Port > 0)
        {
            domain += $":{SelectedSource.Port}";
        }

        var (certs, certError) = await CertPemManager.Instance.GetCertChainPemAsync(domain, serverName,
            verifyPeerCertByName: Utils.String2List(SelectedSource.VerifyPeerCertByName));
        Cert = CertPemManager.ConcatenatePemChain(certs);
        UpdateCertTip(certError);
    }

    private string GetCurrentTransportHost()
    {
        return SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.raw) => Host,
            nameof(ETransport.ws) => Host,
            nameof(ETransport.httpupgrade) => Host,
            nameof(ETransport.xhttp) => Host,
            nameof(ETransport.grpc) => GrpcAuthority,
            _ => string.Empty,
        };
    }
}
