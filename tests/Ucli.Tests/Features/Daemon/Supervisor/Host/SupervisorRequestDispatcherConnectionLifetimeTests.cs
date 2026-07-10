using MackySoft.Tests;
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
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.AsynchronousIgnoringCancellation);

        var handleTask = dispatcher.HandleConnectionAsync(
            stream,
            runtimeContext,
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None);
        await TestAwaiter.WaitAsync(
            stream.ReadStarted,
            "Supervisor initial frame read",
            SignalWaitTimeout);

        try
        {
            await TestAwaiter.WaitAsync(
                handleTask,
                "Supervisor initial frame deadline",
                SignalWaitTimeout);
        }
        finally
        {
            stream.CompleteRead();
            await TestAwaiter.WaitAsync(
                stream.ReadReturned,
                "Supervisor initial frame read return",
                SignalWaitTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenInitialReadBlocksBeforeReturningValueTask_ReturnsAtInitialFrameDeadline ()
    {
        var dispatcher = CreateDispatcher();
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
            await TestAwaiter.WaitAsync(
                stream.ReadStarted,
                "Synchronous supervisor initial frame read",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                handleTask,
                "Synchronous supervisor initial frame deadline",
                TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            stream.CompleteRead();
            await TestAwaiter.WaitAsync(
                stream.ReadReturned,
                "Synchronous supervisor initial frame read return",
                SignalWaitTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenInitialReadFaultsAfterDeadline_ObservesLateFault ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.AsynchronousIgnoringCancellation);
        var handleTask = dispatcher.HandleConnectionAsync(
            stream,
            runtimeContext,
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None);
        await TestAwaiter.WaitAsync(
            stream.ReadStarted,
            "Supervisor initial frame read",
            SignalWaitTimeout);
        await TestAwaiter.WaitAsync(
            handleTask,
            "Supervisor initial frame deadline",
            SignalWaitTimeout);

        stream.FailRead(new ApplicationException("late initial frame read fault"));

        await TestAwaiter.WaitAsync(
            stream.ReadReturned,
            "Late supervisor initial frame read fault",
            SignalWaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSingleRequestCompletes_ReleasesReadAndWriteDeadlineTimers ()
    {
        var timeProvider = new ManualTimeProvider();
        var dispatcher = CreateDispatcher(timeProvider: timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var request = new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "completed-single-request",
            SessionToken: runtimeContext.Manifest.SessionToken,
            Method: SupervisorIpcContracts.PingMethod,
            Payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
            responseMode: IpcResponseMode.Single);

        var response = await SendRequestAsync(dispatcher, runtimeContext, request);

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSingleResponseWriteAndDisposeBlock_ReturnsAtFrameDeadline ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        var request = new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "blocking-single-response-write",
            SessionToken: runtimeContext.Manifest.SessionToken,
            Method: SupervisorIpcContracts.PingMethod,
            Payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
            responseMode: IpcResponseMode.Single);
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
            exception => fatalException.TrySetResult(exception));
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
            await TestAwaiter.WaitAsync(
                stream.WriteStarted,
                "Supervisor single-response write",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                handlerReturned.Task,
                "Supervisor single-response frame deadline",
                SupervisorConstants.ResponseFrameWriteTimeout + TimeSpan.FromSeconds(1));
            await TestAwaiter.WaitAsync(
                stream.DisposeStarted,
                "Supervisor timed-out response stream disposal",
                SignalWaitTimeout);
            Assert.False(fatalException.Task.IsCompleted);
        }
        finally
        {
            stream.CompleteWrite();
            stream.CompleteDispose();
            connectionGroup.Release();
            await connectionGroup.DrainAsync(SignalWaitTimeout);
        }
    }
}
