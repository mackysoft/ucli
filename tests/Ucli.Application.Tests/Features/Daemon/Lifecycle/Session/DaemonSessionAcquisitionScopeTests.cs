using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Daemon.Lifecycle.Session;

public sealed class DaemonSessionAcquisitionScopeTests
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(
        DaemonTimeouts.StartupProbeRetryDelayMilliseconds);

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAfterPreWriteFailure_WhenNewGenerationPublishesDuringRetryDelay_ReturnsNewGeneration ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-endpoint-rollover"));
        var failedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "failed-token",
            "11111111-1111-1111-1111-111111111111");
        var replacementSession = CreateSession(
            unityProject.ProjectFingerprint,
            "replacement-token",
            "22222222-2222-2222-2222-222222222222");
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(failedSession),
            DaemonSessionReadResultTestFactory.Found(replacementSession));
        var timeProvider = new ManualTimeProvider();
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(sessionStore)
            .CreateScope(ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider));

        var resolutionTask = scope.ResolveAfterPreWriteFailureAsync(
                unityProject,
                failedSession,
                CancellationToken.None)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(RetryDelay);

        Assert.False(resolutionTask.IsCompleted);
        timeProvider.Advance(RetryDelay);
        var result = await resolutionTask;

        Assert.Equal(DaemonSessionAcquisitionKind.Success, result.Kind);
        Assert.Equal(replacementSession, result.Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveReplacement_WhenStoreRollsBackAcrossRejectedGenerations_ReturnsOnlyUnrejectedGeneration ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-rejected-generation-rollback"));
        var firstRejectedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "first-rejected-token",
            "11111111-1111-1111-1111-111111111111");
        var secondRejectedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "second-rejected-token",
            "22222222-2222-2222-2222-222222222222");
        var acceptedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "accepted-token",
            "33333333-3333-3333-3333-333333333333");
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(secondRejectedSession),
            DaemonSessionReadResultTestFactory.Found(firstRejectedSession),
            DaemonSessionReadResultTestFactory.Found(firstRejectedSession),
            DaemonSessionReadResultTestFactory.Found(acceptedSession));
        var timeProvider = new ManualTimeProvider();
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(sessionStore)
            .CreateScope(ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider));

        var firstReplacement = await scope.ResolveReplacementAsync(
            unityProject,
            firstRejectedSession,
            CancellationToken.None);
        Assert.Equal(secondRejectedSession, firstReplacement.Session);

        var secondReplacementTask = scope.ResolveReplacementAsync(
                unityProject,
                secondRejectedSession,
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            secondReplacementTask,
            TimeSpan.FromSeconds(1),
            RetryDelay);
        var secondReplacement = await secondReplacementTask;

        Assert.Equal(DaemonSessionAcquisitionKind.Success, secondReplacement.Kind);
        Assert.Equal(acceptedSession, secondReplacement.Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveReplacement_WhenSessionReadFails_PreservesOriginalFailureMetadata ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-read-failure"));
        var rejectedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "rejected-token",
            "11111111-1111-1111-1111-111111111111");
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("failed-session-artifact"u8);
        var readFailure = DaemonSessionReadResult.Failure(
            ExecutionError.InternalError("Synthetic session read failure."),
            DaemonSessionReadFailureKind.IoFailure,
            artifactIdentity);
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(new RecordingDaemonSessionStore(readFailure))
            .CreateScope(ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System));

        var result = await scope.ResolveReplacementAsync(
            unityProject,
            rejectedSession,
            CancellationToken.None);

        Assert.Equal(DaemonSessionAcquisitionKind.SessionReadFailure, result.Kind);
        Assert.Same(readFailure, result.ReadFailure);
        Assert.Equal(DaemonSessionReadFailureKind.IoFailure, result.ReadFailure!.FailureKind);
        Assert.Same(artifactIdentity, result.ReadFailure.ArtifactIdentity);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveReplacement_WhenSuccessorIsAlreadyPublished_ReadsSessionStoreExactlyOnce ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-successor-single-read"));
        var rejectedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "rejected-token",
            "11111111-1111-1111-1111-111111111111");
        var successorSession = CreateSession(
            unityProject.ProjectFingerprint,
            "successor-token",
            "22222222-2222-2222-2222-222222222222");
        var sessionStore = new RecordingDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(successorSession));
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var coordinator = new DaemonSessionAcquisitionCoordinator(
            sessionStore,
            new DaemonSessionRecoveryWaiter(
                lifecycleStore,
                new RecordingDaemonProcessIdentityAssessor()));
        var scope = coordinator.CreateScope(
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System));

        var result = await scope.ResolveReplacementAsync(
            unityProject,
            rejectedSession,
            CancellationToken.None);

        Assert.Equal(DaemonSessionAcquisitionKind.Success, result.Kind);
        Assert.Equal(successorSession, result.Session);
        Assert.Single(sessionStore.ReadInvocations);
        Assert.Empty(lifecycleStore.ReadInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAfterPreWriteFailure_WhenSessionArtifactIsMissingDuringRecovery_WaitsForPublishedSession ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-missing-during-recovery"));
        var failedSession = CreateGuiSession(
            unityProject.ProjectFingerprint,
            "failed-token",
            "11111111-1111-1111-1111-111111111111");
        var recoveredSession = CreateGuiSession(
            unityProject.ProjectFingerprint,
            "recovered-token",
            "22222222-2222-2222-2222-222222222222");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count < 3
                ? DaemonSessionReadResult.Missing()
                : DaemonSessionReadResultTestFactory.Found(recoveredSession),
        };
        var timeProvider = new ManualTimeProvider();
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                CreateRecoveringObservation(failedSession, timeProvider.GetUtcNow())),
        };
        var coordinator = new DaemonSessionAcquisitionCoordinator(
            sessionStore,
            new DaemonSessionRecoveryWaiter(
                lifecycleStore,
                new RecordingDaemonProcessIdentityAssessor(
                    DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess)));
        var scope = coordinator.CreateScope(
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider));

        var resolutionTask = scope.ResolveAfterPreWriteFailureAsync(
                unityProject,
                failedSession,
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resolutionTask,
            TimeSpan.FromSeconds(1),
            RetryDelay);
        var result = await resolutionTask;

        Assert.Equal(DaemonSessionAcquisitionKind.Success, result.Kind);
        Assert.Equal(recoveredSession, result.Session);
        Assert.Equal(3, sessionStore.ReadInvocations.Count);
        Assert.Equal(2, lifecycleStore.ReadInvocations.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAfterPreWriteFailure_WhenSessionReadIgnoresCancellation_ReturnsAtEndpointAvailabilityCap ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-endpoint-read-cap"));
        var failedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "failed-token",
            "11111111-1111-1111-1111-111111111111");
        var sessionStore = new NonCooperativeBlockingDaemonSessionStore(
            blockOnCall: 1,
            DaemonSessionReadResult.Missing());
        var timeProvider = new ManualTimeProvider();
        var requestDeadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(sessionStore)
            .CreateScope(requestDeadline);

        var resolutionTask = scope.ResolveAfterPreWriteFailureAsync(
                unityProject,
                failedSession,
                CancellationToken.None)
            .AsTask();
        await sessionStore.Blocked.WaitAsync(TimeSpan.FromSeconds(1));
        await timeProvider
            .WaitForTimerDueWithinAsync(DaemonTimeouts.ProbeAttemptTimeoutCap)
            .WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap);
            var result = await resolutionTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired, result.Kind);
            Assert.False(requestDeadline.IsExpired);
            Assert.Equal(
                DateTimeOffset.UnixEpoch + DaemonTimeouts.ProbeAttemptTimeoutCap,
                timeProvider.GetUtcNow());
        }
        finally
        {
            sessionStore.Release();
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAfterPreWriteFailure_WhenSameGenerationRemainsPublished_WaitsBeforeReturningIt ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-same-generation-delay"));
        var failedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "failed-token",
            "11111111-1111-1111-1111-111111111111");
        var sessionStore = new RecordingDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(failedSession));
        var timeProvider = new ManualTimeProvider();
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(sessionStore)
            .CreateScope(ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider));

        var resolutionTask = scope.ResolveAfterPreWriteFailureAsync(
                unityProject,
                failedSession,
                CancellationToken.None)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(RetryDelay);

        Assert.False(resolutionTask.IsCompleted);
        Assert.Single(sessionStore.ReadInvocations);
        timeProvider.Advance(RetryDelay);
        var result = await resolutionTask;

        Assert.Equal(DaemonSessionAcquisitionKind.Success, result.Kind);
        Assert.Equal(failedSession, result.Session);
        Assert.Equal(2, sessionStore.ReadInvocations.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAfterResponseInterruption_WhenSameGenerationRemainsPublished_ReturnsItAfterRetryDelay ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-response-interruption-delay"));
        var interruptedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "interrupted-token",
            "11111111-1111-1111-1111-111111111111");
        var sessionStore = new RecordingDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(interruptedSession));
        var timeProvider = new ManualTimeProvider();
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(sessionStore)
            .CreateScope(ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider));

        var resolutionTask = scope.ResolveAfterStatelessResponseInterruptionAsync(
                unityProject,
                interruptedSession,
                CancellationToken.None)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(RetryDelay);

        Assert.False(resolutionTask.IsCompleted);
        timeProvider.Advance(RetryDelay);
        var result = await resolutionTask;

        Assert.Equal(DaemonSessionAcquisitionKind.Success, result.Kind);
        Assert.Equal(interruptedSession, result.Session);
        Assert.Equal(2, sessionStore.ReadInvocations.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAfterResponseInterruption_WhenStoreRollsBackToRejectedGeneration_DoesNotReturnRejectedGeneration ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-response-rollback"));
        var rejectedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "rejected-token",
            "11111111-1111-1111-1111-111111111111");
        var interruptedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "interrupted-token",
            "22222222-2222-2222-2222-222222222222");
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(interruptedSession),
            DaemonSessionReadResultTestFactory.Found(rejectedSession),
            DaemonSessionReadResultTestFactory.Found(interruptedSession));
        var timeProvider = new ManualTimeProvider();
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(sessionStore)
            .CreateScope(ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider));
        var firstReplacement = await scope.ResolveReplacementAsync(
            unityProject,
            rejectedSession,
            CancellationToken.None);
        Assert.Equal(interruptedSession, firstReplacement.Session);

        var responseRecoveryTask = scope.ResolveAfterStatelessResponseInterruptionAsync(
                unityProject,
                interruptedSession,
                CancellationToken.None)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(RetryDelay);
        timeProvider.Advance(RetryDelay);
        var result = await responseRecoveryTask;

        Assert.Equal(DaemonSessionAcquisitionKind.Success, result.Kind);
        Assert.Equal(interruptedSession, result.Session);
        Assert.NotEqual(rejectedSession, result.Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveReplacement_WhenRecoveryLeaseOutlivesPublicationWindow_ReturnsSessionPublishedDuringLease ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-long-recovery"));
        var rejectedSession = CreateGuiSession(
            unityProject.ProjectFingerprint,
            "rejected-token",
            "11111111-1111-1111-1111-111111111111");
        var successorSession = CreateGuiSession(
            unityProject.ProjectFingerprint,
            "successor-token",
            "22222222-2222-2222-2222-222222222222");
        var timeProvider = new ManualTimeProvider();
        var leaseDuration = DaemonTimeouts.SessionPublicationRetryTimeout + TimeSpan.FromMilliseconds(500);
        var leaseExpiresAtUtc = timeProvider.GetUtcNow() + leaseDuration;
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = _ => timeProvider.GetUtcNow() < leaseExpiresAtUtc
                ? DaemonSessionReadResultTestFactory.Found(rejectedSession)
                : DaemonSessionReadResultTestFactory.Found(successorSession),
        };
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                CreateRecoveringObservation(
                    rejectedSession,
                    timeProvider.GetUtcNow(),
                    leaseExpiresAtUtc)),
        };
        var coordinator = new DaemonSessionAcquisitionCoordinator(
            sessionStore,
            new DaemonSessionRecoveryWaiter(
                lifecycleStore,
                new RecordingDaemonProcessIdentityAssessor(
                    DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess)));
        var scope = coordinator.CreateScope(
            ExecutionDeadline.Start(TimeSpan.FromSeconds(10), timeProvider));

        var resolutionTask = scope.ResolveReplacementAsync(
                unityProject,
                rejectedSession,
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resolutionTask,
            TimeSpan.FromSeconds(4),
            RetryDelay);
        var result = await resolutionTask;

        Assert.Equal(DaemonSessionAcquisitionKind.Success, result.Kind);
        Assert.Equal(successorSession, result.Session);
        Assert.Equal(leaseExpiresAtUtc, timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveReplacement_WhenRecoveryLeaseExpiresWithoutSuccessor_StopsAfterOneFreshPublicationWindow ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-recovery-lease-expired"));
        var rejectedSession = CreateGuiSession(
            unityProject.ProjectFingerprint,
            "rejected-token",
            "11111111-1111-1111-1111-111111111111");
        var timeProvider = new ManualTimeProvider();
        var leaseDuration = DaemonTimeouts.SessionPublicationRetryTimeout + TimeSpan.FromMilliseconds(500);
        var leaseExpiresAtUtc = timeProvider.GetUtcNow() + leaseDuration;
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                CreateRecoveringObservation(
                    rejectedSession,
                    timeProvider.GetUtcNow(),
                    leaseExpiresAtUtc)),
        };
        var coordinator = new DaemonSessionAcquisitionCoordinator(
            new RecordingDaemonSessionStore(
                DaemonSessionReadResultTestFactory.Found(rejectedSession)),
            new DaemonSessionRecoveryWaiter(
                lifecycleStore,
                new RecordingDaemonProcessIdentityAssessor(
                    DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess)));
        var requestDeadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(10), timeProvider);
        var scope = coordinator.CreateScope(requestDeadline);

        var resolutionTask = scope.ResolveReplacementAsync(
                unityProject,
                rejectedSession,
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resolutionTask,
            TimeSpan.FromSeconds(6),
            RetryDelay);
        var result = await resolutionTask;

        Assert.Equal(DaemonSessionAcquisitionKind.PublicationWindowExpired, result.Kind);
        Assert.False(requestDeadline.IsExpired);
        Assert.Equal(
            leaseExpiresAtUtc + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveReplacement_WhenRequestDeadlinePrecedesPublicationWindow_ReturnsRequestDeadlineExpired ()
    {
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-session-request-deadline"));
        var rejectedSession = CreateSession(
            unityProject.ProjectFingerprint,
            "rejected-token",
            "11111111-1111-1111-1111-111111111111");
        var timeProvider = new ManualTimeProvider();
        var requestTimeout = TimeSpan.FromMilliseconds(150);
        var scope = DaemonSessionAcquisitionCoordinatorTestFactory
            .Create(new RecordingDaemonSessionStore(
                DaemonSessionReadResultTestFactory.Found(rejectedSession)))
            .CreateScope(ExecutionDeadline.Start(requestTimeout, timeProvider));

        var resolutionTask = scope.ResolveReplacementAsync(
                unityProject,
                rejectedSession,
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(RetryDelay),
            "daemon session acquisition publication retry timer",
            TimeSpan.FromSeconds(5));
        timeProvider.Advance(requestTimeout);
        var result = await TestAwaiter.WaitAsync(
            resolutionTask,
            "daemon session acquisition request deadline result",
            TimeSpan.FromSeconds(5));

        Assert.Equal(DaemonSessionAcquisitionKind.RequestDeadlineExpired, result.Kind);
        Assert.Equal(DateTimeOffset.UnixEpoch + requestTimeout, timeProvider.GetUtcNow());
    }

    private static DaemonSession CreateSession (
        ProjectFingerprint projectFingerprint,
        string sessionToken,
        string sessionGenerationId)
    {
        return DaemonSessionTestFactory.Create(
            projectFingerprint: projectFingerprint,
            sessionToken: sessionToken,
            sessionGenerationId: Guid.Parse(sessionGenerationId));
    }

    private static DaemonSession CreateGuiSession (
        ProjectFingerprint projectFingerprint,
        string sessionToken,
        string sessionGenerationId)
    {
        return DaemonSessionTestFactory.Create(
            projectFingerprint: projectFingerprint,
            sessionToken: sessionToken,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId,
            sessionGenerationId: Guid.Parse(sessionGenerationId));
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (
        DaemonSession session,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? recoveryLeaseExpiresAtUtc = null)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: recoveryLeaseExpiresAtUtc.HasValue
                    ? IpcEditorLifecycleState.Recovering
                    : IpcEditorLifecycleState.DomainReloading,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: observedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: session.EditorInstanceId!.Value,
            recoveryLease: recoveryLeaseExpiresAtUtc.HasValue
                ? new DaemonLifecycleRecoveryLease(
                    session.SessionGenerationId,
                    recoveryLeaseExpiresAtUtc.Value)
                : null);
    }
}
