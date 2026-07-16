using System.Net.Sockets;
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
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
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
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredStreamingDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.TestRun,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WithRequestDeadline_PreservesAbsoluteDeadlineAlongsideTransportTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var response = CreateResponse(Guid.NewGuid());
        var transportClient = new RecordingIpcTransportClient(_ => response);
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
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
        var sessionStore = new UnexpectedDaemonSessionStore(
            "Unsupported streaming dispatch should fail before session connection resolution.");
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

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
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

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
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore,
            recoveryWaiter));

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
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredStreamingDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.TestRun,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.NotEqual(Guid.Empty, requestId);
    }
}
