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
        await TestAwaiter.WaitAsync(
            stream.ReadStarted,
            "Supervisor initial frame read",
            SignalWaitTimeout);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)),
            "Supervisor initial frame timer",
            SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

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
            await TestAwaiter.WaitAsync(
                stream.ReadStarted,
                "Synchronous supervisor initial frame read",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)),
                "Synchronous supervisor initial frame timer",
                SignalWaitTimeout);
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
            await TestAwaiter.WaitAsync(
                handleTask,
                "Synchronous supervisor initial frame deadline",
                SignalWaitTimeout);
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
        await TestAwaiter.WaitAsync(
            stream.ReadStarted,
            "Supervisor initial frame read",
            SignalWaitTimeout);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)),
            "Supervisor initial frame timer",
            SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
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
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: runtimeContext.Manifest.SessionToken,
            method: SupervisorIpcContracts.PingMethod,
            payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single));

        var response = await SendRequestAsync(dispatcher, runtimeContext, request);

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSingleResponseWriteAndDisposeBlock_ReturnsAtFrameDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var dispatcher = CreateDispatcher(timeProvider: timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var request = new IpcRequest(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: runtimeContext.Manifest.SessionToken,
            method: SupervisorIpcContracts.PingMethod,
            payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single));
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
            await TestAwaiter.WaitAsync(
                stream.WriteStarted,
                "Supervisor single-response write",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                timeProvider.WaitForTimerDueWithinAsync(SupervisorConstants.ResponseFrameWriteTimeout),
                "Supervisor single-response frame timer",
                SignalWaitTimeout);
            timeProvider.Advance(SupervisorConstants.ResponseFrameWriteTimeout);
            await TestAwaiter.WaitAsync(
                handlerReturned.Task,
                "Supervisor single-response frame deadline",
                SignalWaitTimeout);
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
            await TestAwaiter.WaitAsync(
                connectionGroup.DrainAsync(SignalWaitTimeout),
                "Supervisor connection drain",
                SignalWaitTimeout);
        }
    }
}
