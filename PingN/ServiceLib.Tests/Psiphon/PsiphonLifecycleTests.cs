namespace ServiceLib.Tests.Psiphon;

public class PsiphonLifecycleTests
{
    [Test]
    public async Task NoticesReconnectStopAndBaseFailureDriveRealProcessState()
    {
        // Uses a real subprocess and sockets without contacting public infrastructure.
        if (!OperatingSystem.IsLinux()) return;
        var directory = Path.Combine(Path.GetTempPath(), "pingn-psiphon-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "fake-core");
        await File.WriteAllTextAsync(executable, """
            #!/bin/sh
            echo '{"noticeType":"ListeningSocksProxyPort","data":{"port":33002}}'
            sleep 1
            echo '{"noticeType":"Tunnels","data":{"count":1}}'
            sleep 1
            echo '{"noticeType":"Tunnels","data":{"count":0}}'
            sleep 1
            echo '{"noticeType":"Tunnels","data":{"count":1}}'
            sleep 30
            """ + "\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var id = Guid.NewGuid().ToString("N");
        var secondId = Guid.NewGuid().ToString("N");
        try
        {
            var alive = true;
            await PsiphonRuntimeManager.StartAsync(new(id, executable, "unused", directory, port, 33002), () => alive);
            await Wait(id, "Connecting");
            await Wait(id, "Connected");
            await Wait(id, "Connecting");
            await Wait(id, "Connected");
            alive = false;
            await Wait(id, "Disconnect");
            await PsiphonRuntimeManager.StopAsync();
            await PsiphonRuntimeManager.StartAsync(new(secondId, executable, "unused", directory, port, 33002), () => true);
            await Wait(secondId, "Connecting");
            await PsiphonRuntimeManager.StopAsync();
            await Wait(secondId, "Disconnect");
            await Wait(id, "Disconnect");
        }
        finally
        {
            await PsiphonRuntimeManager.StopAsync();
            listener.Stop();
            Directory.Delete(directory, true);
        }
    }

    private static async Task Wait(string id, string expected)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        while (PsiphonRuntimeManager.GetState(id).Text != expected)
            await Task.Delay(20, deadline.Token);
        var color = expected switch { "Connected" => "#16A34A", "Connecting" => "#D97706", _ => "#DC2626" };
        if (PsiphonRuntimeManager.GetState(id).Color != color) throw new Exception("Incorrect state color");
    }
}
