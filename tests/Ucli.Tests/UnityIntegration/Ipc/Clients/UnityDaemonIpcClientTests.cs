using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessful_ResolvesSessionConnectionAndDelegatesToTransport ()
    {
        var transportClient = new StubIpcTransportClient();
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);
        var response = CreateResponse("req-success");
        transportClient.Response = response;

        var result = await client.SendAsync(
            CreateContext(),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertUnityResponse(response, result.Response);
        Assert.Equal(1, sessionConnectionProvider.CallCount);
        Assert.Equal(1, transportClient.CallCount);
        Assert.Equal("/tmp/ucli-session.sock", transportClient.Endpoints[0].Address);
        Assert.Equal("daemon-token", transportClient.LastRequest!.SessionToken);
        Assert.Equal(IpcMethodNames.OpsRead, transportClient.LastRequest.Method);
        Assert.Equal(CreateDispatchPayload().GetRawText(), transportClient.LastRequest.Payload.GetRawText());
    }

    private static void AssertUnityResponse (
        IpcResponse expected,
        UnityRequestResponse? actual)
    {
        Assert.NotNull(actual);
        Assert.False(actual!.HasFailureStatus);
        Assert.Equal(expected.Payload.GetRawText(), actual.Payload.GetRawText());
        Assert.Equal(expected.Errors.Count, actual.Errors.Count);
        for (var i = 0; i < expected.Errors.Count; i++)
        {
            Assert.Equal(expected.Errors[i].Code, actual.Errors[i].Code);
            Assert.Equal(expected.Errors[i].Message, actual.Errors[i].Message);
            Assert.Equal(expected.Errors[i].OpId, actual.Errors[i].OpId);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenIsNotAvailable_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new StubIpcTransportClient();
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            DaemonSessionConnectionResolutionResult.SessionNotAvailable());
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            CreateContext(),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        Assert.Equal(1, sessionConnectionProvider.CallCount);
        Assert.Equal(0, transportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenTransportTimesOut_ReturnsIpcTimeout ()
    {
        var transportClient = new StubIpcTransportClient
        {
            Exception = new TimeoutException("timed out"),
        };
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            CreateContext(),
            CreateDispatchRequest(),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenNonRecoverableDispatchConnectionIsRefused_ReturnsDaemonNotRunningWithoutRetry ()
    {
        var transportClient = new StubIpcTransportClient
        {
            Exception = new SocketException((int)SocketError.ConnectionRefused),
        };
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            CreateContext(),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        Assert.Equal(1, sessionConnectionProvider.CallCount);
        Assert.Equal(1, transportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchLosesResponse_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueException(new EndOfStreamException("lost response"));
        transportClient.EnqueueResponse(CreateResponse("req-recovered"));
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            CreateContext(),
            new UnityIpcDispatchRequest(IpcMethodNames.PlayEnter, CreateDispatchPayload(), isRecoverable: true),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sessionConnectionProvider.CallCount);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal("daemon-token-1", transportClient.Requests[0].SessionToken);
        Assert.Equal("daemon-token-2", transportClient.Requests[1].SessionToken);
        Assert.Equal(transportClient.Requests[0].RequestId, transportClient.Requests[1].RequestId);
        Assert.StartsWith($"{IpcMethodNames.PlayEnter}-", transportClient.Requests[0].RequestId, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchConnectionIsRefused_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        transportClient.EnqueueResponse(CreateResponse("req-recovered"));
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                CreateContext(),
                new UnityIpcDispatchRequest(IpcMethodNames.Compile, CreateDispatchPayload(), isRecoverable: true),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sessionConnectionProvider.CallCount);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal("daemon-token-1", transportClient.Requests[0].SessionToken);
        Assert.Equal("daemon-token-2", transportClient.Requests[1].SessionToken);
        Assert.Equal(transportClient.Requests[0].RequestId, transportClient.Requests[1].RequestId);
        Assert.StartsWith($"{IpcMethodNames.Compile}-", transportClient.Requests[0].RequestId, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchConnectionRefusalOutlivesEndpointAbsenceGrace_ReturnsDaemonNotRunning ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient
        {
            Exception = new SocketException((int)SocketError.ConnectionRefused),
        };
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            timeProvider: timeProvider);

        var sendTask = client.SendAsync(
                CreateContext(),
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
        Assert.True(transportClient.CallCount > 1);
        Assert.True(transportClient.CallCount < 20);
        Assert.Equal(transportClient.Requests[0].RequestId, transportClient.Requests[^1].RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableResponseAttemptTimesOutBeforeDeadline_RetriesWithSameRequestId ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueException(new TimeoutException("response wait timed out"));
        transportClient.EnqueueResponse(CreateResponse("req-recovered"));
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            timeProvider: timeProvider);
        var attemptTimeout = TimeSpan.FromMilliseconds(250);

        var sendTask = client.SendAsync(
                CreateContext(),
                new UnityIpcDispatchRequest(
                    IpcMethodNames.PlayExit,
                    CreateDispatchPayload(),
                    isRecoverable: true,
                    recoverableResponseAttemptTimeout: attemptTimeout),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);
        Assert.Equal(attemptTimeout, transportClient.Timeouts[0]);

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sessionConnectionProvider.CallCount);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal("daemon-token-1", transportClient.Requests[0].SessionToken);
        Assert.Equal("daemon-token-2", transportClient.Requests[1].SessionToken);
        Assert.Equal(transportClient.Requests[0].RequestId, transportClient.Requests[1].RequestId);
        Assert.Equal(attemptTimeout, transportClient.Timeouts[1]);
        Assert.StartsWith($"{IpcMethodNames.PlayExit}-", transportClient.Requests[0].RequestId, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenDispatchIsRecoverable_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new StubIpcTransportClient();
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendStreamingAsync(
            CreateContext(),
            new UnityIpcDispatchRequest(
                IpcMethodNames.PlayEnter,
                CreateDispatchPayload(),
                isRecoverable: true,
                responseMode: IpcResponseModes.Stream),
            TimeSpan.FromSeconds(30),
            (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(0, sessionConnectionProvider.CallCount);
        Assert.Equal(0, transportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressFrameHandlerFails_RethrowsHandlerException ()
    {
        var handlerException = new InvalidOperationException("progress frame rejected");
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueException(new IpcProgressFrameHandlerException(handlerException));
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendStreamingAsync(
                    CreateContext(),
                    new UnityIpcDispatchRequest(
                        IpcMethodNames.OpsRead,
                        CreateDispatchPayload(),
                        responseMode: IpcResponseModes.Stream),
                    TimeSpan.FromSeconds(30),
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask();
        });

        Assert.Same(handlerException, exception);
        Assert.Equal(1, transportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableSessionTokenIsTemporarilyUnavailableDuringRecovery_WaitsAndSendsRecoveredSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueResponse(CreateResponse("req-recovered-session"));
        var session = CreateRecoveringSession();
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(session)),
            new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session))),
            new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess),
            timeProvider);
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            DaemonSessionConnectionResolutionResult.SessionNotAvailable(),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = client.SendAsync(
                CreateContext(),
                new UnityIpcDispatchRequest(IpcMethodNames.PlayEnter, CreateDispatchPayload(), isRecoverable: true),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sessionConnectionProvider.CallCount);
        Assert.Single(transportClient.Requests);
        Assert.Equal("daemon-token-2", transportClient.Requests[0].SessionToken);
        Assert.StartsWith($"{IpcMethodNames.PlayEnter}-", transportClient.Requests[0].RequestId, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenSessionTokenIsTemporarilyUnavailableDuringRecovery_WaitsAndSendsRecoveredSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueResponse(CreateResponse("req-recovered-stream"));
        var session = CreateRecoveringSession();
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(session)),
            new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session))),
            new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess),
            timeProvider);
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            DaemonSessionConnectionResolutionResult.SessionNotAvailable(),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = client.SendStreamingAsync(
                CreateContext(),
                new UnityIpcDispatchRequest(
                    IpcMethodNames.TestRun,
                    CreateDispatchPayload(),
                    responseMode: IpcResponseModes.Stream),
                TimeSpan.FromSeconds(5),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sessionConnectionProvider.CallCount);
        Assert.Single(transportClient.Requests);
        Assert.Equal("daemon-token-2", transportClient.Requests[0].SessionToken);
        Assert.Equal(IpcResponseModes.Stream, transportClient.Requests[0].ResponseMode);
        Assert.StartsWith($"{IpcMethodNames.TestRun}-", transportClient.Requests[0].RequestId, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenConnectionIsRefusedDuringRecovery_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient();
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        transportClient.EnqueueResponse(CreateResponse("req-recovered-stream"));
        var session = CreateRecoveringSession();
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(session)),
            new StubDaemonLifecycleStore(DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session))),
            new StubDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess),
            timeProvider);
        var sessionConnectionProvider = new StubDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = client.SendStreamingAsync(
                CreateContext(),
                new UnityIpcDispatchRequest(
                    IpcMethodNames.TestRun,
                    CreateDispatchPayload(),
                    responseMode: IpcResponseModes.Stream),
                TimeSpan.FromSeconds(5),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sessionConnectionProvider.CallCount);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal("daemon-token-1", transportClient.Requests[0].SessionToken);
        Assert.Equal("daemon-token-2", transportClient.Requests[1].SessionToken);
        Assert.Equal(transportClient.Requests[0].RequestId, transportClient.Requests[1].RequestId);
        Assert.Equal(IpcResponseModes.Stream, transportClient.Requests[0].ResponseMode);
        Assert.Equal(IpcResponseModes.Stream, transportClient.Requests[1].ResponseMode);
        Assert.StartsWith($"{IpcMethodNames.TestRun}-", transportClient.Requests[0].RequestId, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static JsonElement CreateDispatchPayload ()
    {
        return JsonDocument.Parse("""{"sentinel":"daemon-payload"}""").RootElement.Clone();
    }

    private static UnityIpcDispatchRequest CreateDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(IpcMethodNames.OpsRead, CreateDispatchPayload());
    }

    private static IpcResponse CreateResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: EmptyPayload(),
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
            EditorMode: "gui",
            OwnerKind: "user",
            CanShutdownProcess: false,
            EndpointTransportKind: "unixDomainSocket",
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
        public int CallCount { get; private set; }

        public List<IpcEndpoint> Endpoints { get; } = new();

        public List<IpcRequest> Requests { get; } = new();

        public List<TimeSpan> Timeouts { get; } = new();

        public Exception? Exception { get; set; }

        public IpcRequest? LastRequest { get; private set; }

        public IpcResponse Response { get; set; } = CreateResponse("default");

        private Queue<Exception> QueuedExceptions { get; } = new();

        private Queue<IpcResponse> QueuedResponses { get; } = new();

        public void EnqueueException (Exception exception)
        {
            QueuedExceptions.Enqueue(exception);
        }

        public void EnqueueResponse (IpcResponse response)
        {
            QueuedResponses.Enqueue(response);
        }

        public ValueTask<IpcResponse> SendAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Endpoints.Add(endpoint);
            LastRequest = request;
            Requests.Add(request);
            Timeouts.Add(timeout);

            if (QueuedExceptions.Count != 0)
            {
                throw QueuedExceptions.Dequeue();
            }

            if (Exception != null)
            {
                throw Exception;
            }

            return ValueTask.FromResult(QueuedResponses.Count == 0 ? Response : QueuedResponses.Dequeue());
        }

        public async ValueTask<IpcResponse> SendStreamingAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            return await SendAsync(endpoint, request, timeout, cancellationToken);
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
