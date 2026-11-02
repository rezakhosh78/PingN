using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Manager;

public class WarpRegistrationProxyNativeTests
{
    [Test]
    public async Task XrayRegistrationProxyStartsWithAssetsAndSpacedPaths()
    {
        // Explicit opt-in executable supplied by the integration test runner.
        var executable = Environment.GetEnvironmentVariable("PINGN_TEST_XRAY");
        if (string.IsNullOrEmpty(executable)) return;
        var directory = Path.Combine(Path.GetTempPath(), "PingN Registration Fixture " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        ProcessService? process = null;
        try
        {
            var isolatedExecutable = Path.Combine(directory, OperatingSystem.IsWindows() ? "xray.exe" : "xray");
            File.Copy(executable, isolatedExecutable);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(isolatedExecutable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
            CoreConfigTestFactory.BindAppManagerConfig(config);
            var profile = WarpRegistrationCoordinator.CreateProxy1();
            var context = CoreConfigTestFactory.CreateContext(config, profile, ECoreType.Xray);
            var generated = new CoreConfigV2rayService(context).GenerateClientConfigContent();
            await generated.Success.Should().BeTrue();
            var json = JsonNode.Parse(generated.Data!.ToString()!)!.AsObject();
            // This loads a real geosite resource from the app's separate asset directory.
            json["dns"] = JsonNode.Parse("""{"servers":[{"address":"localhost","domains":["geosite:private"]}]}""");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            WarpRegistrationProxySession.PrepareConfiguration(json, false, port);
            var path = Path.Combine(directory, "registration proxy.json");
            await File.WriteAllTextAsync(path, json.ToJsonString());
            // Reproduce the former launch: no asset environment and an isolated cwd/exe.
            var probeInfo = new ProcessStartInfo(isolatedExecutable)
            {
                WorkingDirectory = directory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            probeInfo.ArgumentList.Add("run"); probeInfo.ArgumentList.Add("-test");
            probeInfo.ArgumentList.Add("-c"); probeInfo.ArgumentList.Add(path);
            probeInfo.Environment.Remove(Global.XrayLocalAsset);
            using (var probe = Process.Start(probeInfo)!)
            {
                var stdout = probe.StandardOutput.ReadToEndAsync();
                var stderr = probe.StandardError.ReadToEndAsync();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await probe.WaitForExitAsync(deadline.Token);
                var missingAssets = (await stdout) + (await stderr);
                await (probe.ExitCode != 0).Should().BeTrue();
                await missingAssets.Contains("geosite", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            }
            var core = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray)!;
            var environment = WarpRegistrationProxySession.CreateCoreEnvironment(core);
            await environment.ContainsKey(Global.XrayLocalAsset).Should().BeTrue();
            await environment.ContainsKey(Global.XrayLocalCert).Should().BeTrue();
            environment[Global.XrayLocalAsset] = Path.GetDirectoryName(executable)!;
            environment[Global.XrayLocalCert] = Path.GetDirectoryName(executable)!;
            var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
            process = new ProcessService(isolatedExecutable, string.Format(core.Arguments, path.AppendQuotes()), directory,
                true, false, environment, (_, line) => { output.Enqueue(line); return Task.CompletedTask; });
            await process.StartAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    await process.WaitForExitAsync(timeout.Token);
                    throw new Exception(string.Join(Environment.NewLine, output));
                }
                try { using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token); break; }
                catch (SocketException) { await Task.Delay(50, timeout.Token); }
            }
            await process.HasExited.Should().BeFalse();
        }
        finally
        {
            if (process is not null) { await process.StopAsync(); process.Dispose(); }
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task FastCoreExitRetainsFinalErrorOutput()
    {
        if (!OperatingSystem.IsLinux()) return;
        var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var process = new ProcessService("/bin/sh", "-c \"echo registration-fixture-error >&2; exit 7\"", Path.GetTempPath(),
            true, false, null, (_, message) => { output.Enqueue(message); return Task.CompletedTask; });
        await process.StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(timeout.Token);
        await process.ExitCode.Should().BeEqualTo(7);
        await string.Join("", output).Contains("registration-fixture-error").Should().BeTrue();
    }
}
