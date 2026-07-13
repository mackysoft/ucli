using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

namespace MackySoft.Ucli.Tests.Features.Daemon.Common.Ipc;

public sealed class DaemonIpcRequestSenderTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionIsMissing_ReturnsDaemonSessionNotAvailableWithoutTransportCall ()
    {
        var transportClient = new UnexpectedIpcTransportClient("Missing daemon session must stop before sending IPC requests.");
        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StaticDaemonSessionConnectionProvider(DaemonSessionConnectionResolutionResult.SessionNotAvailable()),
            recoveryWaiter: null);

        var result = await sender.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            sessionToken => CreateRequest("logs.unity.read", sessionToken),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonSessionNotAvailable, error.Code);
        Assert.Contains("--projectPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenResponseAttemptTimesOut_ReturnsTimeoutWithoutRetry ()
    {
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new TimeoutException("attempt timed out"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null);

        var result = await sender.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            sessionToken => CreateRequest("logs.unity.read", sessionToken),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        DaemonIpcDispatchAssert.SingleDispatchPreservedCallerTimeoutBudget(
            transportClient,
            expectedMethod: "logs.unity.read",
            expectedSessionToken: "daemon-token",
            minimumTimeout: TimeSpan.FromSeconds(4.9));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenConnectionIsRefusedDuringRecovery_RetriesWithReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess),
            timeProvider);
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                sessionToken => CreateRequest("logs.unity.read", sessionToken),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(transportClient, "logs.unity.read", "logs.unity.read");
        IpcRequestAssert.SessionTokens(requests, "daemon-token-1", "daemon-token-2");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenEndpointRemainsAbsent_ReturnsDaemonSessionNotAvailable ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        for (var i = 0; i < 20; i++)
        {
            transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        }

        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null,
            timeProvider: timeProvider);

        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                sessionToken => CreateRequest("logs.unity.read", sessionToken),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await TestAwaiter.WaitAsync(
            ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                    timeProvider,
                    sendTask,
                    DaemonTimeouts.ProbeAttemptTimeoutCap + retryDelay,
                    retryDelay)
                .AsTask(),
            "Endpoint absence daemon IPC manual-time drive",
            AsyncWaitTimeout);

        var result = await sendTask;

        Assert.False(result.IsSuccess);
        var requests = IpcRequestAssert.RetriedAtLeastOnce(transportClient);
        Assert.All(requests, request => Assert.Equal("logs.unity.read", request.Method));
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonSessionNotAvailable, error.Code);
        Assert.Contains("--projectPath", error.Message, StringComparison.Ordinal);
    }

    private static IpcRequest CreateRequest (
        string method,
        string sessionToken)
    {
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"{method}-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: method,
            Payload: JsonDocument.Parse("{}").RootElement.Clone(),
            responseMode: IpcResponseMode.Single);
    }

    private static IpcResponse CreateResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: JsonDocument.Parse("{}").RootElement.Clone(),
            Errors: Array.Empty<IpcError>());
    }

    private static RecordingIpcTransportClient CreateTransportClient ()
    {
        return new RecordingIpcTransportClient(static request => CreateResponse(request.RequestId));
    }

    private static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            sessionToken,
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-session.sock")));
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (DaemonSession session)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: session.EditorMode,
            LifecycleState: IpcEditorLifecycleState.DomainReloading,
            CompileState: IpcCompileState.Ready,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            EditorInstanceId = session.EditorInstanceId,
        };
    }

}
