using PingN.Desktop.Base;

namespace PingN.Desktop.Views;

public partial class AddServerWindow : WindowBase<AddServerViewModel>
{
    public AddServerWindow()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => Close();
        cmbNetwork.SelectionChanged += CmbNetwork_SelectionChanged;
        cmbHeaderTypeRaw.SelectionChanged += CmbHeaderTypeRaw_SelectionChanged;
        cmbStreamSecurity.SelectionChanged += CmbStreamSecurity_SelectionChanged;
        btnGUID.Click += btnGUID_Click;
        btnGUID5.Click += btnGUID_Click;

        cmbCoreType.ItemsSource = Global.CoreTypes.AppendEmpty();
        cmbNetwork.ItemsSource = Global.Networks;
        cmbWarpAmneziaScanFamily.ItemsSource = new[] { "IPv4", "IPv6" };
        cmbWarpAmneziaScanMode.ItemsSource = new[] { "Fast", "All" };
        cmbDesyncProfile.ItemsSource = new[]
        {
            "Off", "Speed - Light split", "Balanced - BadSeq", "Severe filtering - BadSeq",
            "SNI spoof - WrongSeq", "Severe filtering - AutoTTL", "Custom"
        };
        cmbDesyncSplitMode.ItemsSource = new[] { "multisplit", "multidisorder" };
        cmbDesyncFooling.ItemsSource = new[] { "none", "badseq", "badsum", "md5sig", "ttl" };
        cmbSniSpoofingUtls.ItemsSource = new[] { "firefox", "chrome", "edge", "safari", "ios", "qq", "360browser", "rotate", "none" };
        cmbSniInjector.ItemsSource = new[] { "active", "passive" };
        cmbSniRouteMode.ItemsSource = new[] { "independent", "pair-round-robin", "pair-balanced" };
        cmbSniIpRouting.ItemsSource = new[] { "sticky", "round-robin", "failover" };
        cmbSniSniRouting.ItemsSource = new[] { "sticky", "round-robin" };

        cmbHeaderTypeRaw.ItemsSource = new List<string> { Global.None, Global.RawHeaderHttp };

        var kcpHeaderTypes = new List<string> { Global.None };
        kcpHeaderTypes.AddRange(Global.KcpHeaderTypes);
        cmbHeaderTypeKcp.ItemsSource = kcpHeaderTypes;

        cmbHeaderTypeXhttp.ItemsSource = Global.XhttpMode;
        cmbHeaderTypeGrpc.ItemsSource = new List<string> { Global.GrpcGunMode, Global.GrpcMultiMode };

        cmbFingerprint.ItemsSource = Global.Fingerprints;
        cmbFingerprint2.ItemsSource = Global.Fingerprints;
        cmbAlpn.ItemsSource = Global.Alpns;

        gridTlsMore.IsVisible = false;

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.ViewModel.SelectedSource)
                .KeepNotNull()
                .Subscribe(InitializeData)
                .DisposeWith(disposables);

            var configTypeBindings = new SingleReplaceableDisposable();
            configTypeBindings.DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAmneziaScanStatus, v => v.tbWarpAmneziaScanStatus.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAmneziaScanFamily, v => v.cmbWarpAmneziaScanFamily.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAmneziaScanMode, v => v.cmbWarpAmneziaScanMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAmneziaAutoScan, v => v.chkWarpAmneziaAutoScan.IsChecked).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ScanWarpAmneziaEndpointCmd, v => v.btnScanWarpAmneziaEndpoint).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ApplyWarpAmneziaEndpointCmd, v => v.btnApplyWarpAmneziaEndpoint).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgPrivateKey, v => v.txtWarpAwgPrivateKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgAddress, v => v.txtWarpAwgAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgDns, v => v.txtWarpAwgDns.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgMtu, v => v.txtWarpAwgMtu.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgListenPort, v => v.txtWarpAwgListenPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgJc, v => v.txtWarpAwgJc.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgJmin, v => v.txtWarpAwgJmin.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgJmax, v => v.txtWarpAwgJmax.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgS1, v => v.txtWarpAwgS1.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgS2, v => v.txtWarpAwgS2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgH1, v => v.txtWarpAwgH1.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgH2, v => v.txtWarpAwgH2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgH3, v => v.txtWarpAwgH3.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgH4, v => v.txtWarpAwgH4.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgPublicKey, v => v.txtWarpAwgPublicKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgPresharedKey, v => v.txtWarpAwgPresharedKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgAllowedIPs, v => v.txtWarpAwgAllowedIPs.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgEndpoint, v => v.txtWarpAwgEndpoint.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgPersistentKeepalive, v => v.txtWarpAwgPersistentKeepalive.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueEndpoint, v => v.txtWarpMasqueEndpoint.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueEndpointPort, v => v.txtWarpMasqueEndpointPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueEndpointCandidates, v => v.txtWarpMasqueEndpointCandidates.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueDeviceName, v => v.txtWarpMasqueDeviceName.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueSni, v => v.txtWarpMasqueSni.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueDns, v => v.txtWarpMasqueDns.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueAutoScan, v => v.chkWarpMasqueAutoScan.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueHttp2Enabled, v => v.chkWarpMasqueHttp2Enabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpMasqueScanStatus, v => v.tbWarpMasqueScanStatus.Text).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ScanWarpMasqueEndpointCmd, v => v.btnScanWarpMasqueEndpoint).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ApplyWarpMasqueEndpointCmd, v => v.btnApplyWarpMasqueEndpoint).DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.SelectedSource.ConfigType)
                .Subscribe(configType =>
                {
                    var currentTypeDisposables = new MultipleDisposable();
                    configTypeBindings.Create(currentTypeDisposables);

                    switch (configType)
                    {
                        case EConfigType.VMess:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.AlterId, v => v.txtAlterId.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.VmessSecurity, v => v.cmbSecurity.SelectedValue).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MuxEnabled, v => v.togmuxEnabled.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.Shadowsocks:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId3.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.SsMethod, v => v.cmbSecurity3.SelectedValue).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Uot, v => v.togUotEnabled3.IsChecked).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MuxEnabled, v => v.togmuxEnabled3.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.SOCKS:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId4.Text)
                                .DisposeWith(disposables);
                            this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtSecurity4.Text)
                                .DisposeWith(disposables);
                            break;

                        case EConfigType.HTTP:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId4.Text)
                                .DisposeWith(disposables);
                            this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtSecurity4.Text)
                                .DisposeWith(disposables);
                            this.Bind(ViewModel, vm => vm.HttpHeadersJson, v => v.txtHttpHeadersJson.Text)
                                .DisposeWith(disposables);
                            break;

                        case EConfigType.VLESS:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId5.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow5.SelectedValue).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.VlessEncryption, v => v.txtSecurity5.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MuxEnabled, v => v.togmuxEnabled5.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.Trojan:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId6.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow6.SelectedValue).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MuxEnabled, v => v.togmuxEnabled6.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.Hysteria2:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId7.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.SalamanderPass, v => v.txtPath7.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Ports, v => v.txtPorts7.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.HopInterval, v => v.txtHopInt7.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.UpMbps, v => v.txtUpMbps7.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.DownMbps, v => v.txtDownMbps7.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Hy2RealmUrl, v => v.txtHy2RealmUrl.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.GeckoMinPacketSize, v => v.txtMinGeckoPacketSize.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.GeckoMaxPacketSize, v => v.txtMaxGeckoPacketSize.Text).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.TUIC:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtId8.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtSecurity8.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl8.SelectedValue).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.WireGuard:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId9.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.WgPublicKey, v => v.txtPublicKey9.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.WgPresharedKey, v => v.txtPreSharedKey9.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.WgReserved, v => v.txtPath9.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.WgInterfaceAddress, v => v.txtRequestHost9.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.WgMtu, v => v.txtShortId9.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.WgDns, v => v.txtDns.Text).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.Anytls:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId11.Text).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.Naive:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtId12.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtSecurity12.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.NaiveQuic, v => v.togNaiveQuic12.IsChecked).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.NaiveQuic, v => v.cmbCongestionControl12.IsEnabled).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl12.SelectedValue).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.InsecureConcurrency, v => v.txtInsecureConcurrency12.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Uot, v => v.togUotEnabled12.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.MASQUE:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtId14.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtSecurity14.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MasquePath, v => v.txtPath14.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MasqueHeaders, v => v.txtHttpHeaders14.Text).DisposeWith(currentTypeDisposables);
                            break;
                    }
                })
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncProfile, v => v.cmbDesyncProfile.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedDesyncScanResult, v => v.cmbDesyncScanResult.SelectedItem).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ScanDesyncCmd, v => v.btnScanDesync).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ApplyDesyncScanCmd, v => v.btnApplyDesyncScan).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncSplitMode, v => v.cmbDesyncSplitMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncSplitPositions, v => v.txtDesyncSplitPositions.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncFooling, v => v.cmbDesyncFooling.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncFakeSni, v => v.txtDesyncFakeSni.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncBadSequence, v => v.txtDesyncBadSequence.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncRepeats, v => v.txtDesyncRepeats.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncAutoTtlDelta, v => v.txtDesyncAutoTtlDelta.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncAutoTtlMin, v => v.txtDesyncAutoTtlMin.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncAutoTtlMax, v => v.txtDesyncAutoTtlMax.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniSpoofingPlusEnabled, v => v.togSniSpoofingEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniSpoofingPlusFakeSni, v => v.txtSniSpoofingFakeSni.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniSpoofingPlusUtls, v => v.cmbSniSpoofingUtls.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniListenPort, v => v.txtSniListenPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniConnect, v => v.txtSniConnect.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniFakeRepeat, v => v.txtSniFakeRepeat.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniFakeDelay, v => v.txtSniFakeDelay.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniAckTimeout, v => v.txtSniAckTimeout.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniInjector, v => v.cmbSniInjector.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniEnableFragment, v => v.togSniEnableFragment.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniFragmentDelay, v => v.txtSniFragmentDelay.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniSniChunk, v => v.txtSniSniChunk.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniRouteMode, v => v.cmbSniRouteMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniIpRouting, v => v.cmbSniIpRouting.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SniSniRouting, v => v.cmbSniSniRouting.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RawHeaderType, v => v.cmbHeaderTypeRaw.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostRaw.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathRaw.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.KcpHeaderType, v => v.cmbHeaderTypeKcp.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpSeed, v => v.txtKcpSeed.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpMtu, v => v.txtKcpMtu.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostWs.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathWs.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostHttpupgrade.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathHttpupgrade.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.XhttpMode, v => v.cmbHeaderTypeXhttp.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.XhttpExtra, v => v.txtExtraXhttp.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.GrpcMode, v => v.cmbHeaderTypeGrpc.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcAuthority, v => v.txtRequestHostGrpc.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcServiceName, v => v.txtPathGrpc.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.StreamSecurity, v => v.cmbStreamSecurity.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AllowInsecure, v => v.togAllowInsecure.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Alpn, v => v.cmbAlpn.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertSha, v => v.txtCertSha256Pinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertTip, v => v.labCertPinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.EchConfigList, v => v.txtEchConfigList.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.VerifyPeerCertByName, v => v.txtVerifyPeerCertByName.Text).DisposeWith(disposables);

            //reality
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint2.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PublicKey, v => v.txtPublicKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.ShortId, v => v.txtShortId.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.SpiderX, v => v.txtSpiderX.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Mldsa65Verify, v => v.txtMldsa65Verify.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Finalmask, v => v.txtFinalmask.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.FetchCertCmd, v => v.btnFetchCert).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FetchCertChainCmd, v => v.btnFetchCertChain).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }

    private void InitializeData(ProfileItem profileItem)
    {
        var isWarpAmnezia = string.Equals(
            profileItem.GetProtocolExtra().WarpProfileType,
            OberonRuntimeManager.WarpProfileType,
            StringComparison.OrdinalIgnoreCase);
        var isWarpMasque = string.Equals(
            profileItem.GetProtocolExtra().WarpProfileType,
            WarpMasqueRuntimeManager.WarpProfileType,
            StringComparison.OrdinalIgnoreCase);
        Title = isWarpAmnezia ? "WARP AmneziaWG" : isWarpMasque ? WarpMasqueRuntimeManager.Description : $"{profileItem.ConfigType}";
        tbServerType.Text = Title;
        var lstStreamSecurity = new List<string> { string.Empty, Global.StreamSecurity };
        switch (profileItem.ConfigType)
        {
            case EConfigType.VMess:
                gridVMess.IsVisible = true;
                cmbSecurity.ItemsSource = Global.VmessSecurities;
                break;

            case EConfigType.Shadowsocks:
                gridSs.IsVisible = true;
                cmbSecurity3.ItemsSource = AppManager.Instance.GetShadowsocksSecurities(profileItem);
                break;

            case EConfigType.SOCKS:
                if (isWarpAmnezia || isWarpMasque)
                {
                    gridWarpAmnezia.IsVisible = isWarpAmnezia;
                    gridWarpMasque.IsVisible = isWarpMasque;
                    tbAddress.IsVisible = false;
                    txtAddress.IsVisible = false;
                    tbPort.IsVisible = false;
                    txtPort.IsVisible = false;
                    gridSocks.IsVisible = false;
                    sepa2.IsVisible = false;
                    gridTransport.IsVisible = false;
                    gridFinalmask.IsVisible = false;
                    sepaTls.IsVisible = false;
                    gridTls.IsVisible = false;
                    gridTlsMore.IsVisible = false;
                    gridRealityMore.IsVisible = false;
                    sepaFeatures.IsVisible = false;
                    grpPingNFeatures.IsVisible = false;
                }
                else
                {
                    gridSocks.IsVisible = true;
                }
                break;

            case EConfigType.HTTP:
                gridSocks.IsVisible = true;
                tbHttpHeaders.IsVisible = true;
                txtHttpHeadersJson.IsVisible = true;
                break;

            case EConfigType.VLESS:
                gridVLESS.IsVisible = true;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow5.ItemsSource = Global.Flows;
                break;

            case EConfigType.Trojan:
                gridTrojan.IsVisible = true;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow6.ItemsSource = Global.Flows;
                break;

            case EConfigType.Hysteria2:
                gridHysteria2.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                break;

            case EConfigType.TUIC:
                gridTuic.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                cmbCoreType.IsEnabled = false;
                cmbFingerprint.IsEnabled = false;
                gridFinalmask.IsVisible = false;

                cmbCongestionControl8.ItemsSource = Global.TuicCongestionControls;
                break;

            case EConfigType.WireGuard:
                gridWireguard.IsVisible = true;

                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                gridTls.IsVisible = false;

                break;

            case EConfigType.Anytls:
                gridAnytls.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbCoreType.IsEnabled = false;
                gridFinalmask.IsVisible = false;
                break;

            case EConfigType.Naive:
                gridNaive.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                cmbCoreType.IsEnabled = false;
                gridFinalmask.IsVisible = false;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                togAllowInsecure.IsEnabled = false;

                cmbCongestionControl12.ItemsSource = Global.NaiveCongestionControls;
                break;

            case EConfigType.MASQUE:
                gridMasque.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                break;
        }
        cmbStreamSecurity.ItemsSource = lstStreamSecurity;
        cmbStreamSecurity.SelectedItem = profileItem.StreamSecurity;
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void CmbNetwork_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetTransportGridVisibility();
    }

    private void CmbHeaderTypeRaw_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetRawHttpFieldsVisibility();
    }

    private void CmbStreamSecurity_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var security = cmbStreamSecurity.SelectedItem.ToString();
        if (security == Global.StreamSecurityReality)
        {
            gridRealityMore.IsVisible = true;
            gridTlsMore.IsVisible = false;
        }
        else if (security == Global.StreamSecurity)
        {
            gridRealityMore.IsVisible = false;
            gridTlsMore.IsVisible = true;
        }
        else
        {
            gridRealityMore.IsVisible = false;
            gridTlsMore.IsVisible = false;
        }
    }

    private void btnGUID_Click(object? sender, RoutedEventArgs e)
    {
        txtId.Text =
        txtId5.Text = Utils.GetGuid();
    }

    private void SetTransportGridVisibility()
    {
        var network = cmbNetwork.SelectedItem?.ToString();
        if (network.IsNullOrEmpty())
        {
            network = Global.DefaultNetwork;
        }

        gridTransportRaw.IsVisible = false;
        gridTransportKcp.IsVisible = false;
        gridTransportWs.IsVisible = false;
        gridTransportHttpupgrade.IsVisible = false;
        gridTransportXhttp.IsVisible = false;
        gridTransportGrpc.IsVisible = false;

        switch (network)
        {
            case nameof(ETransport.raw):
                gridTransportRaw.IsVisible = true;
                break;

            case nameof(ETransport.kcp):
                gridTransportKcp.IsVisible = true;
                break;

            case nameof(ETransport.ws):
                gridTransportWs.IsVisible = true;
                break;

            case nameof(ETransport.httpupgrade):
                gridTransportHttpupgrade.IsVisible = true;
                break;

            case nameof(ETransport.xhttp):
                gridTransportXhttp.IsVisible = true;
                break;

            case nameof(ETransport.grpc):
                gridTransportGrpc.IsVisible = true;
                break;

            default:
                gridTransportRaw.IsVisible = true;
                break;
        }

        SetRawHttpFieldsVisibility();
    }

    private void SetRawHttpFieldsVisibility()
    {
        var network = cmbNetwork.SelectedItem?.ToString();
        if (network.IsNullOrEmpty())
        {
            network = Global.DefaultNetwork;
        }

        var rawHeaderType = cmbHeaderTypeRaw.SelectedItem?.ToString();
        var showRawHttpFields = network == nameof(ETransport.raw)
                                && rawHeaderType == Global.RawHeaderHttp;
        gridTransportRawHttp.IsVisible = showRawHttpFields;
    }
}
