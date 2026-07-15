using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientRecoverableDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverablePlayResponseIsInterrupted_PreservesLogicalDeadlineAcrossRetry ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost play transition response")));
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
            new UnityRequestPayload.PlayEnter());

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                dispatchRequest,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, transportClient.Timeouts.Count);
        Assert.Equal(TimeSpan.FromSeconds(5), transportClient.Timeouts[0]);
        Assert.True(transportClient.Timeouts[1] < transportClient.Timeouts[0]);
        Assert.True(transportClient.Timeouts[1] > TimeSpan.FromSeconds(4));
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.PlayEnter,
            UnityIpcMethod.PlayEnter);
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.All(
            requests,
            request => Assert.Equal(
                DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(5),
                request.RequestDeadlineUtc));
        Assert.All(requests, request => Assert.True(request.RequestDeadlineRemainingMilliseconds > 1000));
        Assert.True(
            requests[1].RequestDeadlineRemainingMilliseconds
            < requests[0].RequestDeadlineRemainingMilliseconds);
        Assert.Equal(requests[0].Method, requests[1].Method);
        Assert.Equal(requests[0].Payload.GetRawText(), requests[1].Payload.GetRawText());
        Assert.All(requests, request => Assert.False(request.Payload.TryGetProperty("timeoutMilliseconds", out _)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenDispatchUsesRotatedSessionToken_ReloadsSessionAndRetriesSameRequest ()
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
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.PlayEnter,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
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
        Assert.True(
            requests[1].RequestDeadlineRemainingMilliseconds
            < requests[0].RequestDeadlineRemainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRotatedSessionTokenAttemptLosesRecoverableResponse_RetriesSameRequest ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(static _ => throw new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost response after session token rotation")));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.PlayEnter,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();

        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.PlayEnter,
            UnityIpcMethod.PlayEnter,
            UnityIpcMethod.PlayEnter);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
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
            new UnityIpcDispatchRequest(
                UnityIpcMethod.PlayEnter,
                CreateDispatchPayload(),
                UnityBatchmodeLaunchOptions.Default),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
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
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
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
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.Compile,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
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
        Assert.True(
            requests[1].RequestDeadlineRemainingMilliseconds
            < requests[0].RequestDeadlineRemainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchConnectionRefusalOutlivesEndpointAbsenceGrace_ReturnsDaemonNotRunning ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => throw new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.Compile,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
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
    public async Task SendAsync_WhenRecoverableSessionTokenIsTemporarilyUnavailableDuringRecovery_WaitsAndSendsRecoveredSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
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
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.PlayEnter,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
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
