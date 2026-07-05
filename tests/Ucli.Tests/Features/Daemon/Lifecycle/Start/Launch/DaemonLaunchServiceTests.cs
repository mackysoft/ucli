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
        var startedSession = initialSession with
        {
            ProcessId = 999,
            ProcessStartedAtUtc = processStartedAtUtc,
        };
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
        var startedSession = initialSession with
        {
            ProcessId = 999,
            ProcessStartedAtUtc = processStartedAtUtc,
        };
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

}
