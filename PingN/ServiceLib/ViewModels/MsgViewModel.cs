namespace ServiceLib.ViewModels;

public partial class MsgViewModel : MyReactiveObject
{
    public Interaction<(string Category, string Text), RxVoid> ShowMsgInteraction { get; } = new();

    private readonly ConcurrentQueue<(string Category, string Text)> _queueMsg = new();
    private volatile bool _lastMsgFilterNotAvailable;
    public int NumMaxMsg => 500;

    [Reactive]
    public partial string MsgFilter { get; set; }

    [Reactive]
    public partial bool AutoRefresh { get; set; }

    public MsgViewModel()
    {
        _config = AppManager.Instance.Config;
        MsgFilter = _config.MsgUIItem.MainMsgFilter ?? string.Empty;
        AutoRefresh = _config.MsgUIItem.AutoRefresh ?? true;

        this.WhenAnyValue(
                x => x.MsgFilter)
            .Subscribe(c => DoMsgFilter());

        this.WhenAnyValue(x => x.AutoRefresh)
            .Subscribe(_ => _config.MsgUIItem.AutoRefresh = AutoRefresh);

        AppEvents.SendMsgViewRequested
            .AsObservable()
            .Subscribe(EnqueueQueueMsg);

        this.WhenActivated(disposables =>
        {
            Signal.Every(TimeSpan.FromSeconds(1))
                .Where(_ => AutoRefresh && AppManager.Instance.ShowInTaskbar)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => FlushQueueToView())
                .DisposeWith(disposables);
        });
    }

    private void FlushQueueToView()
    {
        if (!AutoRefresh || _queueMsg.IsEmpty)
        {
            return;
        }

        if (!AppManager.Instance.ShowInTaskbar)
        {
            return;
        }

        var sb = new StringBuilder();
        string? category = null;
        while (_queueMsg.TryDequeue(out var item))
        {
            if (category is not null && category != item.Category)
            {
                ShowMsgInteraction.HandleSafe((category, sb.ToString())).Subscribe();
                sb.Clear();
            }
            category = item.Category;
            sb.Append(item.Text);
        }

        if (sb.Length > 0 && category is not null)
        {
            ShowMsgInteraction.HandleSafe((category, sb.ToString())).Subscribe();
        }
    }

    private void EnqueueQueueMsg(string msg)
    {
        if (string.IsNullOrEmpty(msg))
        {
            return;
        }

        //filter msg
        if (MsgFilter.IsNotEmpty() && !_lastMsgFilterNotAvailable)
        {
            try
            {
                if (!Utils.IsRegexMatch(msg, MsgFilter))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                EnqueueWithLimit(("Xray", ex.Message));
                _lastMsgFilterNotAvailable = true;
            }
        }

        var formattedMsg = msg.EndsWith(Environment.NewLine)
            ? msg
            : msg + Environment.NewLine;

        var category = msg.StartsWith("Desync", StringComparison.OrdinalIgnoreCase) ? "Desync"
            : msg.StartsWith("SNI Spoofing Plus", StringComparison.OrdinalIgnoreCase) ? "SNI Spoofing Plus"
            : "Xray";
        EnqueueWithLimit((category, formattedMsg));
    }

    private void EnqueueWithLimit((string Category, string Text) item)
    {
        _queueMsg.Enqueue(item);

        while (_queueMsg.Count > NumMaxMsg)
        {
            _queueMsg.TryDequeue(out _);
        }
    }

    //public void ClearMsg()
    //{
    //    _queueMsg.Clear();
    //}

    private void DoMsgFilter()
    {
        _config.MsgUIItem.MainMsgFilter = MsgFilter;
        _lastMsgFilterNotAvailable = false;
    }
}
