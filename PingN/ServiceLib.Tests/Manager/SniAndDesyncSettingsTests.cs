namespace ServiceLib.Tests.Manager;

public class SniAndDesyncSettingsTests
{
    [Test]
    public async Task SniDefaults_MatchProvidedConfigurationWithAutomaticListenPort()
    {
        var settings = new PingNSniSpoofingSettings();
        await (settings.ListenPort == string.Empty).Should().BeTrue();
        await (settings.Connect == "104.19.229.21:443,104.17.110.184:443,104.19.230.21:443").Should().BeTrue();
        await (PingNSniSpoofingSettings.DefaultFakeSni == "hcaptcha.com,dash.cloudflare.com,api.hcaptcha.com").Should().BeTrue();
        await (settings.IpRouting == "round-robin").Should().BeTrue();
        await (settings.SniRouting == "round-robin").Should().BeTrue();
        await settings.EnableFragment.Should().BeFalse();
    }

    [Test]
    public async Task Lists_NormalizeUnorderedWhitespaceAndSeparatorsForCli()
    {
        var sniOk = PingNSniSpoofingSettings.TryNormalizeFakeSni(
            " hcaptcha.com  ,dash.cloudflare.com;\n api.hcaptcha.com ", out var names, out _);
        var connectOk = PingNSniSpoofingSettings.TryNormalizeConnect(
            "104.19.229.21 : 443,\n104.17.110.184:443; 104.19.230.21:443", out var endpoints, out _);
        await sniOk.Should().BeTrue();
        await connectOk.Should().BeTrue();
        await (names == "hcaptcha.com,dash.cloudflare.com,api.hcaptcha.com").Should().BeTrue();
        await (endpoints == "104.19.229.21:443,104.17.110.184:443,104.19.230.21:443").Should().BeTrue();
        await PingNSniSpoofingSettings.TryNormalizeConnect("a.example:443 b.example:8443", out _, out _).Should().BeFalse();
    }

    [Test]
    public async Task DesyncCandidates_StayInSelectedProfileAndRetainCurrentFakeSni()
    {
        var selected = PingNDesyncSettings.DefaultsFor("SNI spoof - WrongSeq")
            with { BadSequence = -43210, FakeSni = "example.com" };
        var candidates = DesyncPresetScanner.BuildCandidates(selected);
        await (candidates.Count > 1).Should().BeTrue();
        await (candidates[0] == selected).Should().BeTrue();
        await candidates.All(item => item.Profile == selected.Profile && item.FakeSni == "example.com").Should().BeTrue();
        await candidates.Any(item => item.BadSequence != selected.BadSequence).Should().BeTrue();
    }
}
