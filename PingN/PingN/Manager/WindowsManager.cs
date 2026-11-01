using System.Drawing;

namespace PingN.Manager;

public sealed class WindowsManager
{
    private static readonly Lazy<WindowsManager> instance = new(() => new());
    public static WindowsManager Instance => instance.Value;

    private static Uri GetIconUri(Config config) => new(
        $"pack://application:,,,/Resources/{(config.SystemProxyItem.SysProxyType == ESysProxyType.ForcedChange ? "PingNProxy.ico" : "PingN.ico")}",
        UriKind.Absolute);

    public Task<Icon> GetNotifyIcon(Config config)
    {
        using var stream = System.Windows.Application.GetResourceStream(GetIconUri(config))!.Stream;
        using var icon = new Icon(stream);
        return Task.FromResult((Icon)icon.Clone());
    }

    public System.Windows.Media.ImageSource GetAppIcon(Config config) =>
        System.Windows.Media.Imaging.BitmapFrame.Create(GetIconUri(config));

    public void RegisterGlobalHotkey(Config config, Action<EGlobalHotkey> handler, Action<bool, string>? update)
    {
        HotkeyManager.Instance.UpdateViewEvent += update;
        HotkeyManager.Instance.HotkeyTriggerEvent += handler;
        HotkeyManager.Instance.Load();
    }
}
