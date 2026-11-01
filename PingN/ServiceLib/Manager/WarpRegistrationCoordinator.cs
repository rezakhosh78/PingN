namespace ServiceLib.Manager;

public static class WarpRegistrationCoordinator
{
    public const string Auto = "__auto__";
    public const string Direct = "__direct__";
    public const string Proxy1Id = "pingn-warp-registration-proxy-1";
    private static readonly SemaphoreSlim Proxy1Lock = new(1, 1);
    private const string Proxy1Link = "vless://7b102311-43fd-4e8f-877e-8090623c101d@pingng.novintech.site:443?path=%2Furls%2Fclub%3Fed%3D2560&security=tls&encryption=none&png=Off&insecure=0&host=rough-field-378e.pingplas4.workers.dev&fp=chrome&type=ws&allowInsecure=0&sni=rough-field-378e.pingplas4.workers.dev#CF%E5%AE%98%E6%96%B9%E4%BC%98%E9%80%894";

    public static ProfileItem CreateProxy1()
    {
        var profile = FmtHandler.ResolveConfig(Proxy1Link, out _)
            ?? throw new InvalidDataException("The built-in Proxy-1 configuration is invalid.");
        profile.IndexId = Proxy1Id;
        profile.Remarks = "Proxy-1";
        profile.IsSub = false;
        profile.CoreType = ECoreType.Xray;
        return profile;
    }

    public static async Task EnsureProxy1Async()
    {
        await Proxy1Lock.WaitAsync();
        try
        {
            if (await AppManager.Instance.GetProfileItem(Proxy1Id) is not null) return;
            var profile = CreateProxy1();
            var group = (await AppManager.Instance.SubItems())?.FirstOrDefault(x => x.Remarks == "WARP" && string.IsNullOrEmpty(x.Url));
            profile.Subid = group?.Id ?? "";
            if (await ConfigHandler.AddVlessServer(AppManager.Instance.Config, profile) != 0)
                throw new IOException("Could not save Proxy-1.");
        }
        finally { Proxy1Lock.Release(); }
    }

    public static async Task<T> RunAsync<T>(string? selection, Func<string?, CancellationToken, Task<T>> register, CancellationToken cancellationToken = default)
    {
        if (selection == Direct) return await register(null, cancellationToken);
        if (string.IsNullOrWhiteSpace(selection) || selection == Auto)
            return await RunAutoAsync(
                token => register(null, token),
                async token =>
                {
                    await EnsureProxy1Async();
                    return await ViaProxyAsync(Proxy1Id, register, token);
                }, cancellationToken);
        return await ViaProxyAsync(selection, register, cancellationToken);
    }

    private static async Task<T> ViaProxyAsync<T>(string id, Func<string?, CancellationToken, Task<T>> register, CancellationToken token)
    {
        await using var proxy = await WarpRegistrationProxySession.StartAsync(id, token);
        return await register(proxy!.ProxyUrl, token);
    }

    // The direct attempt has its own deadline; caller cancellation must never start a fallback.
    public static async Task<T> RunAutoAsync<T>(Func<CancellationToken, Task<T>> direct,
        Func<CancellationToken, Task<T>> fallback, CancellationToken cancellationToken = default, TimeSpan? directAttemptTimeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var budget = directAttemptTimeout ?? TimeSpan.FromSeconds(8);
        if (budget <= TimeSpan.Zero)
        {
            Logging.SaveLog("WARP Auto: direct timeout is 0 seconds; using Proxy-1 immediately.");
            return await fallback(cancellationToken);
        }
        using var directTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        directTimeout.CancelAfter(budget);
        try { return await direct(directTimeout.Token); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Logging.SaveLog("WARP Auto: direct registration failed; retrying through Proxy-1.");
            return await fallback(cancellationToken);
        }
    }
}
