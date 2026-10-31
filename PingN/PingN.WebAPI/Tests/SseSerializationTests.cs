using PingN.WebAPI.Api;

namespace PingN.WebAPI.Tests;

public class SseSerializationTests
{
    [Test]
    public async Task NullEventDataSerializesAsJsonNull()
    {
        await WebApiEndpoints.SerializeEventData(null).Should().BeEqualTo("null");
    }
}
