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
        var timeProvider = new ManualTimeProvider(processStartedAtUtc);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var primaryDiagnostic = new DaemonPrimaryDiagnostic(
            Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
            Code: "CS1739",
            File: "Assets/Foo.cs",
            Line: 74,
            Column: 17,
            Message: "Missing parameter");
        var blocker = DaemonGuiStartupBlockerObservationTestFactory.Create(
            processId: 6543,
            processStartedAtUtc,
            unityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-gui-launch-compiler-error/unity.log",
            message: "Unity Editor startup is blocked because scripts have compiler errors. FirstError=Assets/Foo.cs(74,17): error CS1739: Missing parameter",
            primaryDiagnostic: primaryDiagnostic);
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
            timeProvider,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            DaemonEditorMode.Gui,
            onStartupBlocked,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(DaemonDiagnosisReason.UnityScriptCompilationFailed, diagnosis.Reason);
        Assert.Equal(DaemonDiagnosisStartupPhase.ScriptCompilation, diagnosis.StartupPhase);
        Assert.Equal(DaemonDiagnosisActionRequired.FixCompileErrors, diagnosis.ActionRequired);
        Assert.Equal(processStartedAtUtc, diagnosis.ProcessStartedAtUtc);
        Assert.Equal(blocker.UnityLogPath, diagnosis.UnityLogPath);
        Assert.Equal(primaryDiagnostic, diagnosis.PrimaryDiagnostic);
        Assert.Equal(diagnosis, result.Diagnosis);
        var startup = DaemonLaunchInvocationAssert.StartupFailureKeptProcessWithoutCompensation(
            result,
            compensationService);
        Assert.Equal(DaemonStartupBlockingReason.Compile, startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.RetryAfterFix, startup.RetryDisposition);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LaunchAttemptRecordedAndPrunedFor(
            launchAttemptStore,
            context,
            AssertStartupLaunchAttemptId(startup),
            DaemonStartupStatus.Blocked,
            DaemonStartupProcessAction.Kept);
        Assert.Equal(blocker.UnityLogPath, launchAttempt.UnityLogPath);
        Assert.Equal(diagnosis, launchAttempt.Diagnosis);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.Launching,
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.BlockerDetected);
        var blockerObservation = progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(^1);
        Assert.Equal(DaemonStartupBlockingReason.Compile, blockerObservation.StartupBlockingReason);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked.Value, blockerObservation.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFindsActionableBlocker_WritesDiagnosisAndPreservesGuiProcess ()
    {
        const DaemonStartupBlockingReason startupBlockingReason = DaemonStartupBlockingReason.SafeMode;
        const DaemonDiagnosisReason reason = DaemonDiagnosisReason.EditorUserActionRequired;
        const DaemonStartupRetryDisposition retryDisposition = DaemonStartupRetryDisposition.ManualActionRequired;
        const DaemonDiagnosisStartupPhase startupPhase = DaemonDiagnosisStartupPhase.UserAction;
        const DaemonDiagnosisActionRequired actionRequired = DaemonDiagnosisActionRequired.ResolveUnityDialog;

        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create($"fingerprint-gui-launch-{startupBlockingReason}"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(processStartedAtUtc);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var blocker = DaemonGuiStartupBlockerObservationTestFactory.Create(
            processId: 6543,
            processStartedAtUtc,
            unityLogPath: $"/tmp/repo-root/.ucli/local/fingerprints/{context.ProjectFingerprint}/unity.log",
            startupBlockingReason: startupBlockingReason,
            reason: reason,
            retryDisposition: retryDisposition,
            message: "Unity Editor startup is blocked by an actionable GUI state.",
            startupPhase: startupPhase,
            actionRequired: actionRequired);
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
            timeProvider,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
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
