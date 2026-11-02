namespace ServiceLib.Tests.Psiphon;

public class PsiphonRoutingTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void UserTrafficUsesPsiphonAndBootstrapUsesSelectedProfile(bool singbox)
    {
        var root = Fixture(singbox);
        var original = root["outbounds"]![0]!.ToJsonString();
        PsiphonRuntimeManager.ConfigureRouting(root, singbox, 33001, 33002);
        var rules = root[singbox ? "route" : "routing"]!["rules"]!.AsArray();
        var key = singbox ? "outbound" : "outboundTag";
        Check(rules[0]![key]!.GetValue<string>() == "proxy", "Bootstrap must use the selected profile");
        Check(rules[3]![key]!.GetValue<string>() == "pingn-psiphon-exit", "User TCP must go through Psiphon before bypass rules");
        Check(root["outbounds"]![0]!.ToJsonString() == original, "Selected outbound must remain intact");
        Check(root["inbounds"]!.AsArray().Last()!["listen"]!.GetValue<string>() == "127.0.0.1", "Bootstrap listener must be private");
        Check(rules[2]!["network"]!.GetValue<string>() == "udp", "UDP must not fall back to original IP");
        Check(singbox ? rules[2]!["action"]!.GetValue<string>() == "reject" : rules[2]![key]!.GetValue<string>() == "pingn-psiphon-block", "UDP must fail closed");
        Check(root["outbounds"]!.AsArray().Single(x => x!["tag"]!.GetValue<string>() == "pingn-psiphon-exit") is not null, "One Psiphon exit only");
    }

    [Test]
    public void SingboxWireGuardEndpointCanBeTheUpstream()
    {
        var root = Fixture(true);
        root["endpoints"] = new JsonArray(root["outbounds"]![0]!.DeepClone());
        root["outbounds"]!.AsArray().RemoveAt(0);
        PsiphonRuntimeManager.ConfigureRouting(root, true, 33001, 33002);
        Check(root["route"]!["rules"]![0]!["outbound"]!.GetValue<string>() == "proxy", "WireGuard endpoint must remain reachable");
    }

    [Test]
    public void SavedFlagSurvivesProfileCopyAndLegacyProfilesDefaultOff()
    {
        var node = new ProfileItem();
        Check(!node.GetProtocolExtra().PsiphonOver, "Old profiles must default off");
        node.SetProtocolExtra(node.GetProtocolExtra() with { PsiphonOver = true });
        Check(JsonUtils.DeepCopy(node)!.GetProtocolExtra().PsiphonOver, "Copy must preserve enabled state");
    }

    [Test]
    public void DuplicateWrappingIsRejected()
    {
        var root = Fixture(false);
        PsiphonRuntimeManager.ConfigureRouting(root, false, 33001, 33002);
        try { PsiphonRuntimeManager.ConfigureRouting(root, false, 33003, 33004); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Double wrapping should fail before generating a loop");
    }

    [Test]
    public void ApiInboundIsNotHijacked()
    {
        var root = Fixture(false);
        root["inbounds"]!.AsArray().Add(new JsonObject { ["tag"] = "api", ["protocol"] = "dokodemo-door" });
        PsiphonRuntimeManager.ConfigureRouting(root, false, 33001, 33002);
        var userRule = root["routing"]!["rules"]![3]!["inboundTag"]!.AsArray();
        Check(!userRule.Any(x => x!.GetValue<string>() == "api"), "API traffic must remain local");
    }

    private static JsonObject Fixture(bool singbox) => JsonNode.Parse(singbox ? """
        {"inbounds":[{"type":"mixed","tag":"socks","listen":"127.0.0.1","listen_port":10808}],
         "outbounds":[{"type":"socks","tag":"proxy","server":"127.0.0.1","server_port":19000},{"type":"direct","tag":"direct"}],
         "route":{"rules":[{"outbound":"direct"}],"final":"proxy"}}
        """ : """
        {"inbounds":[{"protocol":"socks","tag":"socks","listen":"127.0.0.1","port":10808}],
         "outbounds":[{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"127.0.0.1","port":19000}]}},{"protocol":"freedom","tag":"direct"}],
         "routing":{"rules":[{"type":"field","outboundTag":"direct","network":"tcp,udp"}]}}
        """).AsObject();

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
