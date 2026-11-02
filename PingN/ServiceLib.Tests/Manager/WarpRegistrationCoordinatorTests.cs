namespace ServiceLib.Tests.Manager;

public class WarpRegistrationCoordinatorTests
{
    [Test]
    public async Task ExplicitZeroTimeoutUsesProxyImmediatelyWithoutDirectAttempt()
    {
        var directCalls = 0;
        var result = await WarpRegistrationCoordinator.RunAutoAsync(
            _ => { directCalls++; return Task.FromResult("direct"); },
            _ => Task.FromResult("Proxy-1"), directAttemptTimeout: TimeSpan.Zero);
        await directCalls.Should().BeEqualTo(0);
        await result.Should().BeEqualTo("Proxy-1");
    }

    [Test]
    public async Task CanceledRegistrationDoesNotStartEitherAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = 0;
        var canceled = false;
        try
        {
            await WarpRegistrationCoordinator.RunAutoAsync(
                _ => { calls++; return Task.FromResult(1); },
                _ => { calls++; return Task.FromResult(2); }, cancellation.Token);
        }
        catch (OperationCanceledException) { canceled = true; }
        await canceled.Should().BeTrue();
        await calls.Should().BeEqualTo(0);
    }

    [Test]
    public async Task AutoUsesDirectSuccessWithoutStartingProxy()
    {
        var fallbackCalls = 0;
        var result = await WarpRegistrationCoordinator.RunAutoAsync(
            _ => Task.FromResult("direct"),
            _ => { fallbackCalls++; return Task.FromResult("proxy"); });
        await result.Should().BeEqualTo("direct");
        await fallbackCalls.Should().BeEqualTo(0);
    }

    [Test]
    public async Task AutoRetriesFailedDirectAttemptThroughProxy()
    {
        var order = new List<string>();
        var result = await WarpRegistrationCoordinator.RunAutoAsync(
            _ => { order.Add("direct"); return Task.FromException<string>(new IOException("unreachable")); },
            _ => { order.Add("Proxy-1"); return Task.FromResult("registered"); }, directAttemptTimeout: TimeSpan.FromSeconds(25));
        await result.Should().BeEqualTo("registered");
        await string.Join(",", order).Should().BeEqualTo("direct,Proxy-1");
    }

    [Test]
    public async Task UserCancellationDoesNotStartProxy()
    {
        using var cancellation = new CancellationTokenSource();
        var fallbackCalls = 0;
        var canceled = false;
        try
        {
            await WarpRegistrationCoordinator.RunAutoAsync<string>(
                _ => { cancellation.Cancel(); return Task.FromCanceled<string>(cancellation.Token); },
                _ => { fallbackCalls++; return Task.FromResult("unexpected"); }, cancellation.Token, TimeSpan.FromSeconds(25));
        }
        catch (OperationCanceledException) { canceled = true; }
        await canceled.Should().BeTrue();
        await fallbackCalls.Should().BeEqualTo(0);
    }

    [Test]
    public async Task Proxy1PreservesSuppliedTlsWebsocketConfiguration()
    {
        var profile = WarpRegistrationCoordinator.CreateProxy1();
        await profile.Remarks.Should().BeEqualTo("Proxy-1");
        await profile.ConfigType.Should().BeEqualTo(EConfigType.VLESS);
        await profile.Address.Should().BeEqualTo("pingng.novintech.site");
        await profile.Port.Should().BeEqualTo(443);
        await profile.Network.Should().BeEqualTo("ws");
        await profile.StreamSecurity.Should().BeEqualTo("tls");
        await profile.Sni.Should().BeEqualTo("rough-field-378e.pingplas4.workers.dev");
        await profile.GetTransportExtra().Host.Should().BeEqualTo(profile.Sni);
        await profile.GetTransportExtra().Path.Should().BeEqualTo("/urls/club?ed=2560");
        await profile.GetAllowInsecure().Should().BeFalse();
    }
}
