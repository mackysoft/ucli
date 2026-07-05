using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientRecoverableDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchLosesResponse_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse("unused"));
        transportClient.EnqueueException(new EndOfStreamException("lost response"));
        transportClient.EnqueueResponse(CreateResponse("req-recovered"));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            new UnityIpcDispatchRequest(IpcMethodNames.PlayEnter, CreateDispatchPayload(), isRecoverable: true),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredDispatchesWithReloadedSessionToken(
            transportClient,
            IpcMethodNames.PlayEnter,
            "daemon-token-1",
            "daemon-token-2");
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.StartsWith($"{IpcMethodNames.PlayEnter}-", requestId, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchConnectionIsRefused_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse("unused"));
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        transportClient.EnqueueResponse(CreateResponse("req-recovered"));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(IpcMethodNames.Compile, CreateDispatchPayload(), isRecoverable: true),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredDispatchesWithReloadedSessionToken(
            transportClient,
            IpcMethodNames.Compile,
            "daemon-token-1",
            "daemon-token-2");
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.StartsWith($"{IpcMethodNames.Compile}-", requestId, StringComparison.Ordinal);
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
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(IpcMethodNames.Compile, CreateDispatchPayload(), isRecoverable: true),
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
            IpcMethodNames.Compile,
            maximumAttempts: 19);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableResponseAttemptTimesOutBeforeDeadline_RetriesWithSameRequestId ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse("unused"));
        transportClient.EnqueueException(new TimeoutException("response wait timed out"));
        transportClient.EnqueueResponse(CreateResponse("req-recovered"));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            timeProvider: timeProvider);
        var attemptTimeout = TimeSpan.FromMilliseconds(250);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    IpcMethodNames.PlayExit,
                    CreateDispatchPayload(),
                    isRecoverable: true,
                    recoverableResponseAttemptTimeout: attemptTimeout),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requestId = DaemonIpcDispatchAssert.RecoverableDispatchRetriedWithReloadedSessionTokenAndAttemptTimeout(
            transportClient,
            IpcMethodNames.PlayExit,
            "daemon-token-1",
            "daemon-token-2",
            attemptTimeout);
        Assert.StartsWith($"{IpcMethodNames.PlayExit}-", requestId, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableSessionTokenIsTemporarilyUnavailableDuringRecovery_WaitsAndSendsRecoveredSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse("unused"));
        transportClient.EnqueueResponse(CreateResponse("req-recovered-session"));
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var recoveryWaiter = CreateRecoveryWaiter(session, timeProvider);
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
                new UnityIpcDispatchRequest(IpcMethodNames.PlayEnter, CreateDispatchPayload(), isRecoverable: true),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var request = DaemonIpcDispatchAssert.SingleDispatchSent(
            transportClient,
            IpcMethodNames.PlayEnter,
            "daemon-token-2");
        Assert.StartsWith($"{IpcMethodNames.PlayEnter}-", request.RequestId, StringComparison.Ordinal);
    }
}
