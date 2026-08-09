using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherConnectionLifetimeTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenConnectedPeerSendsNoInitialFrame_ReturnsAtInitialFrameDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var dispatcher = CreateDispatcher(timeProvider: timeProvider);
        var runtimeContext = CreateRuntimeContext();
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.AsynchronousIgnoringCancellation);

        var handleTask = dispatcher.HandleConnectionAsync(
            stream,
            runtimeContext,
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None);
        await stream.ReadStarted.WaitAsync(SignalWaitTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)).WaitAsync(SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        try
        {
            await handleTask.WaitAsync(SignalWaitTimeout);
        }
        finally
        {
            stream.CompleteRead();
            await stream.ReadReturned.WaitAsync(SignalWaitTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenInitialReadBlocksBeforeReturningValueTask_ReturnsAtInitialFrameDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var dispatcher = CreateDispatcher(timeProvider: timeProvider);
        var runtimeContext = CreateRuntimeContext();
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.SynchronousBeforeValueTaskReturn);

        var handleTask = Task.Run(() => dispatcher.HandleConnectionAsync(
            stream,
            runtimeContext,
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None));
        try
        {
            await stream.ReadStarted.WaitAsync(SignalWaitTimeout);
            await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)).WaitAsync(SignalWaitTimeout);
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
            await handleTask.WaitAsync(SignalWaitTimeout);
        }
        finally
        {
            stream.CompleteRead();
            await stream.ReadReturned.WaitAsync(SignalWaitTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenInitialReadFaultsAfterDeadline_ObservesLateFault ()
    {
        var timeProvider = new ManualTimeProvider();
        var dispatcher = CreateDispatcher(timeProvider: timeProvider);
        var runtimeContext = CreateRuntimeContext();
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.AsynchronousIgnoringCancellation);
        var handleTask = dispatcher.HandleConnectionAsync(
            stream,
            runtimeContext,
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None);
        await stream.ReadStarted.WaitAsync(SignalWaitTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)).WaitAsync(SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        await handleTask.WaitAsync(SignalWaitTimeout);

        stream.FailRead(new ApplicationException("late initial frame read fault"));

        await stream.ReadReturned.WaitAsync(SignalWaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSingleRequestCompletes_ReleasesReadAndWriteDeadlineTimers ()
    {
        var timeProvider = new ManualTimeProvider();
        var dispatcher = CreateDispatcher(timeProvider: timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var request = new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
            method: TextVocabulary.GetText(SupervisorIpcMethod.Ping),
            payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
            responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue);

        var response = await SendRequestAsync(dispatcher, runtimeContext, request);

        Assert.Equal(IpcResponseStatus.Ok, response.Status);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSingleResponseWriteAndDisposeBlock_ReturnsAtFrameDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var dispatcher = CreateDispatcher(timeProvider: timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var request = new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
            method: TextVocabulary.GetText(SupervisorIpcMethod.Ping),
            payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
            responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue);
        using var requestBytes = new MemoryStream();
        await IpcFrameCodec.WriteModelAsync(
            requestBytes,
            request,
            IpcJsonSerializerOptions.Default);
        var stream = new SupervisorControlledWriteStream(requestBytes.ToArray());
        var handlerReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fatalException = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionGroup = new SupervisorTransportConnectionGroup(
            static connectionStream => connectionStream.Dispose(),
            exception => fatalException.TrySetResult(exception),
            timeProvider);
        Assert.True(connectionGroup.TryStart(
            stream,
            async (connectionStream, cancellationToken) =>
            {
                await dispatcher.HandleConnectionAsync(
                    connectionStream,
                    runtimeContext,
                    SupervisorConstants.InitialFrameReadTimeout,
                    cancellationToken);
                handlerReturned.TrySetResult();
            },
            maximumActiveConnections: 1,
            CancellationToken.None));

        try
        {
            await stream.WriteStarted.WaitAsync(SignalWaitTimeout);
            await timeProvider.WaitForTimerDueWithinAsync(SupervisorConstants.ResponseFrameWriteTimeout).WaitAsync(SignalWaitTimeout);
            timeProvider.Advance(SupervisorConstants.ResponseFrameWriteTimeout);
            await handlerReturned.Task.WaitAsync(SignalWaitTimeout);
            await stream.DisposeStarted.WaitAsync(SignalWaitTimeout);
            Assert.False(fatalException.Task.IsCompleted);
        }
        finally
        {
            stream.CompleteWrite();
            stream.CompleteDispose();
            connectionGroup.Release();
            await connectionGroup.DrainAsync(SignalWaitTimeout).WaitAsync(SignalWaitTimeout);
        }
    }
}
