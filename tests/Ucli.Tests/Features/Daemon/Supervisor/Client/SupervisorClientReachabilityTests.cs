using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientReachabilityTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsDead_ReturnsUnreachable ()
    {
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);

        var result = await client.ProbeReachabilityAsync(
            SupervisorClientTestSupport.CreateManifest(processId: int.MaxValue),
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.Unreachable, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsAlive_ReturnsTimedOut ()
    {
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);

        var result = await client.ProbeReachabilityAsync(
            SupervisorClientTestSupport.CreateManifest(processId: Environment.ProcessId),
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.TimedOut, result);
    }
}
