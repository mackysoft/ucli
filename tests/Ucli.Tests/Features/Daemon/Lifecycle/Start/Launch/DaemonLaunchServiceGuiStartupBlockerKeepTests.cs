using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchServiceGuiStartupBlockerKeepTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Keep)]
    public async Task Launch_WhenEditorModeGuiStartupObserverFindsCompilerErrorWithKeepPolicy_WritesStartupBlockedDiagnosisAndPreservesGuiProcess (
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-gui-launch-compiler-error"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var primaryDiagnostic = new DaemonPrimaryDiagnostic(
            Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
            Code: "CS1739",
            File: "Assets/Foo.cs",
            Line: 74,
            Column: 17,
            Message: "Missing parameter");
        var blocker = new DaemonGuiStartupBlocker(
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
            Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix),
            Message: "Unity Editor startup is blocked because scripts have compiler errors. FirstError=Assets/Foo.cs(74,17): error CS1739: Missing parameter",
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation),
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            ProcessId: 6543,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-gui-launch-compiler-error/unity.log",
            PrimaryDiagnostic: primaryDiagnostic);
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var progressObserver = new CollectingDaemonStartProgressObserver();
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
            onStartupBlocked,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, diagnosis.Reason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation), diagnosis.StartupPhase);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, diagnosis.ActionRequired);
        Assert.Equal(processStartedAtUtc, diagnosis.ProcessStartedAtUtc);
        Assert.Equal(blocker.UnityLogPath, diagnosis.UnityLogPath);
        Assert.Equal(primaryDiagnostic, diagnosis.PrimaryDiagnostic);
        Assert.Equal(diagnosis, result.Diagnosis);
        var startup = DaemonLaunchInvocationAssert.StartupFailureKeptProcessWithoutCompensation(
            result,
            compensationService);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile), startup.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix), startup.RetryDisposition);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LaunchAttemptRecordedAndPrunedFor(
            launchAttemptStore,
            context,
            AssertStartupLaunchAttemptId(startup),
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
            ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept));
        Assert.Equal(blocker.UnityLogPath, launchAttempt.UnityLogPath);
        Assert.Equal(diagnosis, launchAttempt.Diagnosis);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.Launching,
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.BlockerDetected);
        var blockerObservation = progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(^1);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile), blockerObservation.StartupBlockingReason);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked.Value, blockerObservation.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFindsActionableBlocker_WritesDiagnosisAndPreservesGuiProcess ()
    {
        var startupBlockingReason = ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.SafeMode);
        const string reason = DaemonDiagnosisReasonValues.EditorUserActionRequired;
        var retryDisposition = ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.ManualActionRequired);
        var startupPhase = ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.UserAction);
        const string actionRequired = DaemonDiagnosisActionRequiredValues.ResolveUnityDialog;

        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create($"fingerprint-gui-launch-{startupBlockingReason}"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var blocker = new DaemonGuiStartupBlocker(
            StartupBlockingReason: startupBlockingReason,
            Reason: reason,
            RetryDisposition: retryDisposition,
            Message: "Unity Editor startup is blocked by an actionable GUI state.",
            StartupPhase: startupPhase,
            ActionRequired: actionRequired,
            ProcessId: 6543,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: $"/tmp/repo-root/.ucli/local/fingerprints/{context.ProjectFingerprint}/unity.log",
            PrimaryDiagnostic: null);
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
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(reason, diagnosis.Reason);
        Assert.Equal(startupPhase, diagnosis.StartupPhase);
        Assert.Equal(actionRequired, diagnosis.ActionRequired);
        Assert.Equal(processStartedAtUtc, diagnosis.ProcessStartedAtUtc);
        Assert.Equal(blocker.UnityLogPath, diagnosis.UnityLogPath);
        Assert.Equal(diagnosis, result.Diagnosis);
        var startup = DaemonLaunchInvocationAssert.StartupFailureKeptProcessWithoutCompensation(
            result,
            compensationService);
        Assert.Equal(startupBlockingReason, startup.StartupBlockingReason);
        Assert.Equal(retryDisposition, startup.RetryDisposition);
    }
}
