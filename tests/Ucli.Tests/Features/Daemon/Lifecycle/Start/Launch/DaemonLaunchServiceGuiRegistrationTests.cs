using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceGuiRegistrationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGui_LaunchesGuiAndWaitsForRegisteredSessionWithoutPrewritingSession ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-success"));
        var registeredSession = DaemonSessionTestFactory.Create(
            processId: 4321,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: "gui",
            endpointAddress: LaunchEndpointAddress);
        var launchSessionService = new RecordingDaemonLaunchSessionService();
        var batchmodeLauncher = new RecordingUnityDaemonProcessLauncher();
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(4321, DateTimeOffset.UtcNow),
        };
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Success(registeredSession),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            batchmodeLauncher,
            readinessProbe,
            compensationService,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.Equal(registeredSession, result.Session);
        DaemonLaunchInvocationAssert.GuiLaunchCompletedWithoutPrewrittenSessionOrCompensation(
            launchSessionService,
            batchmodeLauncher,
            guiLauncher,
            guiStartupObserver,
            compensationService,
            diagnosisStore,
            context,
            processId: 4321);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenGuiLaunchAndRegistrationSucceed_EmitsStartupProgress ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-progress"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero);
        var registeredSession = DaemonSessionTestFactory.Create(
            processId: 4321,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: "gui",
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: processStartedAtUtc);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(4321, processStartedAtUtc),
        };
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Success(
                registeredSession,
                new DaemonStartLifecycleSnapshot(
                    IpcEditorLifecycleStateCodec.Ready,
                    null,
                    CanAcceptExecutionRequests: true)),
        };
        var progressObserver = new CollectingDaemonStartProgressObserver();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            new RecordingDaemonLaunchCompensationService(),
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.Launching,
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.SessionRegistered,
            DaemonStartProgressEvent.EndpointRegistered,
            DaemonStartProgressEvent.LifecycleObserved);
        var launchingObservation = progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(0);
        Assert.Equal("gui", launchingObservation.EditorMode);
        Assert.Null(launchingObservation.ProcessId);
        var waitingObservation = progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(1);
        Assert.Equal(4321, waitingObservation.ProcessId);
        var lifecycleSnapshot = progressObserver.PayloadAt<DaemonStartLifecycleSnapshot>(^1);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, lifecycleSnapshot.LifecycleState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiRegistrationTimesOut_WritesGuiEndpointDiagnosis ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-timeout"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var launchSessionService = new RecordingDaemonLaunchSessionService();
        var batchmodeLauncher = new RecordingUnityDaemonProcessLauncher();
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(5432, processStartedAtUtc),
        };
        var timeoutError = ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout);
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(timeoutError),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            launchSessionService,
            batchmodeLauncher,
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(DaemonDiagnosisReasonValues.GuiEndpointNotRegistered, diagnosis.Reason);
        Assert.True(diagnosis.IsInferred);
        Assert.Equal(5432, diagnosis.ProcessId);
        Assert.Equal(processStartedAtUtc, diagnosis.ProcessStartedAtUtc);
        Assert.Equal(
            Path.GetFullPath(Path.Combine("/tmp/repo-root", ".ucli", "local", "fingerprints", context.ProjectFingerprint.ToString(), "unity.log")),
            diagnosis.UnityLogPath);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration), diagnosis.StartupPhase);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, diagnosis.ActionRequired);
        Assert.Equal(
            Path.Combine("/tmp/unity-project", "Library", "EditorInstance.json"),
            diagnosis.EditorInstancePath);
        DaemonLaunchInvocationAssert.StartupFailureKeptProcessWithoutCompensation(
            result,
            compensationService);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout), launchAttempt.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept), launchAttempt.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiRegistrationTimesOutAndTerminatePolicy_CleansFailedProcess ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-timeout-cleanup-success"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(5434, processStartedAtUtc),
        };
        var timeoutError = ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout);
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(timeoutError),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.NotNull(result.Startup);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), result.Startup!.ProcessAction);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 5434,
            processStartedAtUtc: processStartedAtUtc);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout), launchAttempt.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), launchAttempt.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiRegistrationTimesOutAndCompensationFails_RecordsUnknownProcessAction ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-timeout-cleanup-fail"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(5433, processStartedAtUtc),
        };
        var timeoutError = ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout);
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(timeoutError),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError("cleanup failed")),
        };
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.NotNull(result.Startup);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown), result.Startup!.ProcessAction);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 5433,
            processStartedAtUtc: processStartedAtUtc);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout), launchAttempt.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown), launchAttempt.ProcessAction);
    }
}
