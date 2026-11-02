using ServiceLib.Base;

namespace PingN.WebAPI.Adapters;

public sealed class HeadlessWindowDialog : IWindowDialog
{
    public Task<bool> ShowDialogAsync<TViewModel>(TViewModel vm)
        where TViewModel : class => Task.FromResult(false);
}
