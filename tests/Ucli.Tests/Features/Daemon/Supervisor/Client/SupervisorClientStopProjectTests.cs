using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientStopProjectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StopProject_WhenTerminalArrivesAfterCommandDeadlineWithinGrace_PreservesResult ()
    {
        var timeProvider = new ManualTimeProvider();
        var requestObserved = new TaskCompletionSource<IpcRequestEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) =>
            {
                requestObserved.TrySetResult(request);
                return new ValueTask<IpcResponse>(terminalResponseSource.Task);
            },
        };
        var client = new SupervisorClient(transportClient, timeProvider);
        var commandTimeout = TimeSpan.FromSeconds(1);
        var deadline = ExecutionDeadline.Start(commandTimeout, timeProvider);

        var resultTask = client.StopProjectAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.NewGuid(),
                SupervisorClientTestSupport.CreateUnityProject(),
                deadline,
                CancellationToken.None)
            .AsTask();
        var request = await requestObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(commandTimeout.Add(TimeSpan.FromMilliseconds(500)));
        terminalResponseSource.TrySetResult(
            SupervisorProjectGatewayTestSupport.CreateStopProjectStoppedResponse(request));

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        var invocation = Assert.Single(transportClient.Invocations);
        Assert.True(invocation.UsesUnboundedResponseWait);
        Assert.Equal(commandTimeout, invocation.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StopProject_WhenTerminalNeverCompletes_ReturnsAtFiniteGraceDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var requestObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, _, _, cancellationToken) =>
            {
                requestObserved.TrySetResult();
                _ = cancellationToken.Register(() => cancellationObserved.TrySetResult());
                return new ValueTask<IpcResponse>(terminalResponseSource.Task);
            },
        };
        var client = new SupervisorClient(transportClient, timeProvider);
        var commandTimeout = TimeSpan.FromSeconds(1);
        var terminalTimeout = commandTimeout.Add(SupervisorConstants.StopProjectTerminalResponseGrace);
        var deadline = ExecutionDeadline.Start(commandTimeout, timeProvider);

        var resultTask = client.StopProjectAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.NewGuid(),
                SupervisorClientTestSupport.CreateUnityProject(),
                deadline,
                CancellationToken.None)
            .AsTask();
        await requestObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await timeProvider.WaitForTimerDueWithinAsync(terminalTimeout).WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            timeProvider.Advance(terminalTimeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            terminalResponseSource.TrySetException(new TimeoutException("Release non-cooperative stop response."));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StopProject_WhenRequestIdIsEmpty_ThrowsBeforeTransport ()
    {
        var transportClient = new StubIpcTransportClient();
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client
            .StopProjectAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.Empty,
                SupervisorClientTestSupport.CreateUnityProject(),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
                CancellationToken.None)
            .AsTask());

        Assert.Equal("requestId", exception.ParamName);
        Assert.Empty(transportClient.Invocations);
    }
}
