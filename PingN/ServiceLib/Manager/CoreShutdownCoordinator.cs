namespace ServiceLib.Manager;

public static class CoreShutdownCoordinator
{
    // One failed auxiliary shutdown must never skip stopping the main traffic core.
    public static async Task<bool> StopAllAsync(IEnumerable<(string Name, Func<Task> Stop)> steps, Action<string, Exception> report)
    {
        var success = true;
        foreach (var (name, stop) in steps)
        {
            try { await stop(); }
            catch (Exception ex) { success = false; report(name, ex); }
        }
        return success;
    }
}
