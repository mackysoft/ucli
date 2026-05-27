using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Features.Daemon.Common.Ipc;

public sealed class DaemonIpcRequestSenderTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionIsMissing_ReturnsDaemonSessionNotAvailableWithoutTransportCall ()
    {
        var transportClient = new StubIpcTransportClient();
        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StubDaemonSessionConnectionProvider(DaemonSessionConnectionResolutionResult.SessionNotAvailable()),
            recoveryWaiter: null);

        var result = await sender.SendAsync(
            CreateContext(),
            sessionToken => CreateRequest("logs.unity.read", sessionToken),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, transportClient.CallCount);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonSessionNotAvailable, error.Code);
        Assert.Contains("--projectPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenResponseAttemptTimesOut_ReturnsTimeoutWithoutRetry ()
    {
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueException(new TimeoutException("attempt timed out"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StubDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null);

        var result = await sender.SendAsync(
            CreateContext(),
            sessionToken => CreateRequest("logs.unity.read", sessionToken),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(transportClient.Requests);
        Assert.True(transportClient.Timeouts[0] > TimeSpan.FromSeconds(4.9));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenConnectionIsRefusedDuringRecovery_RetriesWithReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var session = CreateRecoveringSession();
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(session)),
            new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session))),
            new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess),
            timeProvider);
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = sender.SendAsync(
                CreateContext(),
                sessionToken => CreateRequest("logs.unity.read", sessionToken),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal(2, sessionConnectionProvider.CallCount);
        Assert.Equal("daemon-token-1", transportClient.Requests[0].SessionToken);
        Assert.Equal("daemon-token-2", transportClient.Requests[1].SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenEndpointRemainsAbsent_ReturnsDaemonSessionNotAvailable ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient();
        for (var i = 0; i < 20; i++)
        {
            transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        }

        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StubDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null,
            timeProvider: timeProvider);

        var sendTask = sender.SendAsync(
                CreateContext(),
                sessionToken => CreateRequest("logs.unity.read", sessionToken),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        for (var i = 0; i < 20 && !sendTask.IsCompleted; i++)
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
            await Task.Yield();
        }

        var result = await TestAwaiter.WaitAsync(sendTask, "Endpoint absence daemon IPC send", AsyncWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.True(transportClient.CallCount > 1);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonSessionNotAvailable, error.Code);
        Assert.Contains("--projectPath", error.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
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
            Payload: JsonDocument.Parse("{}").RootElement.Clone());
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

    private static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            sessionToken,
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-session.sock")));
    }

    private static DaemonSession CreateRecoveringSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "project-fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: DaemonEditorModeValues.Gui,
            OwnerKind: "user",
            CanShutdownProcess: false,
            EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10),
            OwnerProcessId: null)
        {
            EditorInstanceId = "editor-instance-1",
        };
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (DaemonSession session)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: session.EditorMode,
            LifecycleState: IpcEditorLifecycleStateCodec.DomainReloading,
            BlockingReason: IpcEditorBlockingReasonCodec.DomainReload,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            EditorInstanceId = session.EditorInstanceId,
        };
    }

    private sealed class StubIpcTransportClient : IIpcTransportClient
    {
        private readonly Queue<Func<IpcRequest, IpcResponse>> responses = new();

        private readonly Queue<Exception> exceptions = new();

        public int CallCount { get; private set; }

        public List<IpcRequest> Requests { get; } = new();

        public List<TimeSpan> Timeouts { get; } = new();

        public void EnqueueResponse (Func<IpcRequest, IpcResponse> response)
        {
            responses.Enqueue(response);
        }

        public void EnqueueException (Exception exception)
        {
            exceptions.Enqueue(exception);
        }

        public ValueTask<IpcResponse> SendAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Requests.Add(request);
            Timeouts.Add(timeout);

            if (exceptions.Count != 0)
            {
                throw exceptions.Dequeue();
            }

            return ValueTask.FromResult(responses.Count == 0
                ? CreateResponse(request.RequestId)
                : responses.Dequeue()(request));
        }

        public ValueTask<IpcResponse> SendStreamingAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(endpoint, request, timeout, cancellationToken);
        }

        public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan sendTimeout,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(endpoint, request, sendTimeout, cancellationToken);
        }
    }

    private sealed class StubDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
    {
        private readonly Queue<DaemonSessionConnectionResolutionResult> results;

        private DaemonSessionConnectionResolutionResult lastResult;

        public StubDaemonSessionConnectionProvider (params DaemonSessionConnectionResolutionResult[] results)
        {
            this.results = new Queue<DaemonSessionConnectionResolutionResult>(results);
            lastResult = results.Length == 0
                ? DaemonSessionConnectionResolutionResult.SessionNotAvailable()
                : results[^1];
        }

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (results.Count != 0)
            {
                lastResult = results.Dequeue();
            }

            return ValueTask.FromResult(lastResult);
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        private readonly DaemonSessionReadResult readResult;

        public StubDaemonSessionStore (DaemonSessionReadResult readResult)
        {
            this.readResult = readResult;
        }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(readResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonLifecycleStore : IDaemonLifecycleStore
    {
        private readonly DaemonLifecycleObservationReadResult readResult;

        public StubDaemonLifecycleStore (DaemonLifecycleObservationReadResult readResult)
        {
            this.readResult = readResult;
        }

        public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(readResult);
        }

        public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        private readonly DaemonProcessIdentityAssessmentStatus status;

        public StubDaemonProcessIdentityAssessor (DaemonProcessIdentityAssessmentStatus status)
        {
            this.status = status;
        }

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset? expectedProcessStartedAtUtc)
        {
            return new DaemonProcessIdentityAssessment(status, expectedProcessStartedAtUtc, null);
        }
    }
}
