using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAndReadinessSucceed_ReturnsStarted ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-launch-success"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(processStartedAtUtc);
        var startedSession = DaemonSessionTestFactory.Create(
            processId: 999,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: processStartedAtUtc);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(startedSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(999, processStartedAtUtc),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Ready(
                UnityEditorObservationTestFactory.Create(UnityEditorLifecycleState.Ready)),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            UnityEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(startedSession, result.Session);
        DaemonLaunchInvocationAssert.BatchmodeLaunchCompletedWithoutCompensationOrDiagnosis(
            launchSessionService,
            launcher,
            compensationService,
            diagnosisStore,
            context,
            initialSession,
            processId: 999,
            processStartedAtUtc: processStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeLaunchAndReadinessSucceed_EmitsStartupProgress ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-batchmode-progress"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(processStartedAtUtc);
        var startedSession = DaemonSessionTestFactory.Create(
            processId: 999,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: processStartedAtUtc);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(startedSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(999, processStartedAtUtc),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Ready(
                UnityEditorObservationTestFactory.Create(UnityEditorLifecycleState.Ready)),
        };
        var progressObserver = new CollectingDaemonStartProgressObserver();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            new RecordingDaemonLaunchCompensationService(),
            timeProvider,
            new RecordingDaemonDiagnosisStore());

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            UnityEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.SessionRegistered,
            DaemonStartProgressEvent.Launching,
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.EndpointRegistered,
            DaemonStartProgressEvent.LifecycleObserved);
        var waitingObservation = progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(2);
        Assert.Equal(UnityEditorMode.Batchmode, waitingObservation.EditorMode);
        Assert.Equal(999, waitingObservation.ProcessId);
        var lifecycleObservation = progressObserver.PayloadAt<UnityEditorObservation>(^1);
        Assert.Equal(UnityEditorLifecycleState.Ready, lifecycleObservation.State.LifecycleState);
        Assert.True(UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(lifecycleObservation.State.LifecycleState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeEndpointReadyProgressFails_RunsCompensationAndRethrows ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-batchmode-progress-fail"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 11, 0, 0, 4, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(processStartedAtUtc);
        const int processId = 7644;
        var startedSession = DaemonSessionTestFactory.Create(
            processId: processId,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: processStartedAtUtc);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(startedSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(processId, processStartedAtUtc),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Ready(
                UnityEditorObservationTestFactory.Create(projectFingerprint: context.ProjectFingerprint)),
        };
        var progressFailure = new InvalidOperationException("batchmode endpoint-ready progress failed");
        var progressObserver = new ConfigurableDaemonStartProgressObserver
        {
            Handler = (progressEvent, _) => progressEvent == DaemonStartProgressEvent.EndpointRegistered
                ? ValueTask.FromException(progressFailure)
                : ValueTask.CompletedTask,
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            new RecordingDaemonDiagnosisStore());

        var actualFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LaunchAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
                    UnityEditorMode.Batchmode,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    progressObserver,
                    cancellationToken: CancellationToken.None)
                .AsTask());

        Assert.Same(progressFailure, actualFailure);
        var compensationInvocation = DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId,
            processStartedAtUtc);
        Assert.Equal(startedSession, compensationInvocation.ExpectedSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeSessionRegisteredProgressFails_CleansInitializedSessionAndRethrows ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-batchmode-session-progress-fail"));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher();
        var progressFailure = new InvalidOperationException("batchmode session progress failed");
        var progressObserver = new ConfigurableDaemonStartProgressObserver
        {
            Handler = (progressEvent, _) => progressEvent == DaemonStartProgressEvent.SessionRegistered
                ? ValueTask.FromException(progressFailure)
                : ValueTask.CompletedTask,
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var service = CreateService(
            launchSessionService,
            launcher,
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            timeProvider,
            new RecordingDaemonDiagnosisStore());

        var actualFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LaunchAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
                    UnityEditorMode.Batchmode,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    progressObserver,
                    cancellationToken: CancellationToken.None)
                .AsTask());

        Assert.Same(progressFailure, actualFailure);
        var compensationInvocation = DaemonLaunchInvocationAssert.LaunchCompensationAttemptedWithoutProcessTarget(
            compensationService,
            context);
        Assert.Equal(initialSession, compensationInvocation.ExpectedSession);
        Assert.Empty(launcher.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionInitializationFails_ReturnsFailureWithoutLaunch ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-launch-init-fail"));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var expectedError = ExecutionError.InternalError("session init failed");
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Failure(expectedError),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher();
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            UnityEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        DaemonLaunchInvocationAssert.BatchmodeLaunchStoppedAfterSessionInitializationFailure(
            launchSessionService,
            launcher,
            compensationService,
            diagnosisStore,
            context);
        Assert.NotNull(result.Startup);
        DaemonLaunchAttemptStoreAssert.LaunchAttemptRecordedAndPrunedFor(
            launchAttemptStore,
            context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Failed,
            DaemonStartupProcessAction.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionUpdateFails_RunsCompensationAndReturnsWriteFailure ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-update-fail"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var writeError = ExecutionError.InternalError("write failed");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(processStartedAtUtc);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Failure(writeError),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(2222, processStartedAtUtc),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            UnityEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(writeError, result.Error);
        DaemonLaunchInvocationAssert.ProcessIdUpdatedFor(
            launchSessionService,
            context,
            initialSession,
            processId: 2222,
            processStartedAtUtc: processStartedAtUtc);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 2222,
            processStartedAtUtc: processStartedAtUtc);
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(processStartedAtUtc, diagnosis.ProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LaunchAttemptRecordedAndPrunedFor(
            launchAttemptStore,
            context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Failed,
            DaemonStartupProcessAction.Terminated);
        Assert.Equal(DaemonStartupStatus.Failed, launchAttempt.StartupStatus);
        Assert.Equal(DaemonStartupProcessAction.Terminated, launchAttempt.ProcessAction);
        Assert.Equal(processStartedAtUtc, launchAttempt.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisPersistenceIgnoresCancellation_ReturnsBoundedFailureAndRetainsOwnership ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-update-blocked-diagnosis"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 10, 0, 0, 1, TimeSpan.Zero);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Failure(
                ExecutionError.InternalError("session update failed")),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(2323, processStartedAtUtc),
        };
        var compensationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            OnCleanup = () => compensationStarted.TrySetResult(),
        };
        var diagnosisWriteStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisCancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisCompletion = new TaskCompletionSource<DaemonDiagnosisStoreOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            WriteAsyncHandler = (_, _, _, cancellationToken) =>
            {
                _ = cancellationToken.Register(() =>
                {
                    diagnosisCancellationObserved.TrySetResult();
                });
                diagnosisWriteStarted.TrySetResult();
                return new ValueTask<DaemonDiagnosisStoreOperationResult>(diagnosisCompletion.Task);
            },
        };
        var timeProvider = new FakeTimeProvider(processStartedAtUtc);
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var service = CreateService(
            launchSessionService,
            launcher,
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            timeProvider,
            diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            compensationOperationOwner: compensationOperationOwner);

        var launchTask = service.LaunchAsync(
                context,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
                UnityEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await compensationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await diagnosisWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            timeProvider.AutoAdvanceAmount = DaemonTimeouts.SupplementalPersistenceTimeout;
            timeProvider.Advance(DaemonTimeouts.SupplementalPersistenceTimeout);

            await diagnosisCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var result = await launchTask.WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.AutoAdvanceAmount = TimeSpan.Zero;

            Assert.Equal(DaemonStartStatus.Failed, result.Status);
            Assert.False(diagnosisCompletion.Task.IsCompleted);
            DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
                compensationService,
                context,
                processId: 2323,
                processStartedAtUtc: processStartedAtUtc);

            var followingPersistenceStarted = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var followingPersistenceTask = compensationOperationOwner.ExecuteAsync(
                    context,
                    DaemonOperationLane.SupplementalPersistence,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out before the following persistence operation could begin.",
                    "Timed out while running the following persistence operation.",
                    (_, _) =>
                    {
                        followingPersistenceStarted.TrySetResult();
                        return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
                    })
                .AsTask();
            Assert.False(followingPersistenceStarted.Task.IsCompleted);

            diagnosisCompletion.TrySetResult(DaemonDiagnosisStoreOperationResult.Success());
            await followingPersistenceStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var followingPersistenceResult = await followingPersistenceTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(followingPersistenceResult.IsSuccess);
        }
        finally
        {
            timeProvider.AutoAdvanceAmount = TimeSpan.Zero;
            diagnosisCompletion.TrySetResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

}
