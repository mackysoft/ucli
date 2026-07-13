using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
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
        var dispatchRequest = CreateDispatchRequest().WithResponseMode(IpcResponseMode.Stream);

        var sendTask = client.SendStreamingAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                dispatchRequest,
                TimeSpan.FromSeconds(5),
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
            UnityIpcMethod.OpsRead,
            "daemon-token-1",
            "daemon-token-2");
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WithServerExecutionTimeout_ReservesResponseGraceBeforeTransportDeadline ()
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
                TestRunPlatformCodec.EditMode,
                TestFilter: null,
                TestCategories: [],
                AssemblyNames: [],
                TestSettingsPath: null,
                ResultsXmlPath: "/tmp/results.xml",
                EditorLogPath: "/tmp/editor.log",
                FailFast: false,
                RunId: "run-1"))
            .WithResponseMode(IpcResponseMode.Stream);

        var result = await client.SendStreamingAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            dispatchRequest,
            TimeSpan.FromSeconds(30),
            (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(transportClient.Timeouts));
        var request = Assert.Single(transportClient.StreamingRequests);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcTestRunRequest payload, out _));
        Assert.Equal(29000, payload.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenDispatchIsRecoverable_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider(
            "Recoverable streaming dispatch should fail before session connection resolution.");
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);

        var result = await client.SendStreamingAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            new UnityIpcDispatchRequest(
                UnityIpcMethod.PlayEnter,
                CreateDispatchPayload(),
                isRecoverable: true,
                responseMode: IpcResponseMode.Stream),
            TimeSpan.FromSeconds(30),
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
                        UnityIpcMethod.OpsRead,
                        CreateDispatchPayload(),
                        responseMode: IpcResponseMode.Stream),
                    TimeSpan.FromSeconds(30),
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask();
        });

        Assert.Same(handlerException, exception);
        DaemonIpcDispatchAssert.SingleStreamingDispatchAttempted(transportClient, UnityIpcMethod.OpsRead);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenSessionTokenIsTemporarilyUnavailableDuringRecovery_WaitsAndSendsRecoveredSessionToken ()
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

        var sendTask = client.SendStreamingAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.TestRun,
                    CreateDispatchPayload(),
                    responseMode: IpcResponseMode.Stream),
                TimeSpan.FromSeconds(5),
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
            "daemon-token-2");
        Assert.NotEqual(Guid.Empty, request.RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenConnectionIsRefusedDuringRecovery_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var recoveryWaiter = CreateRecoveryWaiter(session);
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
                    responseMode: IpcResponseMode.Stream),
                TimeSpan.FromSeconds(5),
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
            "daemon-token-1",
            "daemon-token-2");
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.NotEqual(Guid.Empty, requestId);
    }
}
