using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientStreamingTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenDispatchUsesRotatedSessionToken_ReloadsSessionAndRetriesSameRequest ()
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
        var dispatchRequest = new UnityIpcDispatchRequest(
            UnityIpcMethod.TestRun,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);

        var sendTask = client.SendStreamingAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                dispatchRequest,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None)
            .AsTask();

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredStreamingDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.TestRun,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.True(
            requests[1].RequestDeadlineRemainingMilliseconds
            < requests[0].RequestDeadlineRemainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WithRequestDeadline_PreservesAbsoluteDeadlineAlongsideTransportTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var response = CreateResponse(Guid.NewGuid());
        var transportClient = new RecordingIpcTransportClient(_ => response);
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);
        var dispatchRequest = new UnityIpcRequestBuilder()
            .Build(new UnityRequestPayload.TestRun(
                TestRunPlatform.EditMode,
                testFilter: null,
                testCategories: [],
                assemblyNames: [],
                failFast: false,
                runId: RunIdTestValues.Test));

        var result = await client.SendStreamingAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            dispatchRequest,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(transportClient.Timeouts));
        var request = Assert.Single(transportClient.StreamingRequests);
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(30), request.RequestDeadlineUtc);
        Assert.False(request.Payload.TryGetProperty("timeoutMilliseconds", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenMethodDoesNotSupportStreaming_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider(
            "Unsupported streaming dispatch should fail before session connection resolution.");
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);

        var result = await client.SendStreamingAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            new UnityIpcDispatchRequest(
                UnityIpcMethod.OpsRead,
                CreateDispatchPayload(),
                UnityBatchmodeLaunchOptions.Default),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressFrameHandlerFails_RethrowsHandlerException ()
    {
        var handlerException = new InvalidOperationException("progress frame rejected");
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcProgressFrameHandlerException(handlerException));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    new UnityIpcDispatchRequest(
                        UnityIpcMethod.TestRun,
                        CreateDispatchPayload(),
                        UnityBatchmodeLaunchOptions.Default),
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask();
        });

        Assert.Same(handlerException, exception);
        DaemonIpcDispatchAssert.SingleStreamingDispatchAttempted(transportClient, UnityIpcMethod.TestRun);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenSessionTokenIsTemporarilyUnavailableDuringRecovery_WaitsAndSendsRecoveredSessionToken ()
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

        var sendTask = client.SendStreamingAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.TestRun,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var request = DaemonIpcDispatchAssert.SingleStreamingDispatchSent(
            transportClient,
            UnityIpcMethod.TestRun,
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        Assert.NotEqual(Guid.Empty, request.RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenConnectionIsRefusedDuringRecovery_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var recoveryWaiter = CreateRecoveryWaiter(session, timeProvider);
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = client.SendStreamingAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.TestRun,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredStreamingDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.TestRun,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.NotEqual(Guid.Empty, requestId);
        Assert.True(
            requests[1].RequestDeadlineRemainingMilliseconds
            < requests[0].RequestDeadlineRemainingMilliseconds);
    }
}
