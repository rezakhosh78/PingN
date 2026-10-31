using System.Text.Json;

namespace ServiceLib.ViewModels;

public partial class AddServer2ViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    public Interaction<RxVoid, string?> BrowseConfigFileInteraction { get; } = new();

    [Reactive]
    public partial ProfileItem SelectedSource { get; set; }

    [Reactive]
    public partial string? CoreType { get; set; }

    [Reactive]
    public partial bool IsSingboxEndpoint { get; set; }

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
    public bool IsPingNFeatureAvailable => IsWindows && SelectedSource?.ConfigType == EConfigType.Custom;
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
    public bool IsSniSpoofingFieldsEnabled => SniSpoofingPlusEnabled;
    public IReadOnlyList<string> DesyncProfileOptions { get; } =
    [
        "Off", "Speed - Light split", "Balanced - BadSeq", "Severe filtering - BadSeq",
        "SNI spoof - WrongSeq", "Severe filtering - AutoTTL", "Custom"
    ];
    public IReadOnlyList<string> DesyncSplitModeOptions { get; } = ["multisplit", "multidisorder"];
    public IReadOnlyList<string> DesyncFoolingOptions { get; } = ["none", "badseq", "badsum", "md5sig", "ttl"];
    public IReadOnlyList<string> SniSpoofingPlusUtlsOptions { get; } = ["firefox", "chrome", "edge", "safari", "ios", "qq", "360browser", "rotate", "none"];

    public ReactiveCommand<RxVoid, RxVoid> BrowseServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> EditServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveServerCmd { get; }
    public bool IsModified { get; set; }

    public AddServer2ViewModel(ProfileItem profileItem)
    {
        _config = AppManager.Instance.Config;

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
        BrowseServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var fileName = await BrowseConfigFileInteraction.HandleSafe(RxVoid.Default);
            if (fileName.IsNullOrEmpty())
            {
                return;
            }
            await BrowseServer(fileName);
        });
        EditServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditServer();
        });
        SaveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveServerAsync();
        });

        SelectedSource = profileItem.IndexId.IsNullOrEmpty() ? profileItem : JsonUtils.DeepCopy(profileItem);
        var coreStr = SelectedSource?.CoreType?.ToString();
        coreStr = coreStr.IsNullOrEmpty() ? Global.CoreTypes.FirstOrDefault() : coreStr;
        CoreType = coreStr;
        IsSingboxEndpoint = SelectedSource?.GetProtocolExtra()?.IsSingboxEndpoint ?? false;
        var extra = SelectedSource.GetProtocolExtra();
        PingNDesyncSettings? desync = null;
        if (!string.IsNullOrWhiteSpace(extra.PingNDesyncJson))
        {
            try { desync = JsonSerializer.Deserialize<PingNDesyncSettings>(extra.PingNDesyncJson); }
            catch (JsonException ex) { Logging.SaveLog("AddServer2ViewModel", ex); }
        }
        DesyncProfile = NormalizeDesyncProfile(desync?.Profile);
        DesyncSplitMode = desync?.SplitMode ?? "multisplit";
        DesyncSplitPositions = desync?.SplitPositions ?? "1,midsld";
        DesyncFooling = desync?.Fooling ?? "badseq";
        DesyncBadSequence = (desync?.BadSequence ?? -10000).ToString();
        DesyncRepeats = (desync?.Repeats ?? 1).ToString();
        DesyncAutoTtlDelta = (desync?.AutoTtlDelta ?? 2).ToString();
        DesyncAutoTtlMin = (desync?.AutoTtlMin ?? 3).ToString();
        DesyncAutoTtlMax = (desync?.AutoTtlMax ?? 20).ToString();
        DesyncFakeSni = desync?.FakeSni ?? string.Empty;
        SniSpoofingPlusEnabled = extra.SniSpoofingPlusEnabled == true;
        SniSpoofingPlusFakeSni = extra.SniSpoofingPlusFakeSni ?? PingNSniSpoofingSettings.DefaultFakeSni;
        var sniSettings = JsonUtils.Deserialize<PingNSniSpoofingSettings>(extra.PingNSniSpoofingJson) ?? new();
        var legacyDefaults = PingNSniSpoofingSettings.IsUntouchedLegacyDefault(sniSettings, extra.SniSpoofingPlusFakeSni);
        if (legacyDefaults) sniSettings = new();
        SniSpoofingPlusUtls = legacyDefaults ? "rotate" : extra.SniSpoofingPlusUtls.NullIfEmpty() ?? "rotate";
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

        this.WhenAnyValue(x => x.DesyncProfile)
            .Skip(1).Subscribe(_ => { ApplyDesyncPreset(); NotifyPingNFeatureState(); });
        this.WhenAnyValue(x => x.DesyncFooling)
            .Subscribe(_ => NotifyPingNFeatureState());
        this.WhenAnyValue(x => x.SniSpoofingPlusEnabled)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsSniSpoofingFieldsEnabled)));
        this.WhenAnyValue(x => x.SelectedSource)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsPingNFeatureAvailable)));
        this.WhenAnyValue(x => x.CoreType)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsPingNFeatureAvailable));
                this.RaisePropertyChanged(nameof(IsXrayCoreSelected));
            });
    }

    private async Task SaveServerAsync()
    {
        var remarks = SelectedSource.Remarks;
        if (remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        if (SelectedSource.Address.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.FillServerAddressCustom);
            return;
        }
        if (!IsXrayCoreSelected &&
            (!string.Equals(DesyncProfile, "Off", StringComparison.OrdinalIgnoreCase) || SniSpoofingPlusEnabled))
        {
            NoticeManager.Instance.Enqueue("PingN Desync and SNI Spoofing Plus require the Xray core. Select Xray or turn both features off before saving.");
            return;
        }
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : Enum.Parse<ECoreType>(CoreType);
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
        SelectedSource.SetProtocolExtra(SelectedSource?.GetProtocolExtra() with
        {
            IsSingboxEndpoint = IsSingboxEndpoint ? true : null,
            PingNDesyncJson = JsonSerializer.Serialize(desyncSettings),
            SniSpoofingPlusEnabled = SniSpoofingPlusEnabled,
            PingNSniSpoofingJson = JsonSerializer.Serialize(sniOptions),
            SniSpoofingPlusFakeSni = normalizedFakeSni,
            SniSpoofingPlusUtls = SniSpoofingPlusUtls.NullIfEmpty() ?? "rotate",
        });

        if (await ConfigHandler.EditCustomServer(_config, SelectedSource) == 0)
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

    public async Task BrowseServer(string fileName)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }

        var item = await AppManager.Instance.GetProfileItem(SelectedSource.IndexId);
        item ??= SelectedSource;
        item.Address = fileName;
        var result = item.ConfigType == EConfigType.Outbound ? await ConfigHandler.AddCustomOutboundServer(_config, item, false) : await ConfigHandler.AddCustomServer(_config, item, false);
        if (result == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.SuccessfullyImportedCustomServer);
            if (item.IndexId.IsNotEmpty())
            {
                SelectedSource = JsonUtils.DeepCopy(item);
            }
            IsModified = true;
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.FailedImportedCustomServer);
        }
    }

    private async Task EditServer()
    {
        var address = SelectedSource.Address;
        if (address.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.FillServerAddressCustom);
            return;
        }

        address = Utils.GetConfigPath(address);
        if (File.Exists(address))
        {
            ProcUtils.ProcessStart(address);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.FailedReadConfiguration);
        }
        await Task.CompletedTask;
    }
}
