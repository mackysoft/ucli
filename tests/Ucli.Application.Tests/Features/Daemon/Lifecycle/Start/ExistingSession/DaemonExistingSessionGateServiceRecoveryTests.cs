namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class DaemonExistingSessionGateServiceRecoveryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingTimesOutDuringRecovery_WaitsForSameProcessAndReturnsAlreadyRunning ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-timeout-recovery");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4009,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("recovering"),
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
        var processIdentityAssessor = DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        DaemonExistingSessionGateServiceAssert.RecoveringSessionReturnedAlreadyRunningWithoutStaleCleanup(
            result,
            session,
            cleanupService);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2,
            CancellationToken.None);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenFreshReadyGuiSidecarOutlivesEndpoint_HandsOffToRebootstrapFlow ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-ready-handoff");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4017,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateRecoveringObservation(session) with
                {
                    LifecycleState = IpcEditorLifecycleStateCodec.Ready,
                    BlockingReason = null,
                    CanAcceptExecutionRequests = true,
                }),
        };
        var pingClient = new RecordingDaemonPingInfoClient(new TimeoutException("endpoint occupied"));
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        var invocation = Assert.Single(pingClient.Invocations);
        Assert.Equal(DaemonTimeouts.ProbeAttemptTimeoutCap, invocation.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringSidecarIsStale_DoesNotRetryOrHandOff ()
    {
        var now = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(now);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-stale-recovery");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4018,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateRecoveringObservation(session) with
                {
                    ObservedAtUtc = now - DaemonLifecycleObservationTimings.FreshnessWindow - TimeSpan.FromMilliseconds(1),
                }),
        };
        var pingClient = new RecordingDaemonPingInfoClient(new TimeoutException("endpoint unavailable"));
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session),
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Single(pingClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenInitialPingSucceeds_DoesNotReadLifecycleSidecar ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-detection-budget");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4015,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        var invocation = Assert.Single(pingClient.Invocations);
        Assert.True(invocation.Timeout > TimeSpan.Zero);
        Assert.True(invocation.Timeout <= TimeSpan.FromMilliseconds(500));
        Assert.Empty(lifecycleStore.ReadInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringProbeBudgetExpiresAfterNotRunningError_ReturnsNullForStartFlow ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-handoff-not-running");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4012,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            new InvalidOperationException("endpoint unavailable"),
            new TimeoutException("recovering endpoint timeout"))
        {
            OnPingAndRead = () => timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap + TimeSpan.FromMilliseconds(1)),
        };
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(
            session,
            timeProvider.GetUtcNow());
        var processIdentityAssessor = DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static exception => exception is InvalidOperationException),
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor,
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2,
            CancellationToken.None);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringRetryReportsNotRunningAndProbeBudgetExpires_ReturnsNullForStartFlow ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-retry-not-running");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4016,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("initial endpoint timeout"),
            new InvalidOperationException("endpoint unavailable during recovery"))
        {
            OnPingAndRead = () => timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap + TimeSpan.FromMilliseconds(1)),
        };
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(
            session,
            timeProvider.GetUtcNow());
        var processIdentityAssessor = DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static exception => exception is InvalidOperationException),
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor,
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2,
            CancellationToken.None);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringGuiSessionConflictsWithRequestedEditorMode_ReturnsMismatch ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-mismatch");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4013,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(new TimeoutException("recovering"));
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
        var processIdentityAssessor = DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Batchmode,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonEditorModeMismatch, error.Code);
        Assert.Empty(cleanupService.StaleSessionInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringSessionIsNotGui_ReturnsTimeoutFailure ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-batchmode");
        var session = DaemonSessionTestFactory.Create(
            processId: 4014,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("recovering")),
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Empty(cleanupService.StaleSessionInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringProbeBudgetExpires_ReturnsNullForStartFlow ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-handoff");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4010,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("initial endpoint timeout"),
            new TimeoutException("recovering endpoint timeout"))
        {
            OnPingAndRead = () => timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap + TimeSpan.FromMilliseconds(1)),
        };
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(
            session,
            timeProvider.GetUtcNow());
        var processIdentityAssessor = DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor,
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2,
            CancellationToken.None);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringSidecarHasInvalidEditorInstanceId_ReturnsTimeoutFailure ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-timeout-recovery-missing-editor-instance");
        var session = DaemonSessionTestFactory.Create(
            processId: 4011,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Failure(
                ExecutionError.InvalidArgument("Daemon lifecycle editorInstanceId is invalid.")),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("recovering")),
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));

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
}
