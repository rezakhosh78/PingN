using PingN.Desktop.Base;
using PingN.Desktop.Common;

namespace PingN.Desktop.Views;

public partial class AddServer2Window : WindowBase<AddServer2ViewModel>
{
    public AddServer2Window()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => Close();

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.ViewModel.SelectedSource)
                .KeepNotNull()
                .Subscribe(InitializeData)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.DisplayLog, v => v.togDisplayLog.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PreSocksPort, v => v.txtPreSocksPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IsSingboxEndpoint, v => v.togSingBoxEndpoint.IsChecked).DisposeWith(disposables);
            cmbDesyncProfile.ItemsSource = ViewModel.DesyncProfileOptions;
            cmbDesyncSplitMode.ItemsSource = ViewModel.DesyncSplitModeOptions;
            cmbDesyncFooling.ItemsSource = ViewModel.DesyncFoolingOptions;
            cmbSniSpoofingUtls.ItemsSource = ViewModel.SniSpoofingPlusUtlsOptions;
        cmbSniInjector.ItemsSource = new[] { "active", "passive" };
        cmbSniRouteMode.ItemsSource = new[] { "independent", "pair-round-robin", "pair-balanced" };
        cmbSniIpRouting.ItemsSource = new[] { "sticky", "round-robin", "failover" };
        cmbSniSniRouting.ItemsSource = new[] { "sticky", "round-robin" };
            this.Bind(ViewModel, vm => vm.DesyncProfile, v => v.cmbDesyncProfile.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedDesyncScanResult, v => v.cmbDesyncScanResult.SelectedItem).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ScanDesyncCmd, v => v.btnScanDesync).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ApplyDesyncScanCmd, v => v.btnApplyDesyncScan).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncSplitMode, v => v.cmbDesyncSplitMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncSplitPositions, v => v.txtDesyncSplitPositions.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncFooling, v => v.cmbDesyncFooling.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncBadSequence, v => v.txtDesyncBadSequence.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncRepeats, v => v.txtDesyncRepeats.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncAutoTtlDelta, v => v.txtDesyncAutoTtlDelta.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncAutoTtlMin, v => v.txtDesyncAutoTtlMin.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncAutoTtlMax, v => v.txtDesyncAutoTtlMax.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DesyncFakeSni, v => v.txtDesyncFakeSni.Text).DisposeWith(disposables);
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

            this.BindCommand(ViewModel, vm => vm.BrowseServerCmd, v => v.btnBrowse).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.EditServerCmd, v => v.btnEdit).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveServerCmd, v => v.btnSave).DisposeWith(disposables);

            ViewModel.BrowseConfigFileInteraction.RegisterHandler(async interaction =>
            {
                var fileName = await UI.OpenFileDialog(null);
                interaction.SetOutput(fileName);
            }).DisposeWith(disposables);
        });
    }

    private void InitializeData(ProfileItem profileItem)
    {
        if (profileItem.ConfigType is EConfigType.Custom)
        {
            Title = ResUI.menuAddCustomServer;
            cmbCoreType.ItemsSource = Utils.GetEnumNames<ECoreType>().Where(t => t != nameof(ECoreType.PingN)).ToList();
            gridCustomServer.IsVisible = true;
            gridCustomOutbound.IsVisible = false;
        }
        else if (profileItem.ConfigType is EConfigType.Outbound)
        {
            Title = ResUI.menuAddCustomOutboundServer;
            cmbCoreType.ItemsSource = Global.CoreTypes;
            gridCustomServer.IsVisible = false;
            gridCustomOutbound.IsVisible = true;
        }
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }
}
