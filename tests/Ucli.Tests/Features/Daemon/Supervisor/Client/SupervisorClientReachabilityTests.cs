using MackySoft.Ucli.Contracts.Ipc;
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenTransportIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var responseSource = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, _, _, cancellationToken) =>
            {
                _ = cancellationToken.Register(() => cancellationObserved.TrySetResult());
                return new ValueTask<IpcResponse>(responseSource.Task);
            },
        };
        var client = new SupervisorClient(transportClient, timeProvider);
        var timeout = TimeSpan.FromSeconds(1);
        var resultTask = client.ProbeReachabilityAsync(
                SupervisorClientTestSupport.CreateManifest(),
                timeout,
                CancellationToken.None)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            timeProvider.Advance(timeout);

            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(SupervisorReachabilityProbeStatus.TimedOut, result);
            await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            responseSource.TrySetException(new TimeoutException("Release non-cooperative reachability probe."));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenManifestIsNull_ThrowsArgumentNullException ()
    {
        var client = new SupervisorClient(new StubIpcTransportClient(), TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => client
            .ProbeReachabilityAsync(null!, TimeSpan.FromSeconds(1), CancellationToken.None)
            .AsTask());

        Assert.Equal("manifest", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenTimeoutIsNotPositive_ThrowsArgumentOutOfRangeException ()
    {
        var client = new SupervisorClient(new StubIpcTransportClient(), TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client
            .ProbeReachabilityAsync(
                SupervisorClientTestSupport.CreateManifest(),
                TimeSpan.Zero,
                CancellationToken.None)
            .AsTask());

        Assert.Equal("timeout", exception.ParamName);
    }
}
