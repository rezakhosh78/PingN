using ServiceLib.Models.Dto;

namespace ServiceLib.Tests.Manager;

public class ProfilePresentationTests
{
    [Test]
    public async Task DnsAddressColumnUsesDomainWithoutChangingLoopbackRouting()
    {
        var dnstt = new ProtocolExtraItem { DnsTunnel = new DnsTunnelSettings { Kind = "dnstt-ssh", Domain = "tunnel.example.org" } };
        await ProfileDisplayAddress.Resolve("127.0.0.1", dnstt).Should().BeEqualTo("tunnel.example.org");
        var master = new DnsTunnelSettings();
        master.MasterOptions["DOMAINS"] = "[\"a.example.org\",\"b.example.org\"]";
        await ProfileDisplayAddress.Resolve("127.0.0.1", new ProtocolExtraItem { DnsTunnel = master }).Should().BeEqualTo("a.example.org, b.example.org");
    }

    [Test]
    public async Task WarpAddressColumnUsesSavedEndpoint()
    {
        var awg = new ProtocolExtraItem { WarpProfileType = OberonRuntimeManager.WarpProfileType,
            WarpAmneziaConfig = "[Peer]\nEndpoint = 162.159.192.1:2408\n" };
        await ProfileDisplayAddress.Resolve("127.0.0.1", awg).Should().BeEqualTo("162.159.192.1:2408");
        var masque = new ProtocolExtraItem { WarpProfileType = WarpMasqueRuntimeManager.WarpProfileType,
            WarpMasqueEndpoint = "162.159.197.1", WarpMasqueEndpointPort = 443 };
        await ProfileDisplayAddress.Resolve("127.0.0.1", masque).Should().BeEqualTo("162.159.197.1:443");
    }
}
