using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAndReadinessSucceed_ReturnsStarted ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-launch-success");
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero);
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
            NextResult = DaemonStartupReadinessProbeResult.Ready(DaemonStartLifecycleSnapshot.Ready()),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-batchmode-progress");
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero);
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
            NextResult = DaemonStartupReadinessProbeResult.Ready(new DaemonStartLifecycleSnapshot(
                IpcEditorLifecycleStateCodec.Ready,
                null,
                CanAcceptExecutionRequests: true)),
        };
        var progressObserver = new CollectingDaemonStartProgressObserver();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            new RecordingDaemonLaunchCompensationService(),
            new RecordingDaemonDiagnosisStore());

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
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
        Assert.Equal("batchmode", waitingObservation.EditorMode);
        Assert.Equal(999, waitingObservation.ProcessId);
        var lifecycleSnapshot = progressObserver.PayloadAt<DaemonStartLifecycleSnapshot>(^1);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, lifecycleSnapshot.LifecycleState);
        Assert.True(lifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeEndpointReadyProgressFails_RunsCompensationAndRethrows ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-batchmode-progress-fail");
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 11, 0, 0, 4, TimeSpan.Zero);
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
            NextResult = DaemonStartupReadinessProbeResult.Ready(DaemonStartLifecycleSnapshot.Ready()),
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
            new RecordingDaemonDiagnosisStore());

        var actualFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Batchmode,
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
            "fingerprint-batchmode-session-progress-fail");
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
            new RecordingDaemonDiagnosisStore());

        var actualFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Batchmode,
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-launch-init-fail");
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
            diagnosisStore,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
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
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
            ContractLiteralCodec.ToValue(DaemonStartupProcessAction.None));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionUpdateFails_RunsCompensationAndReturnsWriteFailure ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-session-update-fail");
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var writeError = ExecutionError.InternalError("write failed");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
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
            diagnosisStore,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
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
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
            ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated));
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed), launchAttempt.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), launchAttempt.ProcessAction);
        Assert.Equal(processStartedAtUtc, launchAttempt.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisPersistenceDoesNotComplete_StartsCompensationAndReturnsAfterSupplementalDeadlines ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-session-update-blocked-diagnosis");
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
        var diagnosisNeverCompletes = new TaskCompletionSource<DaemonDiagnosisStoreOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            WriteAsyncHandler = (_, _, _, _) =>
            {
                diagnosisWriteStarted.TrySetResult();
                return new ValueTask<DaemonDiagnosisStoreOperationResult>(diagnosisNeverCompletes.Task);
            },
        };
        var timeProvider = new ManualTimeProvider();
        var service = CreateService(
            launchSessionService,
            launcher,
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            timeProvider: timeProvider);

        var launchTask = service.LaunchAsync(
                context,
                TimeSpan.FromSeconds(30),
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();

        await compensationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await diagnosisWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var index = 0; index < 2; index++)
        {
            await timeProvider
                .WaitForTimerDueWithinAsync(DaemonTimeouts.SupplementalPersistenceTimeout)
                .WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(DaemonTimeouts.SupplementalPersistenceTimeout);
        }

        var result = await launchTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 2323,
            processStartedAtUtc: processStartedAtUtc);
    }

}
