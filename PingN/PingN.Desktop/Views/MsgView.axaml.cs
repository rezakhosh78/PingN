using PingN.Desktop.Common;

namespace PingN.Desktop.Views;

public partial class MsgView : ReactiveUserControl<MsgViewModel>
{
    private readonly Dictionary<string, StringBuilder> _categoryLogs = RuntimeLogCategories.Names.ToDictionary(x => x, _ => new StringBuilder());
    private string _selectedCategory = "All";
    //private const int KeepLines = 30;

    public MsgView()
    {
        InitializeComponent();
        cmbLogCategory.ItemsSource = RuntimeLogCategories.Names;
        cmbLogCategory.SelectedIndex = 0;
        txtMsg.TextArea.TextView.Options.EnableHyperlinks = false;

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

        TextEditorKeywordHighlighter.Attach(txtMsg, Global.LogLevelColors.ToDictionary(
                kv => kv.Key,
                kv => (IBrush)new SolidColorBrush(Color.Parse(kv.Value))
            ));
    }

    private void ShowMsg(string category, string msg)
    {
        //var lineCount = txtMsg.LineCount;
        //if (lineCount > ViewModel?.NumMaxMsg)
        //{
        //    var cutLine = txtMsg.Document.GetLineByNumber(lineCount - KeepLines);
        //    txtMsg.Document.Remove(0, cutLine.Offset);
        //}
        AppendCategory("All", msg);
        AppendCategory(category, msg);
        if (_selectedCategory != "All" && _selectedCategory != category) return;
        if (txtMsg.LineCount > ViewModel?.NumMaxMsg)
            txtMsg.Text = _categoryLogs[_selectedCategory].ToString();
        else
            txtMsg.AppendText(msg);
        if (togScrollToEnd.IsChecked ?? true)
        {
            txtMsg.ScrollToEnd();
            // Let layout finish without a nested dispatcher loop during a native window drag.
            Dispatcher.UIThread.Post(() =>
            {
                if (togScrollToEnd.IsChecked ?? true)
                {
                    txtMsg.ScrollTo(txtMsg.LineCount, 0, AvaloniaEdit.Rendering.VisualYPosition.TextBottom, txtMsg.Bounds.Height, 0);
                }
            }, DispatcherPriority.Background);
        }
    }

    public void ClearMsg()
    {
        _categoryLogs[_selectedCategory].Clear();
        if (_selectedCategory == "All")
            foreach (var log in _categoryLogs.Values) log.Clear();
        txtMsg.Clear();
        txtMsg.AppendText("----- Message cleared -----\n");
    }

    private void AppendCategory(string category, string text)
    {
        var buffer = _categoryLogs[category];
        buffer.Append(text);
        if (buffer.Length > 200_000)
            buffer.Remove(0, buffer.Length - 150_000);
    }

    private void LogCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (cmbLogCategory.SelectedItem is not string category || !_categoryLogs.ContainsKey(category)) return;
        _selectedCategory = category;
        txtMsg.Text = _categoryLogs[category].ToString();
        txtMsg.ScrollToEnd();
    }

    private void menuMsgViewSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            txtMsg.TextArea.Focus();
            txtMsg.SelectAll();
        }, DispatcherPriority.Render);
    }

    private async void menuMsgViewCopy_Click(object? sender, RoutedEventArgs e)
    {
        var data = txtMsg.SelectedText.TrimEx();
        await AvaUtils.SetClipboardData(this, data);
    }

    private async void menuMsgViewCopyAll_Click(object? sender, RoutedEventArgs e)
    {
        var data = txtMsg.Text.TrimEx();
        await AvaUtils.SetClipboardData(this, data);
    }

    private void menuMsgViewClear_Click(object? sender, RoutedEventArgs e)
    {
        ClearMsg();
    }
}
