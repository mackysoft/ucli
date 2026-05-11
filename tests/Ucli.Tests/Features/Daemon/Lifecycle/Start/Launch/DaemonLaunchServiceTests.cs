using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

public sealed class DaemonLaunchServiceTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAndReadinessSucceed_ReturnsStarted ()
    {
        var context = CreateContext("fingerprint-launch-success");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var startedSession = initialSession with { ProcessId = 999 };
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(startedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(999, DateTimeOffset.UtcNow),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Ready(DaemonStartLifecycleSnapshot.Ready()),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(startedSession, result.Session);
        Assert.Equal(1, launchSessionService.InitializeCallCount);
        Assert.Equal(1, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(1, launcher.CallCount);
        Assert.Equal(0, compensationService.CallCount);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGui_LaunchesGuiAndWaitsForRegisteredSessionWithoutPrewritingSession ()
    {
        var context = CreateContext("fingerprint-gui-launch-success");
        var registeredSession = CreateSession(
            processId: 4321,
            projectFingerprint: context.ProjectFingerprint) with
        {
            EditorMode = DaemonEditorModeValues.Gui,
        };
        var launchSessionService = new StubDaemonLaunchSessionService();
        var batchmodeLauncher = new StubUnityDaemonProcessLauncher();
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(4321, DateTimeOffset.UtcNow),
        };
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Success(registeredSession),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.Equal(registeredSession, result.Session);
        Assert.Equal(0, launchSessionService.InitializeCallCount);
        Assert.Equal(0, batchmodeLauncher.CallCount);
        Assert.Equal(1, guiLauncher.CallCount);
        Assert.Equal(1, guiStartupObserver.CallCount);
        Assert.Equal(4321, guiStartupObserver.LastProcessId);
        Assert.Equal(0, compensationService.CallCount);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiRegistrationTimesOut_WritesGuiEndpointDiagnosis ()
    {
        var context = CreateContext("fingerprint-gui-launch-timeout");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var launchSessionService = new StubDaemonLaunchSessionService();
        var batchmodeLauncher = new StubUnityDaemonProcessLauncher();
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(5432, processStartedAtUtc),
        };
        var timeoutError = ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout);
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(timeoutError),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
        var service = CreateService(
            launchSessionService,
            batchmodeLauncher,
            new StubDaemonStartupReadinessProbe(),
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.GuiEndpointNotRegistered, diagnosisStore.LastDiagnosis!.Reason);
        Assert.True(diagnosisStore.LastDiagnosis.IsInferred);
        Assert.Equal(5432, diagnosisStore.LastDiagnosis.ProcessId);
        Assert.Equal(processStartedAtUtc, diagnosisStore.LastDiagnosis.ProcessStartedAtUtc);
        Assert.Equal(
            Path.GetFullPath(Path.Combine("/tmp/repo-root", ".ucli", "local", "fingerprints", context.ProjectFingerprint, "unity.log")),
            diagnosisStore.LastDiagnosis.UnityLogPath);
        Assert.Equal(DaemonDiagnosisStartupPhaseValues.EndpointRegistration, diagnosisStore.LastDiagnosis.StartupPhase);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, diagnosisStore.LastDiagnosis.ActionRequired);
        Assert.Equal(
            Path.Combine("/tmp/unity-project", "Library", "EditorInstance.json"),
            diagnosisStore.LastDiagnosis.EditorInstancePath);
        Assert.Equal(0, compensationService.CallCount);
        Assert.Null(compensationService.LastProcessId);
        Assert.Null(compensationService.LastProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessActionValues.Kept, result.Startup!.ProcessAction);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(DaemonStartupStatusValues.Timeout, launchAttemptStore.LastLaunchAttempt!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Kept, launchAttemptStore.LastLaunchAttempt.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiRegistrationTimesOutAndCompensationFails_RecordsUnknownProcessAction ()
    {
        var context = CreateContext("fingerprint-gui-launch-timeout-cleanup-fail");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(5433, processStartedAtUtc),
        };
        var timeoutError = ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout);
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(timeoutError),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError("cleanup failed")),
        };
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
            compensationService,
            new StubDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, result.Startup!.ProcessAction);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(DaemonStartupStatusValues.Timeout, launchAttemptStore.LastLaunchAttempt!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, launchAttemptStore.LastLaunchAttempt.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupWaitIsCanceled_RunsCompensationAndRethrows ()
    {
        var context = CreateContext("fingerprint-gui-launch-cancel");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7654, processStartedAtUtc),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            Handler = _ =>
            {
                cancellationTokenSource.Cancel();
                return ValueTask.FromCanceled<DaemonGuiStartupObservationResult>(cancellationTokenSource.Token);
            },
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
            compensationService,
            new StubDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Gui,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    cancellationTokenSource.Token)
                .AsTask());

        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(7654, compensationService.LastProcessId);
        Assert.Equal(processStartedAtUtc, compensationService.LastProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFails_RunsCompensationAndReturnsFailure ()
    {
        var context = CreateContext("fingerprint-gui-launch-observer-fail");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var startupError = ExecutionError.InternalError("observer failed");
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(8765, processStartedAtUtc),
        };
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(startupError),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
            compensationService,
            new StubDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(startupError, result.Error);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(8765, compensationService.LastProcessId);
        Assert.Equal(processStartedAtUtc, compensationService.LastProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, result.Startup!.ProcessAction);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(DaemonStartupStatusValues.Failed, launchAttemptStore.LastLaunchAttempt!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, launchAttemptStore.LastLaunchAttempt.ProcessAction);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData(DaemonStartupBlockedProcessPolicy.Keep)]
    public async Task Launch_WhenEditorModeGuiStartupObserverFindsCompilerErrorWithKeepPolicy_WritesStartupBlockedDiagnosisAndPreservesGuiProcess (
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        var context = CreateContext("fingerprint-gui-launch-compiler-error");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
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
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Compile,
            Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            RetryDisposition: DaemonStartupRetryDispositionValues.RetryAfterFix,
            Message: "Unity Editor startup is blocked because scripts have compiler errors. FirstError=Assets/Foo.cs(74,17): error CS1739: Missing parameter",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            ProcessId: 6543,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-gui-launch-compiler-error/unity.log",
            PrimaryDiagnostic: primaryDiagnostic);
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, diagnosisStore.LastDiagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisStartupPhaseValues.ScriptCompilation, diagnosisStore.LastDiagnosis.StartupPhase);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, diagnosisStore.LastDiagnosis.ActionRequired);
        Assert.Equal(processStartedAtUtc, diagnosisStore.LastDiagnosis.ProcessStartedAtUtc);
        Assert.Equal(blocker.UnityLogPath, diagnosisStore.LastDiagnosis.UnityLogPath);
        Assert.Equal(primaryDiagnostic, diagnosisStore.LastDiagnosis.PrimaryDiagnostic);
        Assert.Equal(diagnosisStore.LastDiagnosis, result.Diagnosis);
        Assert.Equal(0, compensationService.CallCount);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessActionValues.Kept, result.Startup!.ProcessAction);
        Assert.Equal(DaemonStartupBlockingReasonValues.Compile, result.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, result.Startup.RetryDisposition);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(1, launchAttemptStore.PruneCallCount);
        Assert.Equal(result.Startup.LaunchAttemptId, launchAttemptStore.LastLaunchAttempt!.LaunchAttemptId);
        Assert.Equal(DaemonStartupStatusValues.Blocked, launchAttemptStore.LastLaunchAttempt.StartupStatus);
        Assert.Equal(blocker.UnityLogPath, launchAttemptStore.LastLaunchAttempt.UnityLogPath);
        Assert.Equal(diagnosisStore.LastDiagnosis, launchAttemptStore.LastLaunchAttempt.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAttemptIdDirectoryAlreadyExists_RegeneratesLaunchAttemptId ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-service", "launch-attempt-id-collision");
        var context = new ResolvedUnityProjectContext(
            UnityProjectRoot: Path.Combine(scope.FullPath, "UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "fingerprint-id-collision",
            PathSource: UnityProjectPathSource.CommandOption);
        Directory.CreateDirectory(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            context.RepositoryRoot,
            context.ProjectFingerprint,
            "20260312_000000Z_00000001"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var blocker = new DaemonGuiStartupBlocker(
            StartupBlockingReason: DaemonStartupBlockingReasonValues.ProcessExit,
            Reason: DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
            RetryDisposition: DaemonStartupRetryDispositionValues.Unknown,
            Message: "Unity Editor exited before bootstrap completed.",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ProcessExit,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            ProcessId: 6543,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-id-collision/unity.log",
            PrimaryDiagnostic: null);
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
            new StubDaemonLaunchCompensationService(),
            new StubDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Keep,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.NotNull(result.Startup);
        Assert.Equal("20260312_000000Z_00000002", result.Startup!.LaunchAttemptId);
        Assert.Equal("20260312_000000Z_00000002", launchAttemptStore.LastLaunchAttempt!.LaunchAttemptId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupBlockedAndPolicyTerminates_TerminatesCliLaunchedGuiProcess ()
    {
        var context = CreateContext("fingerprint-gui-launch-terminate");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6544, processStartedAtUtc),
        };
        var blocker = new DaemonGuiStartupBlocker(
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Compile,
            Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            RetryDisposition: DaemonStartupRetryDispositionValues.RetryAfterFix,
            Message: "Unity Editor startup is blocked because scripts have compiler errors.",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            ProcessId: 6544,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: $"/tmp/repo-root/.ucli/local/fingerprints/{context.ProjectFingerprint}/unity.log",
            PrimaryDiagnostic: null);
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
            compensationService,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(6544, compensationService.LastProcessId);
        Assert.Equal(processStartedAtUtc, compensationService.LastProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, result.Startup!.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFindsActionableBlocker_WritesDiagnosisAndPreservesGuiProcess ()
    {
        const string startupBlockingReason = DaemonStartupBlockingReasonValues.SafeMode;
        const string reason = DaemonDiagnosisReasonValues.EditorUserActionRequired;
        const string retryDisposition = DaemonStartupRetryDispositionValues.ManualActionRequired;
        const string startupPhase = DaemonDiagnosisStartupPhaseValues.UserAction;
        const string actionRequired = DaemonDiagnosisActionRequiredValues.ResolveUnityDialog;

        var context = CreateContext($"fingerprint-gui-launch-{startupBlockingReason}");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
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
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
            compensationService,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(reason, diagnosisStore.LastDiagnosis!.Reason);
        Assert.Equal(startupPhase, diagnosisStore.LastDiagnosis.StartupPhase);
        Assert.Equal(actionRequired, diagnosisStore.LastDiagnosis.ActionRequired);
        Assert.Equal(processStartedAtUtc, diagnosisStore.LastDiagnosis.ProcessStartedAtUtc);
        Assert.Equal(blocker.UnityLogPath, diagnosisStore.LastDiagnosis.UnityLogPath);
        Assert.Equal(diagnosisStore.LastDiagnosis, result.Diagnosis);
        Assert.Equal(0, compensationService.CallCount);
        Assert.NotNull(result.Startup);
        Assert.Equal(startupBlockingReason, result.Startup!.StartupBlockingReason);
        Assert.Equal(retryDisposition, result.Startup.RetryDisposition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFindsProcessExit_CleansArtifactsWithoutTerminationTarget ()
    {
        var context = CreateContext("fingerprint-gui-launch-process-exit");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new StubUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var blocker = new DaemonGuiStartupBlocker(
            StartupBlockingReason: DaemonStartupBlockingReasonValues.ProcessExit,
            Reason: DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
            RetryDisposition: DaemonStartupRetryDispositionValues.Unknown,
            Message: "Unity Editor process exited before GUI daemon session registration. ProcessId=6543.",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ProcessExit,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            ProcessId: 6543,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-gui-launch-process-exit/unity.log",
            PrimaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: "Unity Editor process exited before GUI daemon session registration. ProcessId=6543."));
        var guiStartupObserver = new StubDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            new StubDaemonLaunchSessionService(),
            new StubUnityDaemonProcessLauncher(),
            new StubDaemonStartupReadinessProbe(),
            compensationService,
            diagnosisStore,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartProcessExited, error.Code);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap, diagnosisStore.LastDiagnosis!.Reason);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Null(compensationService.LastProcessId);
        Assert.Null(compensationService.LastProcessStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupProcessActionValues.None, result.Startup!.ProcessAction);
        Assert.Equal(DaemonStartupBlockingReasonValues.ProcessExit, result.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDispositionValues.Unknown, result.Startup.RetryDisposition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionInitializationFails_ReturnsFailureWithoutLaunch ()
    {
        var context = CreateContext("fingerprint-launch-init-fail");
        var expectedError = ExecutionError.InternalError("session init failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Failure(expectedError),
        };
        var launcher = new StubUnityDaemonProcessLauncher();
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, launchSessionService.InitializeCallCount);
        Assert.Equal(0, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(0, launcher.CallCount);
        Assert.Equal(0, compensationService.CallCount);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
        Assert.NotNull(result.Startup);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(1, launchAttemptStore.PruneCallCount);
        Assert.Equal(result.Startup!.LaunchAttemptId, launchAttemptStore.LastLaunchAttempt!.LaunchAttemptId);
        Assert.Equal(DaemonStartupStatusValues.Failed, launchAttemptStore.LastLaunchAttempt.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.None, launchAttemptStore.LastLaunchAttempt.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityLaunchFails_RunsCompensationAndReturnsLaunchFailure ()
    {
        var context = CreateContext("fingerprint-launch-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Null(compensationService.LastProcessId);
        Assert.Null(compensationService.LastProcessStartedAtUtc);
        Assert.Equal(0, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.StartupFailed, diagnosisStore.LastDiagnosis!.Reason);
        Assert.Equal(launchError.Message, diagnosisStore.LastDiagnosis.Message);
        Assert.Equal(initialSession.IssuedAtUtc, diagnosisStore.LastDiagnosis.SessionIssuedAtUtc);
        Assert.Equal(diagnosisStore.LastDiagnosis, result.Diagnosis);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(1, launchAttemptStore.PruneCallCount);
        Assert.Equal(DaemonStartupStatusValues.Failed, launchAttemptStore.LastLaunchAttempt!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.None, launchAttemptStore.LastLaunchAttempt.ProcessAction);
        Assert.Equal(diagnosisStore.LastDiagnosis, launchAttemptStore.LastLaunchAttempt.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionUpdateFails_RunsCompensationAndReturnsWriteFailure ()
    {
        var context = CreateContext("fingerprint-session-update-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var writeError = ExecutionError.InternalError("write failed");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Failure(writeError),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(2222, processStartedAtUtc),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(writeError, result.Error);
        Assert.Equal(1, launchSessionService.UpdateProcessIdCallCount);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(2222, compensationService.LastProcessId);
        Assert.NotNull(compensationService.LastProcessStartedAtUtc);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.Equal(processStartedAtUtc, diagnosisStore.LastDiagnosis?.ProcessStartedAtUtc);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(DaemonStartupStatusValues.Failed, launchAttemptStore.LastLaunchAttempt!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, launchAttemptStore.LastLaunchAttempt.ProcessAction);
        Assert.Equal(processStartedAtUtc, launchAttemptStore.LastLaunchAttempt.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeFails_RunsCompensationAndReturnsProbeFailure ()
    {
        var context = CreateContext("fingerprint-probe-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var probeError = ExecutionError.Timeout("probe failed");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777, processStartedAtUtc),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(probeError, result.Error);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(7777, compensationService.LastProcessId);
        Assert.NotNull(compensationService.LastProcessStartedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.Equal(processStartedAtUtc, diagnosisStore.LastDiagnosis?.ProcessStartedAtUtc);
        Assert.Equal(diagnosisStore.LastDiagnosis, result.Diagnosis);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(DaemonStartupStatusValues.Timeout, launchAttemptStore.LastLaunchAttempt!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, launchAttemptStore.LastLaunchAttempt.ProcessAction);
        Assert.Equal(processStartedAtUtc, launchAttemptStore.LastLaunchAttempt.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeReturnsClassifiedBlocker_PreservesStartupClassification ()
    {
        var context = CreateContext("fingerprint-probe-classified-blocker");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7778 };
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
        var classification = new DaemonStartupFailureClassification(
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Compile,
            Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            RetryDisposition: DaemonStartupRetryDispositionValues.RetryAfterFix,
            Message: "Unity scripts have compiler errors.",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: null);
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7778, processStartedAtUtc),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(
                ExecutionError.InternalError(classification.Message, DaemonErrorCodes.DaemonStartupBlocked),
                classification),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.Error!.Code);
        Assert.Equal(DaemonStartupStatusValues.Blocked, result.Startup!.StartupStatus);
        Assert.Equal(DaemonStartupBlockingReasonValues.Compile, result.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, result.Startup.RetryDisposition);
        Assert.Equal(DaemonEditorModeValues.Batchmode, result.Startup.EditorMode);
        Assert.Equal(DaemonSessionOwnerKindValues.Cli, result.Startup.OwnerKind);
        Assert.Equal(7778, result.Startup.ProcessId);
        Assert.Equal(processStartedAtUtc, result.Startup.StartedAtUtc);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, result.Startup.ProcessAction);
        Assert.NotNull(result.Startup.ArtifactPath);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Diagnosis!.Reason);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(DaemonStartupStatusValues.Blocked, launchAttemptStore.LastLaunchAttempt!.StartupStatus);
        Assert.Equal(DaemonStartupBlockingReasonValues.Compile, launchAttemptStore.LastLaunchAttempt.StartupBlockingReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeClassifiedBlockerTerminates_SavesEvidenceBeforeCompensationAndStoresFinalProcessAction ()
    {
        var context = CreateContext("fingerprint-probe-classified-blocker-order");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7780 };
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
        var classification = new DaemonStartupFailureClassification(
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Compile,
            Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            RetryDisposition: DaemonStartupRetryDispositionValues.RetryAfterFix,
            Message: "Unity scripts have compiler errors.",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: null);
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(
                ExecutionError.InternalError(classification.Message, DaemonErrorCodes.DaemonStartupBlocked),
                classification),
        };
        var sequence = new List<string>();
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            OnWrite = _ => sequence.Add("diagnosis"),
        };
        var launchAttemptStore = new StubDaemonLaunchAttemptStore
        {
            OnWrite = attempt => sequence.Add($"launchAttempt:{attempt.ProcessAction}"),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            OnCleanup = () => sequence.Add("cleanup"),
        };
        var service = CreateService(
            launchSessionService,
            new StubUnityDaemonProcessLauncher
            {
                NextResult = UnityDaemonLaunchResult.Success(7780, processStartedAtUtc),
            },
            readinessProbe,
            compensationService,
            diagnosisStore,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, result.Startup!.ProcessAction);
        Assert.Equal(
            [
                "diagnosis",
                $"launchAttempt:{DaemonStartupProcessActionValues.Unknown}",
                "cleanup",
                $"launchAttempt:{DaemonStartupProcessActionValues.Terminated}",
            ],
            sequence);
        Assert.Equal(2, launchAttemptStore.WriteCallCount);
        Assert.Equal(1, launchAttemptStore.PruneCallCount);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, launchAttemptStore.LastLaunchAttempt!.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeReturnsClassifiedBlockerAndKeepPolicy_PreservesProcess ()
    {
        var context = CreateContext("fingerprint-probe-classified-blocker-keep");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7779 };
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
        var classification = new DaemonStartupFailureClassification(
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Compile,
            Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            RetryDisposition: DaemonStartupRetryDispositionValues.RetryAfterFix,
            Message: "Unity scripts have compiler errors.",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: null);
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7779, processStartedAtUtc),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(
                ExecutionError.InternalError(classification.Message, DaemonErrorCodes.DaemonStartupBlocked),
                classification),
        };
        var compensationService = new StubDaemonLaunchCompensationService();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore();
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
            DaemonStartupBlockedProcessPolicy.Keep,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(DaemonStartupProcessActionValues.Kept, result.Startup!.ProcessAction);
        Assert.Equal(0, compensationService.CallCount);
        Assert.Equal(DaemonStartupProcessActionValues.Kept, launchAttemptStore.LastLaunchAttempt!.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAttemptWriteFails_PreservesPrimaryTimeoutFailureAndStartupObservation ()
    {
        var context = CreateContext("fingerprint-launch-attempt-write-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var probeError = ExecutionError.Timeout("probe failed", ExecutionErrorCodes.IpcTimeout);
        var artifactError = ExecutionError.InternalError("artifact write failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777, DateTimeOffset.UtcNow),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var launchAttemptStore = new StubDaemonLaunchAttemptStore
        {
            WriteResult = DaemonLaunchAttemptStoreOperationResult.Failure(artifactError),
        };
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            new StubDaemonLaunchCompensationService(),
            new StubDaemonDiagnosisStore(),
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("ArtifactError=artifact write failed", error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupStatusValues.Timeout, result.Startup!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, result.Startup.ProcessAction);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(0, launchAttemptStore.PruneCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAttemptPruneFails_PreservesPrimaryTimeoutFailureAndStartupObservation ()
    {
        var context = CreateContext("fingerprint-launch-attempt-prune-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7778 };
        var probeError = ExecutionError.Timeout("probe failed", ExecutionErrorCodes.IpcTimeout);
        var artifactError = ExecutionError.InternalError("artifact prune failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7778, DateTimeOffset.UtcNow),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var launchAttemptStore = new StubDaemonLaunchAttemptStore
        {
            PruneResult = DaemonLaunchAttemptStoreOperationResult.Failure(artifactError),
        };
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            new StubDaemonLaunchCompensationService(),
            new StubDaemonDiagnosisStore(),
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("ArtifactError=artifact prune failed", error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonStartupStatusValues.Timeout, result.Startup!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Terminated, result.Startup.ProcessAction);
        Assert.Equal(1, launchAttemptStore.WriteCallCount);
        Assert.Equal(1, launchAttemptStore.PruneCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCompensationFails_ReturnsInternalError ()
    {
        var context = CreateContext("fingerprint-launch-compensation-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed");
        var cleanupError = ExecutionError.InternalError("cleanup failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(cleanupError),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Daemon launch failed and cleanup failed.", error.Message, StringComparison.Ordinal);
        Assert.Contains("LaunchError=launch failed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosisError=", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchFailureOccursAfterDeadline_StillRunsCompensation ()
    {
        var context = CreateContext("fingerprint-launch-timeout-compensation");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var timeProvider = new ManualTimeProvider();
        var launchError = ExecutionError.InternalError("launch failed after timeout");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            LaunchDelay = TimeSpan.FromMilliseconds(50),
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
            TimeProvider = timeProvider,
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore,
            timeProvider: timeProvider);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(1),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisWriteFails_PreservesPrimaryTimeoutErrorKind ()
    {
        var context = CreateContext("fingerprint-diagnosis-write-timeout");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var probeError = ExecutionError.Timeout("probe failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777, DateTimeOffset.UtcNow),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("diagnosis write failed")),
        };
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("DiagnosisError=diagnosis write failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCleanupFails_PreservesPrimaryTimeoutErrorKind ()
    {
        var context = CreateContext("fingerprint-cleanup-timeout");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var probeError = ExecutionError.Timeout("probe failed");
        var cleanupError = ExecutionError.InternalError("cleanup failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777, DateTimeOffset.UtcNow),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(cleanupError),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("ProbeError=probe failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCancellationRequestedAfterLaunchFailure_StillRunsCompensation ()
    {
        var context = CreateContext("fingerprint-launch-cancel-after-failure");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed after cancel");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        using var cancellationSource = new CancellationTokenSource();
        var launcher = new StubUnityDaemonProcessLauncher
        {
            OnLaunch = cancellationSource.Cancel,
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
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
            cancellationSource.Token);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        Assert.True(cancellationSource.IsCancellationRequested);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCanceledDuringReadinessProbe_RunsCompensationThenThrows ()
    {
        var context = CreateContext("fingerprint-launch-cancel-during-readiness");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var updatedSession = initialSession with { ProcessId = 7777 };
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777, DateTimeOffset.UtcNow),
        };
        using var cancellationSource = new CancellationTokenSource();
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            OnWaitUntilReady = cancellationSource.Cancel,
            NextException = new OperationCanceledException(cancellationSource.Token),
        };
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.LaunchAsync(
                        context,
                        TimeSpan.FromMilliseconds(500),
                        DaemonEditorMode.Batchmode,
                        DaemonStartupBlockedProcessPolicy.Auto,
                        cancellationSource.Token)
                    .AsTask(),
                "Canceled daemon launch result",
                AsyncWaitTimeout);
        });

        Assert.True(cancellationSource.IsCancellationRequested);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(7777, compensationService.LastProcessId);
        Assert.NotNull(compensationService.LastProcessStartedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(10), compensationService.LastTimeout);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisWriteFails_ReturnsInternalError ()
    {
        var context = CreateContext("fingerprint-launch-diagnosis-fail");
        var initialSession = CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint);
        var launchError = ExecutionError.InternalError("launch failed");
        var diagnosisError = ExecutionError.InternalError("diagnosis failed");
        var launchSessionService = new StubDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe();
        var compensationService = new StubDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(diagnosisError),
        };
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
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Daemon launch failed and diagnosis persistence failed.", error.Message, StringComparison.Ordinal);
        Assert.Contains("LaunchError=launch failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("DiagnosisError=diagnosis failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    private static DaemonLaunchService CreateService (
        IDaemonLaunchSessionService launchSessionService,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonLaunchCompensationService launchCompensationService,
        IDaemonDiagnosisStore? daemonDiagnosisStore = null,
        IUnityGuiEditorProcessLauncher? unityGuiEditorProcessLauncher = null,
        IDaemonGuiStartupObserver? guiStartupObserver = null,
        IDaemonLaunchAttemptIdGenerator? launchAttemptIdGenerator = null,
        IDaemonLaunchAttemptStore? launchAttemptStore = null,
        TimeProvider? timeProvider = null)
    {
        return new DaemonLaunchService(
            daemonLaunchSessionService: launchSessionService,
            unityDaemonProcessLauncher: unityDaemonProcessLauncher,
            unityGuiEditorProcessLauncher: unityGuiEditorProcessLauncher ?? new StubUnityGuiEditorProcessLauncher(),
            startupReadinessProbe: startupReadinessProbe,
            guiStartupObserver: guiStartupObserver ?? new StubDaemonGuiStartupObserver(),
            daemonLaunchCompensationService: launchCompensationService,
            daemonDiagnosisStore: daemonDiagnosisStore ?? new StubDaemonDiagnosisStore(),
            launchAttemptIdGenerator: launchAttemptIdGenerator ?? new StubDaemonLaunchAttemptIdGenerator(),
            launchAttemptStore: launchAttemptStore ?? new StubDaemonLaunchAttemptStore(),
            timeProvider: timeProvider);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        int? processId,
        string projectFingerprint = "fingerprint")
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,
            ProcessStartedAtUtc: processId is null ? null : DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private sealed class StubDaemonLaunchSessionService : IDaemonLaunchSessionService
    {
        public DaemonLaunchSessionWriteResult InitializeResult { get; set; } = DaemonLaunchSessionWriteResult.Success(CreateSession(processId: null));

        public DaemonLaunchSessionWriteResult? UpdateProcessIdResult { get; set; }

        public int InitializeCallCount { get; private set; }

        public int UpdateProcessIdCallCount { get; private set; }

        public ValueTask<DaemonLaunchSessionWriteResult> InitializeAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonEditorMode editorMode,
            CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return ValueTask.FromResult(InitializeResult);
        }

        public ValueTask<DaemonLaunchSessionWriteResult> UpdateProcessIdAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            int? processId,
            DateTimeOffset? processStartedAtUtc,
            CancellationToken cancellationToken = default)
        {
            UpdateProcessIdCallCount++;
            if (UpdateProcessIdResult is not null)
            {
                return ValueTask.FromResult(UpdateProcessIdResult);
            }

            var updatedSession = processId is int pid
                ? session with { ProcessId = pid, ProcessStartedAtUtc = processStartedAtUtc }
                : session;
            return ValueTask.FromResult(DaemonLaunchSessionWriteResult.Success(updatedSession));
        }
    }

    private sealed class StubUnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
    {
        public Action? OnLaunch { get; set; }

        public TimeSpan LaunchDelay { get; set; }

        public ManualTimeProvider? TimeProvider { get; set; }

        public UnityDaemonLaunchResult NextResult { get; set; } = UnityDaemonLaunchResult.Success(1000, DateTimeOffset.UtcNow);

        public int CallCount { get; private set; }

        public async ValueTask<UnityDaemonLaunchResult> LaunchAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            string daemonLogPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            OnLaunch?.Invoke();
            if (LaunchDelay > TimeSpan.Zero)
            {
                if (TimeProvider != null)
                {
                    TimeProvider.Advance(LaunchDelay);
                }
                else
                {
                    throw new InvalidOperationException("ManualTimeProvider is required when LaunchDelay is configured.");
                }
            }

            return NextResult;
        }
    }

    private sealed class StubUnityGuiEditorProcessLauncher : IUnityGuiEditorProcessLauncher
    {
        public UnityDaemonLaunchResult NextResult { get; set; } = UnityDaemonLaunchResult.Success(2000, DateTimeOffset.UtcNow);

        public int CallCount { get; private set; }

        public ValueTask<UnityDaemonLaunchResult> LaunchAsync (
            ResolvedUnityProjectContext unityProject,
            string unityLogPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonGuiStartupObserver : IDaemonGuiStartupObserver
    {
        public DaemonGuiStartupObservationResult NextResult { get; set; } =
            DaemonGuiStartupObservationResult.Success(CreateSession(processId: 2000));

        public Func<CancellationToken, ValueTask<DaemonGuiStartupObservationResult>>? Handler { get; set; }

        public int CallCount { get; private set; }

        public int LastProcessId { get; private set; }

        public ValueTask<DaemonGuiStartupObservationResult> WaitForStartupAsync (
            ResolvedUnityProjectContext unityProject,
            int processId,
            DateTimeOffset processStartedAtUtc,
            string unityLogPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastProcessId = processId;
            if (Handler is not null)
            {
                return Handler(cancellationToken);
            }

            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
    {
        public DaemonStartupReadinessProbeResult NextResult { get; set; } = DaemonStartupReadinessProbeResult.Ready(DaemonStartLifecycleSnapshot.Ready());

        public Action? OnWaitUntilReady { get; set; }

        public Exception? NextException { get; set; }

        public ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReadyAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            int? daemonProcessId = null,
            CancellationToken cancellationToken = default)
        {
            OnWaitUntilReady?.Invoke();
            if (NextException is not null)
            {
                return ValueTask.FromException<DaemonStartupReadinessProbeResult>(NextException);
            }

            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } = DaemonDiagnosisStoreOperationResult.Success();

        public Action<DaemonDiagnosis>? OnWrite { get; set; }

        public int WriteCallCount { get; private set; }

        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            LastDiagnosis = diagnosis;
            OnWrite?.Invoke(diagnosis);
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonLaunchAttemptIdGenerator : IDaemonLaunchAttemptIdGenerator
    {
        private int sequence;

        public string Create (DateTimeOffset startedAtUtc)
        {
            sequence++;
            return $"20260312_000000Z_{sequence:00000000}";
        }
    }

    private sealed class StubDaemonLaunchAttemptStore : IDaemonLaunchAttemptStore
    {
        private readonly List<DaemonLaunchAttempt> launchAttempts = [];

        public DaemonLaunchAttemptStoreOperationResult WriteResult { get; set; } = DaemonLaunchAttemptStoreOperationResult.Success();

        public DaemonLaunchAttemptStoreOperationResult PruneResult { get; set; } = DaemonLaunchAttemptStoreOperationResult.Success();

        public Action<DaemonLaunchAttempt>? OnWrite { get; set; }

        public int WriteCallCount { get; private set; }

        public int PruneCallCount { get; private set; }

        public DaemonLaunchAttempt? LastLaunchAttempt { get; private set; }

        public IReadOnlyList<DaemonLaunchAttempt> LaunchAttempts => launchAttempts;

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteFailureAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonLaunchAttempt launchAttempt,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            LastLaunchAttempt = launchAttempt;
            launchAttempts.Add(launchAttempt);
            OnWrite?.Invoke(launchAttempt);
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonLaunchAttemptReadResult> ReadLastFailureAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
            string storageRoot,
            string projectFingerprint,
            int keepCount,
            CancellationToken cancellationToken = default)
        {
            PruneCallCount++;
            return ValueTask.FromResult(PruneResult);
        }
    }

    private sealed class StubDaemonLaunchCompensationService : IDaemonLaunchCompensationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public Action? OnCleanup { get; set; }

        public int CallCount { get; private set; }

        public int? LastProcessId { get; private set; }

        public DateTimeOffset? LastProcessStartedAtUtc { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunchAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonProcessTerminationTarget? target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProcessId = target?.ProcessId;
            LastProcessStartedAtUtc = target?.ProcessStartedAtUtc;
            LastTimeout = timeout;
            OnCleanup?.Invoke();
            return ValueTask.FromResult(NextResult);
        }
    }
}
