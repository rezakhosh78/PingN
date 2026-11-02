using System.Windows.Controls;

namespace PingN.Views;

public partial class AddServerWindow
{
    public AddServerWindow()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        Closed += (_, _) => ViewModel?.CancelWarpKeyGeneration();
        cmbNetwork.SelectionChanged += CmbNetwork_SelectionChanged;
        cmbHeaderTypeRaw.SelectionChanged += CmbHeaderTypeRaw_SelectionChanged;
        cmbStreamSecurity.SelectionChanged += CmbStreamSecurity_SelectionChanged;
        btnGUID.Click += btnGUID_Click;
        btnGUID5.Click += btnGUID_Click;

        cmbCoreType.ItemsSource = Global.CoreTypes.AppendEmpty();
        cmbNetwork.ItemsSource = Global.Networks;
        cmbWarpAmneziaScanFamily.ItemsSource = new[] { "IPv4", "IPv6" };
        cmbWarpAmneziaScanMode.ItemsSource = new[] { "Fast", "All" };

        cmbHeaderTypeRaw.ItemsSource = new List<string> { Global.None, Global.RawHeaderHttp };

        var kcpHeaderTypes = new List<string> { Global.None };
        kcpHeaderTypes.AddRange(Global.KcpHeaderTypes);
        cmbHeaderTypeKcp.ItemsSource = kcpHeaderTypes;

        cmbHeaderTypeXhttp.ItemsSource = Global.XhttpMode;
        cmbHeaderTypeGrpc.ItemsSource = new List<string> { Global.GrpcGunMode, Global.GrpcMultiMode };

        cmbFingerprint.ItemsSource = Global.Fingerprints;
        cmbFingerprint2.ItemsSource = Global.Fingerprints;
        cmbAlpn.ItemsSource = Global.Alpns;

        gridTlsMore.Visibility = Visibility.Collapsed;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.PsiphonOver, v => v.togPsiphonOver.IsChecked).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.PsiphonRegions, v => v.cmbPsiphonRegion.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedPsiphonRegion, v => v.cmbPsiphonRegion.SelectedItem).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.SelectedSource)
                .KeepNotNull()
                .Subscribe(InitializeData)
                .DisposeWith(disposables);

            var configTypeBindings = new SingleReplaceableDisposable();
            configTypeBindings.DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.WarpAmneziaScanStatus, v => v.tbWarpAmneziaScanStatus.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAmneziaScanFamily, v => v.cmbWarpAmneziaScanFamily.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAmneziaScanMode, v => v.cmbWarpAmneziaScanMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgPrivateKey, v => v.txtWarpAwgPrivateKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgPublicKey, v => v.txtWarpAwgPublicKey.Text).DisposeWith(disposables);
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
            this.Bind(ViewModel, vm => vm.WarpAwgPresharedKey, v => v.txtWarpAwgPresharedKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgAllowedIPs, v => v.txtWarpAwgAllowedIPs.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgEndpoint, v => v.txtWarpAwgEndpoint.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WarpAwgPersistentKeepalive, v => v.txtWarpAwgPersistentKeepalive.Text).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ScanWarpAmneziaEndpointCmd, v => v.btnScanWarpAmneziaEndpoint).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ApplyWarpAmneziaEndpointCmd, v => v.btnApplyWarpAmneziaEndpoint).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.HasScannedWarpAmneziaEndpoint, v => v.btnApplyWarpAmneziaEndpoint.IsEnabled).DisposeWith(disposables);

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
            this.OneWayBind(ViewModel, vm => vm.HasScannedWarpMasqueEndpoint, v => v.btnApplyWarpMasqueEndpoint.IsEnabled).DisposeWith(disposables);

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
                            this.Bind(ViewModel, vm => vm.VmessSecurity, v => v.cmbSecurity.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MuxEnabled, v => v.togmuxEnabled.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.Shadowsocks:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId3.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.SsMethod, v => v.cmbSecurity3.Text).DisposeWith(currentTypeDisposables);
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
                            this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow5.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.VlessEncryption, v => v.txtSecurity5.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MuxEnabled, v => v.togmuxEnabled5.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.Trojan:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId6.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow6.Text).DisposeWith(currentTypeDisposables);
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
                            this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl8.Text).DisposeWith(currentTypeDisposables);
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
                            this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl12.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.InsecureConcurrency, v => v.txtInsecureConcurrency12.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.Uot, v => v.togUotEnabled12.IsChecked).DisposeWith(currentTypeDisposables);
                            break;

                        case EConfigType.MASQUE:
                            this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtId14.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtSecurity14.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MasquePath, v => v.txtPath14.Text).DisposeWith(currentTypeDisposables);
                            this.Bind(ViewModel, vm => vm.MasqueHeaders, v => v.txtHeaders14.Text).DisposeWith(currentTypeDisposables);
                            break;
                    }
                })
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RawHeaderType, v => v.cmbHeaderTypeRaw.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostRaw.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathRaw.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.KcpHeaderType, v => v.cmbHeaderTypeKcp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpSeed, v => v.txtKcpSeed.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpMtu, v => v.txtKcpMtu.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostWs.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathWs.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostHttpupgrade.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathHttpupgrade.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.XhttpMode, v => v.cmbHeaderTypeXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.XhttpExtra, v => v.txtExtraXhttp.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.GrpcMode, v => v.cmbHeaderTypeGrpc.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcAuthority, v => v.txtRequestHostGrpc.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcServiceName, v => v.txtPathGrpc.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.StreamSecurity, v => v.cmbStreamSecurity.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AllowInsecure, v => v.togAllowInsecure.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Alpn, v => v.cmbAlpn.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertSha, v => v.txtCertSha256Pinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertTip, v => v.labCertPinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.EchConfigList, v => v.txtEchConfigList.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.VerifyPeerCertByName, v => v.txtVerifyPeerCertByName.Text).DisposeWith(disposables);

            //reality
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PublicKey, v => v.txtPublicKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.ShortId, v => v.txtShortId.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.SpiderX, v => v.txtSpiderX.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Mldsa65Verify, v => v.txtMldsa65Verify.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Finalmask, v => v.txtFinalmask.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.FetchCertCmd, v => v.btnFetchCert).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FetchCertChainCmd, v => v.btnFetchCertChain).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });

        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

    private void InitializeData(ProfileItem profileItem)
    {
        var warpProfileType = profileItem.GetProtocolExtra().WarpProfileType;
        var isWarpAmnezia = string.Equals(warpProfileType, OberonRuntimeManager.WarpProfileType, StringComparison.OrdinalIgnoreCase);
        var isWarpMasque = string.Equals(warpProfileType, WarpMasqueRuntimeManager.WarpProfileType, StringComparison.OrdinalIgnoreCase);
        var dnsTunnelTitle = profileItem.GetProtocolExtra().DnsTunnel?.Kind switch
        {
            "masterdns" => "MasterDNS",
            "dnstt-ssh" => "DNSTT",
            _ => null,
        };
        Title = isWarpAmnezia ? "WARP AmneziaWG"
            : isWarpMasque ? WarpMasqueRuntimeManager.Description
            : dnsTunnelTitle ?? $"{profileItem.ConfigType}";
        var lstStreamSecurity = new List<string> { string.Empty, Global.StreamSecurity };
        switch (profileItem.ConfigType)
        {
            case EConfigType.VMess:
                gridVMess.Visibility = Visibility.Visible;
                cmbSecurity.ItemsSource = Global.VmessSecurities;
                break;

            case EConfigType.Shadowsocks:
                gridSs.Visibility = Visibility.Visible;
                cmbSecurity3.ItemsSource = AppManager.Instance.GetShadowsocksSecurities(profileItem);
                break;

            case EConfigType.SOCKS:
                if (isWarpAmnezia || isWarpMasque)
                {
                    gridWarpAmnezia.Visibility = isWarpAmnezia ? Visibility.Visible : Visibility.Collapsed;
                    gridWarpMasque.Visibility = isWarpMasque ? Visibility.Visible : Visibility.Collapsed;
                    tbAddress.Visibility = txtAddress.Visibility = Visibility.Collapsed;
                    tbPort.Visibility = txtPort.Visibility = Visibility.Collapsed;
                    sepaCommon.Visibility = Visibility.Collapsed;
                    gridSocks.Visibility = Visibility.Collapsed;
                    sepa2.Visibility = Visibility.Collapsed;
                    gridTransport.Visibility = Visibility.Collapsed;
                    gridFinalmask.Visibility = Visibility.Collapsed;
                    sepaTls.Visibility = Visibility.Collapsed;
                    gridTls.Visibility = Visibility.Collapsed;
                    gridTlsMore.Visibility = Visibility.Collapsed;
                    gridRealityMore.Visibility = Visibility.Collapsed;
                    sepaBottom.Visibility = Visibility.Collapsed;
                }
                else
                {
                    gridSocks.Visibility = Visibility.Visible;
                }
                break;

            case EConfigType.HTTP:
                gridSocks.Visibility = Visibility.Visible;
                tbHttpHeaders.Visibility = Visibility.Visible;
                txtHttpHeadersJson.Visibility = Visibility.Visible;
                break;

            case EConfigType.VLESS:
                gridVLESS.Visibility = Visibility.Visible;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow5.ItemsSource = Global.Flows;
                break;

            case EConfigType.Trojan:
                gridTrojan.Visibility = Visibility.Visible;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow6.ItemsSource = Global.Flows;
                break;

            case EConfigType.Hysteria2:
                gridHysteria2.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                break;

            case EConfigType.TUIC:
                gridTuic.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbCoreType.IsEnabled = false;
                cmbFingerprint.IsEnabled = false;
                gridFinalmask.Visibility = Visibility.Collapsed;

                cmbCongestionControl8.ItemsSource = Global.TuicCongestionControls;
                break;

            case EConfigType.WireGuard:
                gridWireguard.Visibility = Visibility.Visible;

                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                gridTls.Visibility = Visibility.Collapsed;

                break;

            case EConfigType.Anytls:
                gridAnytls.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbCoreType.IsEnabled = false;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                gridFinalmask.Visibility = Visibility.Collapsed;
                break;

            case EConfigType.Naive:
                gridNaive.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbCoreType.IsEnabled = false;
                gridFinalmask.Visibility = Visibility.Collapsed;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                togAllowInsecure.IsEnabled = false;

                cmbCongestionControl12.ItemsSource = Global.NaiveCongestionControls;
                break;

            case EConfigType.MASQUE:
                gridMasque.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                break;
        }
        cmbStreamSecurity.ItemsSource = lstStreamSecurity;
        cmbStreamSecurity.SelectedItem = profileItem.StreamSecurity;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.DnsTunnel is not null)
        {
            serverEditorScroll.Content = BuildDnsTunnelEditor();
            return;
        }
        if (ViewModel?.IsWarpAmneziaProfile == true || ViewModel?.IsWarpMasqueProfile == true) AlignWarpLikeVless();
        txtRemarks.Focus();
    }

    private void CmbNetwork_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SetTransportGridVisibility();
    }

    private void CmbHeaderTypeRaw_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SetRawHttpFieldsVisibility();
    }

    private void CmbStreamSecurity_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var security = cmbStreamSecurity.SelectedItem.ToString();
        if (security == Global.StreamSecurityReality)
        {
            gridRealityMore.Visibility = Visibility.Visible;
            gridTlsMore.Visibility = Visibility.Collapsed;
        }
        else if (security == Global.StreamSecurity)
        {
            gridRealityMore.Visibility = Visibility.Collapsed;
            gridTlsMore.Visibility = Visibility.Visible;
        }
        else
        {
            gridRealityMore.Visibility = Visibility.Collapsed;
            gridTlsMore.Visibility = Visibility.Collapsed;
        }
    }

    private void btnGUID_Click(object sender, RoutedEventArgs e)
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

        gridTransportRaw.Visibility = Visibility.Collapsed;
        gridTransportKcp.Visibility = Visibility.Collapsed;
        gridTransportWs.Visibility = Visibility.Collapsed;
        gridTransportHttpupgrade.Visibility = Visibility.Collapsed;
        gridTransportXhttp.Visibility = Visibility.Collapsed;
        gridTransportGrpc.Visibility = Visibility.Collapsed;

        switch (network)
        {
            case nameof(ETransport.raw):
                gridTransportRaw.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.kcp):
                gridTransportKcp.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.ws):
                gridTransportWs.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.httpupgrade):
                gridTransportHttpupgrade.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.xhttp):
                gridTransportXhttp.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.grpc):
                gridTransportGrpc.Visibility = Visibility.Visible;
                break;

            default:
                gridTransportRaw.Visibility = Visibility.Visible;
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
        gridTransportRawHttp.Visibility = showRawHttpFields
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private StackPanel BuildDnsTunnelEditor()
    {
        var settings = ViewModel!.DnsTunnel!;
        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = settings.Kind == "masterdns" ? "MasterDNS" : "DNSTT SSH", FontSize = 22, Margin = new Thickness(4) });
        void Field(string name, string value, Action<string> changed, bool multiline = false, bool readOnly = false)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) };
            var box = new TextBox { Text = value, Margin = new Thickness(4), MinWidth = 0,
                AcceptsReturn = multiline, IsReadOnly = readOnly, HorizontalAlignment = HorizontalAlignment.Stretch };
            if (multiline) box.MinHeight = 90;
            if (name == "SSH password" || name.StartsWith("Private key passphrase", StringComparison.Ordinal))
            {
                var password = new PasswordBox { Password = value, Margin = new Thickness(4), MinWidth = 0 };
                password.PasswordChanged += (_, _) => changed(password.Password);
                ArrangeEditorRow(row, label, password);
                row.Children.Add(label); row.Children.Add(password); panel.Children.Add(row);
                return;
            }
            box.TextChanged += (_, _) => changed(box.Text ?? string.Empty);
            ArrangeEditorRow(row, label, box);
            row.Children.Add(label); row.Children.Add(box); panel.Children.Add(row);
        }
        void Choice(string name, string value, IReadOnlyList<DnsFieldChoice> choices, Action<string> changed)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            // Preserve newer/unknown upstream values rather than silently changing a saved profile.
            var options = choices.ToList();
            if (!options.Any(x => x.Value == value)) options.Add(new DnsFieldChoice(value, $"{value} (custom / newer core)"));
            var combo = new ComboBox { ItemsSource = options, SelectedItem = options.First(x => x.Value == value),
                MinWidth = 0, Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is DnsFieldChoice selected) changed(selected.Value);
            };
            var label = new TextBlock { Text = name, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center };
            ArrangeEditorRow(row, label, combo);
            row.Children.Add(label); row.Children.Add(combo); panel.Children.Add(row);
        }
        Field("Remarks", ViewModel.SelectedSource.Remarks, x => ViewModel.SelectedSource.Remarks = x);
        Field("Local SOCKS port", ViewModel.SelectedSource.Port.ToString(), x => ViewModel.SelectedSource.Port = int.TryParse(x, out var port) ? port : 0);
        if (settings.Kind == "masterdns")
            foreach (var (key, value) in DnsTunnelCoreInstaller.InstalledMasterDefaults())
                settings.MasterOptions.TryAdd(key, value);
        if (settings.Kind == "masterdns")
        {
            Field("DNS resolvers (one per line)", settings.Resolvers, x => settings.Resolvers = x, true);
            // Every setting shipped by the installed core becomes an editable row.
            foreach (var (key, value) in settings.MasterOptions.ToList())
            {
                var managed = key is "PROTOCOL_TYPE" or "LISTEN_IP" or "LISTEN_PORT" or "SOCKS5_AUTH";
                var displayed = key switch { "PROTOCOL_TYPE" => "\"SOCKS5\"", "LISTEN_IP" => "\"127.0.0.1\"",
                    "LISTEN_PORT" => ViewModel.SelectedSource.Port.ToString(), "SOCKS5_AUTH" => "false", _ => value };
                var choices = MasterDnsEditorSchema.Choices(key, displayed);
                var label = MasterDnsEditorSchema.Label(key);
                if (!managed && choices is not null)
                    Choice(label, displayed, choices, x => settings.MasterOptions[key] = x);
                else if (!managed && displayed.StartsWith("\""))
                {
                    // Plain strings are shown without TOML quotation marks and escaped on save.
                    try
                    {
                        var plain = System.Text.Json.JsonSerializer.Deserialize<string>(displayed) ?? string.Empty;
                        Field(label, plain, x => settings.MasterOptions[key] = System.Text.Json.JsonSerializer.Serialize(x));
                    }
                    catch (System.Text.Json.JsonException) { Field(label, displayed, x => settings.MasterOptions[key] = x); }
                }
                else Field(label, displayed, x => settings.MasterOptions[key] = x, false, managed);
            }
        }
        else
        {
            Field("Tunnel domain", settings.Domain, x => settings.Domain = x.Trim());
            Field("Server public key (hex)", settings.PublicKey, x => settings.PublicKey = x.Trim());
            Choice("DNS transport", settings.Transport, new[] { new DnsFieldChoice("udp", "UDP DNS"),
                new DnsFieldChoice("doh", "DNS over HTTPS (DoH)"), new DnsFieldChoice("dot", "DNS over TLS (DoT)") }, x => settings.Transport = x);
            Field("Resolver: IP:port / HTTPS URL", settings.Resolver, x => settings.Resolver = x.Trim());
            Field("Tunnel local TCP port", settings.TunnelPort, x => settings.TunnelPort = x.Trim());
            Field("TLS fingerprint (uTLS)", settings.Utls, x => settings.Utls = x.Trim());
            Field("SSH username", settings.SshUser, x => settings.SshUser = x);
            Field("SSH password", settings.SshPassword, x => settings.SshPassword = x);
            Field("SSH private key path (optional)", settings.PrivateKeyPath, x => settings.PrivateKeyPath = x);
            Field("Private key passphrase (optional)", settings.PrivateKeyPassphrase, x => settings.PrivateKeyPassphrase = x);
            Field("SSH host key (optional; auto-saved)", settings.HostKeySha256, x => settings.HostKeySha256 = x.Trim());
            Field("SSH timeout (seconds)", settings.ConnectTimeoutSeconds, x => settings.ConnectTimeoutSeconds = x.Trim());
            Field("SSH keepalive (seconds)", settings.KeepAliveSeconds, x => settings.KeepAliveSeconds = x.Trim());
        }
        return panel;
    }

    private double VlessLabelWidth()
    {
        var available = serverEditorScroll.ActualWidth;
        if (available <= 0) available = Width - 48;
        var reference = gridVLESS.ColumnDefinitions[0].Width.Value;
        return Math.Min(reference > 0 ? reference : 300, Math.Max(120, available * 0.4));
    }

    private void ArrangeEditorRow(Grid row, TextBlock label, FrameworkElement input)
    {
        row.ColumnDefinitions.Clear(); row.RowDefinitions.Clear();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(VlessLabelWidth()) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.TextAlignment = TextAlignment.Left;
        label.TextWrapping = TextWrapping.Wrap;
        label.VerticalAlignment = VerticalAlignment.Center;
        input.HorizontalAlignment = HorizontalAlignment.Left;
        input.MinWidth = 0;
        if (input is TextBox text) text.Style = txtId5.Style;
        if (input is ComboBox combo) combo.Style = cmbFlow5.Style;
        label.Style = TryFindResource("ToolbarTextBlock") as Style;
        Grid.SetRow(label, 0); Grid.SetColumn(label, 0);
        Grid.SetRow(input, 0); Grid.SetColumn(input, 1);
        void Resize()
        {
            var available = serverEditorScroll.ActualWidth;
            if (available <= 0) available = Width - 48;
            var labelWidth = VlessLabelWidth();
            row.ColumnDefinitions[0].Width = new GridLength(labelWidth);
            var preferred = input is ComboBox ? cmbFlow5.Width : txtId5.Width;
            if (double.IsNaN(preferred) || preferred <= 0) preferred = input is ComboBox ? 200 : 400;
            input.Width = Math.Max(40, Math.Min(preferred, available - labelWidth - 48));
        }
        Resize();
        serverEditorScroll.SizeChanged += (_, _) => Resize();
    }

    private bool _warpLayoutConfigured;
    private void AlignWarpLikeVless()
    {
        if (_warpLayoutConfigured) return;
        cmbWarpAmneziaRegistrationProxy.ItemsSource = ViewModel!.WarpRegistrationProxies;
        this.Bind(ViewModel, vm => vm.SelectedWarpRegistrationProxy, v => v.cmbWarpAmneziaRegistrationProxy.SelectedItem);
        btnWarpAmneziaGenerateKey.Command = ViewModel.GenerateWarpKeyCmd;
        this.OneWayBind(ViewModel, vm => vm.WarpKeyStatus, v => v.txtWarpAmneziaKeyStatus.Text);

        cmbWarpMasqueRegistrationProxy.ItemsSource = ViewModel!.WarpRegistrationProxies;
        this.Bind(ViewModel, vm => vm.SelectedWarpRegistrationProxy, v => v.cmbWarpMasqueRegistrationProxy.SelectedItem);
        btnWarpMasqueGenerateKey.Command = ViewModel.GenerateWarpKeyCmd;
        this.OneWayBind(ViewModel, vm => vm.WarpKeyStatus, v => v.txtWarpMasqueKeyStatus.Text);

        _warpLayoutConfigured = true;
        foreach (var grid in new[] { gridWarpAmnezia, gridWarpMasque })
        {
            if (grid.Children.OfType<StackPanel>().FirstOrDefault() is not { } fields) continue;
            foreach (var row in fields.Children.OfType<Grid>())
            {
                var label = row.Children.OfType<TextBlock>().FirstOrDefault();
                var input = row.Children.OfType<FrameworkElement>().FirstOrDefault(x => x is TextBox or ComboBox);
                if (label is not null && input is not null) ArrangeEditorRow(row, label, input);
            }
        }
        void AlignCommon()
        {
            var labelWidth = VlessLabelWidth();
            gridBasic.ColumnDefinitions[0].Width = new GridLength(labelWidth);
            var available = serverEditorScroll.ActualWidth;
            if (available <= 0) available = Width - 48;
            foreach (var input in new FrameworkElement[] { txtRemarks, txtAddress, txtPort, cmbCoreType })
            {
                input.HorizontalAlignment = HorizontalAlignment.Left;
                input.Width = Math.Max(40, Math.Min(input is ComboBox ? 200 : 400, available - labelWidth - 24));
            }
            foreach (var label in gridBasic.Children.OfType<TextBlock>()) label.HorizontalAlignment = HorizontalAlignment.Left;
        }
        AlignCommon();
        serverEditorScroll.SizeChanged += (_, _) => AlignCommon();
    }
}
