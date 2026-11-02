namespace PingN.Views;

public partial class MsgView
{
    private readonly Dictionary<string, StringBuilder> _categoryLogs = RuntimeLogCategories.Names.ToDictionary(x => x, _ => new StringBuilder());
    private string _selectedCategory = "All";
    public MsgView()
    {
        InitializeComponent();
        cmbLogCategory.ItemsSource = RuntimeLogCategories.Names;
        cmbLogCategory.SelectedIndex = 0;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.MsgFilter, v => v.cmbMsgFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);

            ViewModel.ShowMsgInteraction.RegisterHandler(interaction =>
            {
                var msg = interaction.Input;
                ShowMsg(msg.Category, msg.Text);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);
        });

        btnCopy.Click += menuMsgViewCopyAll_Click;
        btnClear.Click += menuMsgViewClear_Click;
        menuMsgViewSelectAll.Click += menuMsgViewSelectAll_Click;
        menuMsgViewCopy.Click += menuMsgViewCopy_Click;
        menuMsgViewCopyAll.Click += menuMsgViewCopyAll_Click;
        menuMsgViewClear.Click += menuMsgViewClear_Click;

        cmbMsgFilter.ItemsSource = Global.PresetMsgFilters;
    }

    private void ShowMsg(string category, string msg)
    {
        AppendCategory("All", msg);
        AppendCategory(category, msg);
        if (_selectedCategory != "All" && _selectedCategory != category) return;
        txtMsg.Text = _categoryLogs[_selectedCategory].ToString();
        if (togScrollToEnd.IsChecked ?? true) txtMsg.ScrollToEnd();
    }

    private void AppendCategory(string category, string text)
    {
        var buffer = _categoryLogs[category];
        buffer.Append(text);
        if (buffer.Length > 200_000) buffer.Remove(0, buffer.Length - 150_000);
    }

    private void LogCategoryChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (cmbLogCategory.SelectedItem is not string category || !_categoryLogs.ContainsKey(category)) return;
        _selectedCategory = category;
        txtMsg.Text = _categoryLogs[category].ToString();
        txtMsg.ScrollToEnd();
    }

    public void ClearMsg()
    {
        if (_selectedCategory == "All") foreach (var buffer in _categoryLogs.Values) buffer.Clear();
        else _categoryLogs[_selectedCategory].Clear();
        txtMsg.Clear();
    }

    private void menuMsgViewSelectAll_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        txtMsg.Focus();
        txtMsg.SelectAll();
    }

    private void menuMsgViewCopy_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var data = txtMsg.SelectedText.TrimEx();
        WindowsUtils.SetClipboardData(data);
    }

    private void menuMsgViewCopyAll_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var data = txtMsg.Text;
        WindowsUtils.SetClipboardData(data);
    }

    private void menuMsgViewClear_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ClearMsg();
    }
}
