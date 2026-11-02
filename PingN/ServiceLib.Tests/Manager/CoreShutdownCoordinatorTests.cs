namespace ServiceLib.Tests.Manager;

public class CoreShutdownCoordinatorTests
{
    [Test]
    public async Task AuxiliaryFailureDoesNotLeaveOtherTrafficCoresRunning()
    {
        var stopped = new List<string>();
        var errors = new List<string>();
        var result = await CoreShutdownCoordinator.StopAllAsync(new (string, Func<Task>)[]
        {
            ("main", () => { stopped.Add("main"); return Task.CompletedTask; }),
            ("faulty helper", () => Task.FromException(new IOException("fixture stop error"))),
            ("DNS", () => { stopped.Add("DNS"); return Task.CompletedTask; }),
            ("WARP", () => { stopped.Add("WARP"); return Task.CompletedTask; }),
        }, (name, error) => errors.Add(name));
        await result.Should().BeFalse();
        await stopped.Count.Should().BeEqualTo(3);
        await errors.Single().Should().BeEqualTo("faulty helper");
    }
}
