namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class DaemonExistingSessionGateServiceRecoveryTests
{
    private static readonly IDaemonReachabilityClassifier RecoverableProbeFailureClassifier =
        new DelegatingDaemonReachabilityClassifier(
            isNotRunning: static _ => false,
            isSessionTokenInvalid: static exception => exception is SessionTokenInvalidTestException,
            isRetryableBeforeRequestWrite: static _ => false,
            isRequestTimeout: static exception => exception is TimeoutException,
            isRecoverableResponseInterruption: static exception => exception is ResponseInterruptedTestException);

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingTimesOutDuringRecovery_WaitsForSameProcessAndReturnsAlreadyRunning ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-timeout-recovery"));
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
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
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
            expectedCount: 2);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenSessionTokenRotates_PingsValidatedSuccessorWithSameRequestId ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-existing-token-rotation"));
        var editorInstanceId = Guid.NewGuid();
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4020,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: editorInstanceId);
        var replacementSession = DaemonSessionTestFactory.Create(
            processId: session.ProcessId,
            processStartedAtUtc: session.ProcessStartedAtUtc,
            sessionToken: "replacement-token",
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: session.Endpoint.TransportKind,
            endpointAddress: session.Endpoint.Address,
            ownerProcessId: session.OwnerProcessId,
            editorInstanceId: editorInstanceId,
            sessionGenerationId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            new SessionTokenInvalidTestException(),
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var sessionStore = new RecordingDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(replacementSession));
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            reachabilityClassifier: RecoverableProbeFailureClassifier,
            lifecycleStore: DaemonExistingSessionGateServiceTestSupport.CreateRecoveringLifecycleStore(session),
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session),
            daemonSessionStore: sessionStore);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(replacementSession, result.Session);
        Assert.Collection(
            pingClient.Invocations,
            first => Assert.Equal(session.SessionToken.GetEncodedValue(), first.SessionToken),
            second => Assert.Equal(replacementSession.SessionToken.GetEncodedValue(), second.SessionToken));
        Assert.Single(sessionStore.ReadInvocations);
        Assert.Single(pingClient.Invocations.Select(static invocation => invocation.RequestId).Distinct());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenResponseIsInterrupted_PingsValidatedSuccessorWithSameRequestId ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-existing-response-recovery"));
        var editorInstanceId = Guid.NewGuid();
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4021,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: editorInstanceId);
        var successorSession = DaemonSessionTestFactory.Create(
            processId: session.ProcessId,
            processStartedAtUtc: session.ProcessStartedAtUtc,
            sessionToken: "successor-token",
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: session.Endpoint.TransportKind,
            endpointAddress: session.Endpoint.Address,
            ownerProcessId: session.OwnerProcessId,
            editorInstanceId: editorInstanceId,
            sessionGenerationId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            new ResponseInterruptedTestException(),
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            reachabilityClassifier: RecoverableProbeFailureClassifier,
            daemonSessionStore: new RecordingDaemonSessionStore(
                DaemonSessionReadResultTestFactory.Found(successorSession)));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(successorSession, result.Session);
        Assert.Single(pingClient.Invocations.Select(static invocation => invocation.RequestId).Distinct());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenTokenRecoveryFindsDifferentProcess_DoesNotAcceptSuccessor ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-existing-successor-mismatch"));
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4022,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var mismatchedSuccessor = DaemonSessionTestFactory.Create(
            processId: 9999,
            sessionToken: "mismatched-successor-token",
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: session.EditorInstanceId,
            sessionGenerationId: Guid.NewGuid());
        var pingClient = new RecordingDaemonPingInfoClient(
            new SessionTokenInvalidTestException(),
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            reachabilityClassifier: RecoverableProbeFailureClassifier,
            lifecycleStore: lifecycleStore,
            daemonSessionStore: new RecordingDaemonSessionStore(
                DaemonSessionReadResultTestFactory.Found(mismatchedSuccessor)));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(2, pingClient.Invocations.Count);
        Assert.Empty(lifecycleStore.ReadInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenFreshReadyGuiSidecarOutlivesEndpoint_HandsOffToRebootstrapFlow ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-ready-handoff"));
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4017,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateLifecycleObservation(
                    session,
                    IpcEditorLifecycleState.Ready)),
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
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), new ManualTimeProvider()),
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
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-stale-recovery"));
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4018,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateLifecycleObservation(
                    session,
                    IpcEditorLifecycleState.Recovering,
                    observedAtUtc: now - DaemonLifecycleObservationTimings.FreshnessWindow - TimeSpan.FromMilliseconds(1))),
        };
        var pingClient = new RecordingDaemonPingInfoClient(new TimeoutException("endpoint unavailable"));
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
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
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-recovery-detection-budget"));
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
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
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
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-recovery-handoff-not-running"));
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
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringRetryReportsNotRunningAndProbeBudgetExpires_ReturnsNullForStartFlow ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-recovery-retry-not-running"));
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
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringGuiSessionConflictsWithRequestedEditorMode_ReturnsMismatch ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-recovery-mismatch"));
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
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), new ManualTimeProvider()),
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
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-recovery-batchmode"));
        var session = DaemonSessionTestFactory.Create(
            processId: 4014,
            projectFingerprint: context.ProjectFingerprint);
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateLifecycleObservation(
                    session,
                    IpcEditorLifecycleState.Recovering,
                    editorInstanceId: Guid.NewGuid())),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("recovering")),
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), new ManualTimeProvider()),
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
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-recovery-handoff"));
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
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2);
        Assert.All(pingClient.Invocations, invocation =>
            Assert.True(invocation.Timeout <= DaemonTimeouts.ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringSidecarHasDifferentEditorInstanceId_ReturnsTimeoutFailure ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-timeout-recovery-missing-editor-instance"));
        var session = DaemonSessionTestFactory.Create(
            processId: 4011,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateLifecycleObservation(
                    session,
                    IpcEditorLifecycleState.Recovering,
                    editorInstanceId: Guid.Parse("22222222-2222-2222-2222-222222222222"))),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("recovering")),
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    private sealed class SessionTokenInvalidTestException : Exception
    {
    }

    private sealed class ResponseInterruptedTestException : IOException
    {
    }
}
