namespace ServiceLib.Tests.Manager;

public class WarpRegistrationProxySessionTests
{
    [Test]
    public async Task RegistrationProxyOverridesInheritedBypassAndProxySettings()
    {
        var info = new System.Diagnostics.ProcessStartInfo();
        info.Environment["NO_PROXY"] = "*";
        info.Environment["HTTPS_PROXY"] = "http://old-proxy:3128";
        WarpRegistrationProxySession.ApplyEnvironment(info, "http://127.0.0.1:23456");
        await info.Environment["HTTPS_PROXY"].Should().BeEqualTo("http://127.0.0.1:23456");
        await info.Environment["https_proxy"].Should().BeEqualTo("http://127.0.0.1:23456");
        await info.Environment["NO_PROXY"].Should().BeEqualTo("localhost,127.0.0.1,::1");
        WarpRegistrationProxySession.ApplyEnvironment(info, null);
        await info.Environment["HTTPS_PROXY"].Should().BeEqualTo("");
        await info.Environment["http_proxy"].Should().BeEqualTo("");
    }
}
