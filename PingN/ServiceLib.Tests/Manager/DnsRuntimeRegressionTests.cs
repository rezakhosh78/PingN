using ServiceLib.Models.Dto;

namespace ServiceLib.Tests.Manager;

public class DnsRuntimeRegressionTests
{
    [Test]
    [Arguments("[MasterDNS] [stderr] connection failed", "MasterDNS")]
    [Arguments("[DNSTT] [stdout] session established", "DNSTT")]
    [Arguments("[DNSTT SSH] authentication failed", "DNSTT")]
    [Arguments("1405/08/10 18:22:47 [MasterDNS] exit code 1", "MasterDNS")]
    [Arguments("Desync: ready", "Desync")]
    [Arguments("SNI Spoofing Plus: ready", "SNI Spoofing Plus")]
    [Arguments("Xray inbound bind failed", "Xray")]
    public async Task RuntimeMessagesReachTheirCategory(string message, string category)
        => await RuntimeLogCategories.Detect(message).Should().BeEqualTo(category);

    [Test]
    public async Task OfficialMasterClientArchiveNameIsAcceptedButServerIsRejected()
    {
        var entry = DnsTunnelCoreInstaller.SelectMasterClientEntry(
            ["README.md", "client/MasterDnsVPN_Client_Windows_AMD64_v2026.10.26.100415-5f83ee9.exe"], Architecture.X64);
        await entry.Should().BeEqualTo("client/MasterDnsVPN_Client_Windows_AMD64_v2026.10.26.100415-5f83ee9.exe");
        var rejected = false;
        try { DnsTunnelCoreInstaller.SelectMasterClientEntry(["MasterDnsVPN_Server_Windows_AMD64.exe"], Architecture.X64); }
        catch (IOException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    public async Task AListenerOwnedByAnotherProcessIsNotReused()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await DnsTunnelPortManager.IsPortAvailable(port).Should().BeFalse();
        listener.Stop();
        await DnsTunnelPortManager.IsPortAvailable(port).Should().BeTrue();
    }
}
