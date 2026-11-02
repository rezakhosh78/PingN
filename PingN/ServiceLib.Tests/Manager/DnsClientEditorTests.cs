using ServiceLib.Models.Dto;

namespace ServiceLib.Tests.Manager;

public class DnsClientEditorTests
{
    [Test]
    public async Task UpdateRowsHaveSeparateStorageIdentities()
    {
        var master = new CheckUpdateModel { DnsCoreKind = "masterdns" };
        var dnstt = new CheckUpdateModel { DnsCoreKind = "dnstt-ssh" };
        await master.CoreTypeForStorage.Should().BeEqualTo("MasterDNS Client");
        await dnstt.CoreTypeForStorage.Should().BeEqualTo("DNSTT Client");
    }

    [Test]
    [Arguments(Architecture.X64, "dnstt-client-windows-amd64.exe")]
    [Arguments(Architecture.X86, "dnstt-client-windows-386.exe")]
    [Arguments(Architecture.Arm64, "dnstt-client-windows-arm64.exe")]
    public async Task OnlyClientArtifactsAreSelected(Architecture architecture, string expected)
        => await DnsTunnelCoreInstaller.ClientAssetName("dnstt-ssh", architecture).Should().BeEqualTo(expected);

    [Test]
    public async Task DropdownValuesRetainProtocolIds()
    {
        var encryption = MasterDnsEditorSchema.Choices("DATA_ENCRYPTION_METHOD", "2")!;
        await encryption.Single(x => x.Value == "2").Label.Should().BeEqualTo("2 — ChaCha20");
        var balancing = MasterDnsEditorSchema.Choices("RESOLVER_BALANCING_STRATEGY", "4")!;
        await balancing.Single(x => x.Value == "4").Label.Should().BeEqualTo("4 — Lowest Latency");
        await MasterDnsEditorSchema.Choices("LOCAL_DNS_ENABLED", "true")!.Count.Should().BeEqualTo(2);
    }
}
