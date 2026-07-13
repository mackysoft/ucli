namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;

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
            editorInstanceId: "editor-instance-recovering");
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
    public async Task TryHandleExistingSession_WhenRecoveringDetectionConsumesBudget_UsesRemainingTimeoutForInitialPing ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-detection-budget");
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4015,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: "editor-instance-recovery-detection-budget");
        var pingClient = new RecordingDaemonPingInfoClient(
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
        lifecycleStore.OnRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(400));
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session),
            timeProvider: timeProvider);

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
        Assert.True(invocation.Timeout <= TimeSpan.FromMilliseconds(100));
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
            editorInstanceId: "editor-instance-recovery-handoff-not-running");
        var pingClient = new RecordingDaemonPingInfoClient(
            new InvalidOperationException("endpoint unavailable"),
            new TimeoutException("recovering endpoint timeout"))
        {
            OnPingAndRead = () => timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap + TimeSpan.FromMilliseconds(1)),
        };
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
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
            editorInstanceId: "editor-instance-recovery-retry-not-running");
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("initial endpoint timeout"),
            new InvalidOperationException("endpoint unavailable during recovery"))
        {
            OnPingAndRead = () => timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap + TimeSpan.FromMilliseconds(1)),
        };
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
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
            editorInstanceId: "editor-instance-recovery-mismatch");
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
        var session = DaemonSessionTestFactory.Create(processId: 4014, projectFingerprint: context.ProjectFingerprint) with
        {
            EditorInstanceId = "editor-instance-recovery-batchmode",
        };
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
            editorInstanceId: "editor-instance-recovery-handoff");
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("initial endpoint timeout"),
            new TimeoutException("recovering endpoint timeout"))
        {
            OnPingAndRead = () => timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap + TimeSpan.FromMilliseconds(1)),
        };
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
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
    public async Task TryHandleExistingSession_WhenRecoveringSidecarLacksEditorInstanceId_ReturnsTimeoutFailure ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-timeout-recovery-missing-editor-instance");
        var session = DaemonSessionTestFactory.Create(
            processId: 4011,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false);
        var lifecycleStore = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session);
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
