using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientRecoverableDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverablePlayUsesBoundedResponseAttempts_PreservesLogicalExecutionBudgetInPayload ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new EndOfStreamException("lost play transition response"));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider);
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.PlayEnter(5000));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                dispatchRequest,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)],
            transportClient.Timeouts);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.PlayEnter,
            UnityIpcMethod.PlayEnter);
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.True(IpcPayloadCodec.TryDeserialize(requests[0].Payload, out IpcPlayEnterRequest firstPayload, out _));
        Assert.True(IpcPayloadCodec.TryDeserialize(requests[1].Payload, out IpcPlayEnterRequest secondPayload, out _));
        Assert.Equal(4500, firstPayload.TimeoutMilliseconds);
        Assert.Equal(4410, secondPayload.TimeoutMilliseconds);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_WhenDispatchUsesRotatedSessionToken_ReloadsSessionAndRetriesSameRequest (bool isRecoverable)
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(UnityIpcMethod.PlayEnter, CreateDispatchPayload(), isRecoverable: isRecoverable),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.PlayEnter,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchLosesResponse_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost response")));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            new UnityIpcDispatchRequest(UnityIpcMethod.PlayEnter, CreateDispatchPayload(), isRecoverable: true),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.PlayEnter,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.NotEqual(Guid.Empty, requestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchConnectionIsRefused_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(UnityIpcMethod.Compile, CreateDispatchPayload(), isRecoverable: true),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.Compile,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.NotEqual(Guid.Empty, requestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchConnectionRefusalOutlivesEndpointAbsenceGrace_ReturnsDaemonNotRunning ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => throw new SocketException((int)SocketError.ConnectionRefused));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(UnityIpcMethod.Compile, CreateDispatchPayload(), isRecoverable: true),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(100));

        Assert.True(sendTask.IsCompleted);
        var result = await sendTask;
        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        DaemonIpcDispatchAssert.RetriedDispatchesWithSameRequestId(
            transportClient,
            UnityIpcMethod.Compile,
            maximumAttempts: 19);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableResponseAttemptTimesOutBeforeDeadline_RetriesWithSameRequestId ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new TimeoutException("response wait timed out"));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);
        var attemptTimeout = TimeSpan.FromMilliseconds(250);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.PlayExit,
                    CreateDispatchPayload(),
                    isRecoverable: true,
                    recoverableResponseAttemptTimeout: attemptTimeout),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requestId = DaemonIpcDispatchAssert.RecoverableDispatchRetriedWithReloadedSessionTokenAndAttemptTimeout(
            transportClient,
            UnityIpcMethod.PlayExit,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            attemptTimeout);
        Assert.NotEqual(Guid.Empty, requestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableSessionTokenIsTemporarilyUnavailableDuringRecovery_WaitsAndSendsRecoveredSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var recoveryWaiter = CreateRecoveryWaiter(session);
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            DaemonSessionConnectionResolutionResult.SessionNotAvailable(),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(UnityIpcMethod.PlayEnter, CreateDispatchPayload(), isRecoverable: true),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var request = DaemonIpcDispatchAssert.SingleDispatchSent(
            transportClient,
            UnityIpcMethod.PlayEnter,
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        Assert.NotEqual(Guid.Empty, request.RequestId);
    }
}
