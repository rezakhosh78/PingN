namespace ServiceLib.Tests.Manager;

public class DnsTunnelSettingsTests
{
    [Test]
    public async Task MasterDnsRequiresRealServerIdentity()
    {
        var settings = new DnsTunnelSettings();
        var rejected = false;
        try { settings.Validate(18000); } catch (InvalidDataException) { rejected = true; }
        await rejected.Should().BeTrue();
        settings.MasterOptions["DOMAINS"] = "[\"tunnel.mydomain.org\"]";
        settings.MasterOptions["ENCRYPTION_KEY"] = "\"test-server-key\"";
        settings.Validate(18000);
    }

    [Test]
    public async Task DnsttRejectsPortCollisionButAllowsPasswordOnlyAuthentication()
    {
        var settings = new DnsTunnelSettings { Kind = "dnstt-ssh", Domain = "tunnel.mydomain.org",
            PublicKey = new string('a', 64), SshUser = "tunnel", SshPassword = "example" };
        foreach (var port in new[] { 17000 })
        {
            var rejected = false;
            try { settings.Validate(port); } catch (InvalidDataException) { rejected = true; }
            await rejected.Should().BeTrue();
        }
        settings.Validate(18000);
        settings.HostKeySha256 = "SHA256:" + new string('a', 43);
        settings.Validate(18000);
    }

    [Test]
    public async Task BackupRetainsDnsTunnelFieldsAndPendingWarpState()
    {
        var extra = new ProtocolExtraItem { DnsTunnel = new DnsTunnelSettings { Kind = "dnstt-ssh",
            Domain = "tunnel.mydomain.org", SshUser = "tunnel", HostKeySha256 = "SHA256:" + new string('a', 43) },
            WarpAmneziaRegistrationPending = true };
        var restored = JsonSerializer.Deserialize<ProtocolExtraItem>(JsonSerializer.Serialize(extra))!;
        await restored.DnsTunnel!.Domain.Should().BeEqualTo("tunnel.mydomain.org");
        await restored.WarpAmneziaRegistrationPending.Should().BeTrue();
    }
}
