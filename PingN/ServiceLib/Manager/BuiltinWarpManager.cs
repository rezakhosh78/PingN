namespace ServiceLib.Manager;

/// <summary>One-time creation of the local WARP group; never subscribes to a remote URL.</summary>
public static class BuiltinWarpManager
{
    private static readonly SemaphoreSlim InitializeLock = new(1, 1);
    public static EventChannel<RxVoid> RegistrationUpdated { get; } = new();

    public static async Task InitializeAsync(Config config)
    {
        await InitializeLock.WaitAsync();
        try
        {
            if (config.BuiltinWarpInitialized)
            {
                await WarpRegistrationCoordinator.EnsureProxy1Async();
                return;
            }
            var groups = await AppManager.Instance.SubItems();
            // Do not appropriate a remote subscription that happens to have the same name.
            var group = groups?.FirstOrDefault(x => x.Remarks == "WARP" && string.IsNullOrWhiteSpace(x.Url));
            if (group is null)
            {
                group = new SubItem { Id = Utils.GetGuid(false), Remarks = "WARP", Url = string.Empty,
                    Enabled = true, AutoUpdateInterval = 0, Memo = "PingN built-in WARP profiles" };
                if (await ConfigHandler.AddSubItem(config, group) != 0)
                    throw new IOException("Could not create the built-in WARP group.");
            }
            var profiles = await AppManager.Instance.ProfileItems(group.Id) ?? [];
            if (!profiles.Any(x => x.GetProtocolExtra().WarpProfileType == WarpMasqueRuntimeManager.WarpProfileType))
            {
                if (await ConfigHandler.AddSocksServer(config, WarpMasqueRuntimeManager.CreateProfile(group.Id)) != 0)
                    throw new IOException("Could not save the built-in WARP MASQUE/H2 profile.");
            }
            if (!profiles.Any(x => x.GetProtocolExtra().WarpProfileType == OberonRuntimeManager.WarpProfileType))
            {
                if (await ConfigHandler.AddSocksServer(config, OberonRuntimeManager.CreatePendingProfile(group.Id)) != 0)
                    throw new IOException("Could not save the built-in WARP AmneziaWG profile.");
            }
            await WarpRegistrationCoordinator.EnsureProxy1Async();
            if (string.IsNullOrEmpty(config.SubIndexId)) config.SubIndexId = group.Id;
            // Saved only after both inserts succeed. Partial attempts can be retried without duplicates.
            config.BuiltinWarpInitialized = true;
            if (await ConfigHandler.SaveConfig(config) != 0)
            {
                config.BuiltinWarpInitialized = false;
                throw new IOException("Could not save WARP initialization state.");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("WARP initialization", ex);
            NoticeManager.Instance.Enqueue($"WARP initialization failed; will retry next launch: {ex.Message}");
        }
        finally { InitializeLock.Release(); }
    }

    public static async Task RegisterPendingAsync(Config config)
    {
        try
        {
            var profiles = await AppManager.Instance.ProfileItems(string.Empty) ?? [];
            foreach (var profile in profiles.Where(x => x.GetProtocolExtra().WarpProfileType == OberonRuntimeManager.WarpProfileType
                && x.GetProtocolExtra().WarpAmneziaRegistrationPending))
            {
                try { await OberonRuntimeManager.EnsureRegisteredAsync(profile); }
                catch (Exception ex)
                {
                    Logging.SaveLog("WARP initial registration", ex);
                    NoticeManager.Instance.Enqueue("WARP AmneziaWG is saved. Registration failed; it will retry when you connect. " + ex.Message);
                }
            }
        }
        catch (Exception ex) { Logging.SaveLog("WARP initial registration", ex); }
    }
}
