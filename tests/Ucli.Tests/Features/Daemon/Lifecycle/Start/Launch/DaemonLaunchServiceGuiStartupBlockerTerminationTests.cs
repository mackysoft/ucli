using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchServiceGuiStartupBlockerTerminationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupBlockedAndPolicyTerminates_TerminatesCliLaunchedGuiProcess ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-terminate"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6544, processStartedAtUtc),
        };
        var blocker = DaemonGuiStartupBlockerObservationTestFactory.Create(
            processId: 6544,
            processStartedAtUtc,
            unityLogPath: $"/tmp/repo-root/.ucli/local/fingerprints/{context.ProjectFingerprint}/unity.log");
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
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

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 6544,
            processStartedAtUtc: processStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.Terminated, result.Startup!.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupBlockedAndCleanupFails_PreservesPrimaryBlockerAndRecordsUnknownProcessAction ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-terminate-cleanup-fail"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var primaryDiagnostic = new DaemonPrimaryDiagnostic(
            Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
            Code: "CS0103",
            File: "Assets/Foo.cs",
            Line: 12,
            Column: 9,
            Message: "The name does not exist");
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6545, processStartedAtUtc),
        };
        var blocker = DaemonGuiStartupBlockerObservationTestFactory.Create(
            processId: 6545,
            processStartedAtUtc,
            unityLogPath: $"/tmp/repo-root/.ucli/local/fingerprints/{context.ProjectFingerprint}/unity.log",
            primaryDiagnostic: primaryDiagnostic);
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError("cleanup failed")),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
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
            DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("StartupError=Unity Editor startup is blocked because scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisStartupPhase.ScriptCompilation, result.Diagnosis.StartupPhase);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, result.Diagnosis.ActionRequired);
        Assert.Equal(primaryDiagnostic, result.Diagnosis.PrimaryDiagnostic);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 6545,
            processStartedAtUtc: processStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.Unknown, result.Startup!.ProcessAction);
        Assert.Equal(DaemonStartupBlockingReason.Compile, result.Startup.StartupBlockingReason);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context);
        Assert.Equal(DaemonStartupProcessAction.Unknown, launchAttempt.ProcessAction);
        Assert.Equal(result.Diagnosis, launchAttempt.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFindsProcessExit_CleansArtifactsWithoutTerminationTarget ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-process-exit"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var blocker = DaemonGuiStartupBlockerObservationTestFactory.Create(
            processId: 6543,
            processStartedAtUtc,
            unityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-gui-launch-process-exit/unity.log",
            startupBlockingReason: DaemonStartupBlockingReason.ProcessExit,
            reason: DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
            retryDisposition: DaemonStartupRetryDisposition.Unknown,
            message: "Unity Editor process exited before GUI daemon session registration. ProcessId=6543.",
            startupPhase: DaemonDiagnosisStartupPhase.ProcessExit,
            actionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            primaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: "Unity Editor process exited before GUI daemon session registration. ProcessId=6543."));
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartProcessExited, error.Code);
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap, diagnosis.Reason);
        DaemonLaunchInvocationAssert.LaunchCompensationAttemptedWithoutProcessTarget(compensationService, context);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessAction.None, result.Startup!.ProcessAction);
        Assert.Equal(DaemonStartupBlockingReason.ProcessExit, result.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.Unknown, result.Startup.RetryDisposition);
    }
}
