using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientReachabilityTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenManifestTokenIsRejected_ReturnsSessionTokenRejected ()
    {
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Supervisor session token is invalid."));
            },
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.ProbeReachabilityAsync(
            SupervisorClientTestSupport.CreateManifest(),
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.SessionTokenRejected, result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenPingGenerationDiffersFromManifest_ReturnsUnreachable (
        bool processIdMatches,
        bool issuedAtUtcMatches)
    {
        var manifest = SupervisorClientTestSupport.CreateManifest();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(IpcResponseTestFactory.CreateSuccess(
                    request,
                    new SupervisorIpcContracts.PingResponse(
                        ProcessId: processIdMatches ? manifest.ProcessId : manifest.ProcessId + 1,
                        IssuedAtUtc: issuedAtUtcMatches
                            ? manifest.IssuedAtUtc
                            : manifest.IssuedAtUtc.AddSeconds(1))));
            },
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.ProbeReachabilityAsync(
            manifest,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.Unreachable, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsDead_ReturnsUnreachable ()
    {
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);

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
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.ProbeReachabilityAsync(
            SupervisorClientTestSupport.CreateManifest(processId: Environment.ProcessId),
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.TimedOut, result);
    }
}
