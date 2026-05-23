using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class DaemonExistingSessionGateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingSucceeds_ReturnsAlreadyRunning ()
    {
        var session = CreateSession(processId: 4001);
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.CompletedTask),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor());

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-running"),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, result.LifecycleSnapshot!.LifecycleState);
        Assert.True(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingReportsCompiling_ReturnsAlreadyRunningWithLifecycleSnapshot ()
    {
        var session = CreateSession(processId: 4010);
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.CompletedTask)
            {
                Response = CreatePingResponse(IpcEditorLifecycleStateCodec.Compiling, IpcEditorBlockingReasonCodec.Compile, false),
            },
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor());

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-running-compiling"),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, result.LifecycleSnapshot!.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.Compile, result.LifecycleSnapshot.BlockingReason);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRequestedEditorModeDiffersFromRunningSession_ReturnsMismatch ()
    {
        var session = CreateSession(processId: 4008);
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.CompletedTask),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor());

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-running-mismatch"),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonEditorModeMismatch, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingTimesOut_ReturnsTimeoutFailure ()
    {
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new TimeoutException("timeout"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor());

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-timeout"),
            CreateSession(processId: 4002),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingTimesOutDuringRecovery_WaitsForSameProcessAndReturnsAlreadyRunning ()
    {
        var context = CreateContext("fingerprint-existing-timeout-recovery");
        var session = CreateSession(processId: 4009, projectFingerprint: context.ProjectFingerprint) with
        {
            EditorInstanceId = "editor-instance-recovering",
        };
        var pingClient = new StubDaemonPingClient(_ =>
        {
            return ValueTask.FromException(new TimeoutException("recovering"));
        })
        {
            Handler = callCount => callCount == 1
                ? ValueTask.FromException(new TimeoutException("recovering"))
                : ValueTask.CompletedTask,
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var lifecycleStore = new StubDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
        };
        var processIdentityAssessor = new StubDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                session.ProcessStartedAtUtc,
                Error: null),
        };
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: pingClient,
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: cleanupService,
            daemonLifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(2, pingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringSidecarLacksEditorInstanceId_ReturnsTimeoutFailure ()
    {
        var context = CreateContext("fingerprint-existing-timeout-recovery-missing-editor-instance");
        var session = CreateSession(processId: 4011, projectFingerprint: context.ProjectFingerprint);
        var lifecycleStore = new StubDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
        };
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new TimeoutException("recovering"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonLifecycleStore: lifecycleStore,
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor
            {
                Assessment = new DaemonProcessIdentityAssessment(
                    DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                    session.ProcessStartedAtUtc,
                    Error: null),
            });

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenSessionIsStaleAndCleanupSucceeds_ReturnsNullForFreshLaunch ()
    {
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var session = CreateSession(processId: 4003);
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new InvalidOperationException("stale"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService,
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor());

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-stale"),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(session, cleanupService.LastStaleSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenStaleSessionCleanupRuns_UsesRemainingTimeoutBudget ()
    {
        var timeProvider = new ManualTimeProvider();
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(timeout =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(120));
                return ValueTask.FromException(new InvalidOperationException("stale"));
            }),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService,
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor(),
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-stale-remaining-timeout"),
            CreateSession(processId: 4006),
            TimeSpan.FromMilliseconds(300),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.True(cleanupService.LastStaleCleanupTimeout < TimeSpan.FromMilliseconds(260));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenStaleSessionCleanupCannotStartWithinDeadline_ReturnsTimeoutWithoutCleanup ()
    {
        var timeProvider = new ManualTimeProvider();
        var cleanupService = new StubDaemonSessionCleanupService();
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(timeout =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(80));
                return ValueTask.FromException(new InvalidOperationException("stale"));
            }),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService,
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor(),
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-stale-timeout-before-cleanup"),
            CreateSession(processId: 4007),
            TimeSpan.FromMilliseconds(20),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenSessionIsStaleAndCleanupFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("cleanup failed");
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new InvalidOperationException("stale"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService,
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor());

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-stale-failed"),
            CreateSession(processId: 4004),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingThrowsUnexpectedError_ReturnsInternalFailure ()
    {
        var service = new DaemonExistingSessionGateService(
            daemonPingInfoClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new InvalidOperationException("unexpected"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonLifecycleStore: new StubDaemonLifecycleStore(),
            processIdentityAssessor: new StubDaemonProcessIdentityAssessor());

        var result = await service.TryHandleExistingSessionAsync(
            CreateContext("fingerprint-existing-unexpected"),
            CreateSession(processId: 4005),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        int? processId,
        string projectFingerprint = "fingerprint")
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (DaemonSession session)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: session.EditorMode,
            LifecycleState: IpcEditorLifecycleStateCodec.Recovering,
            BlockingReason: IpcEditorBlockingReasonCodec.Recovery,
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

    private static IpcPingResponse CreatePingResponse (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests)
    {
        return new IpcPingResponse(
            ServerVersion: "1.0.0",
            EditorMode: DaemonEditorModeValues.Batchmode,
            UnityVersion: "2022.3.0f1",
            ProjectFingerprint: "fingerprint",
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            CanAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    private sealed class StubDaemonPingClient : IDaemonPingInfoClient
    {
        private readonly Func<TimeSpan, ValueTask> handler;

        public StubDaemonPingClient (Func<TimeSpan, ValueTask> handler)
        {
            this.handler = handler;
        }

        public Func<int, ValueTask>? Handler { get; set; }

        public IpcPingResponse Response { get; set; } = CreatePingResponse(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true);

        public int CallCount { get; private set; }

        public async ValueTask<IpcPingResponse> PingAndReadAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            bool validateProjectFingerprint = true,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Handler is not null)
            {
                await Handler(CallCount).ConfigureAwait(false);
                return Response;
            }

            await handler(timeout).ConfigureAwait(false);
            return Response;
        }
    }

    private sealed class StubDaemonReachabilityClassifier : IDaemonReachabilityClassifier
    {
        private readonly Func<Exception, bool> predicate;

        public StubDaemonReachabilityClassifier (Func<Exception, bool> predicate)
        {
            this.predicate = predicate;
        }

        public bool IsNotRunning (Exception exception)
        {
            return predicate(exception);
        }
    }

    private sealed class StubDaemonLifecycleStore : IDaemonLifecycleStore
    {
        public DaemonLifecycleObservationReadResult ReadResult { get; set; } = DaemonLifecycleObservationReadResult.Success(null);

        public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        public DaemonProcessIdentityAssessment Assessment { get; set; } = new(
            DaemonProcessIdentityAssessmentStatus.NotRunning,
            ObservedStartTimeUtc: null,
            Error: null);

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset? expectedProcessStartedAtUtc)
        {
            return Assessment;
        }
    }

    private sealed class StubDaemonSessionCleanupService : IDaemonSessionCleanupService
    {
        public DaemonSessionStoreOperationResult CleanupInvalidSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public DaemonSessionStoreOperationResult CleanupStaleSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CleanupStaleSessionArtifactsCallCount { get; private set; }

        public DaemonSession? LastStaleSession { get; private set; }

        public TimeSpan LastStaleCleanupTimeout { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifactsAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSessionReadResult readResult,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CleanupInvalidSessionArtifactsResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CleanupStaleSessionArtifactsCallCount++;
            LastStaleSession = session;
            LastStaleCleanupTimeout = timeout;
            return ValueTask.FromResult(CleanupStaleSessionArtifactsResult);
        }
    }
}
