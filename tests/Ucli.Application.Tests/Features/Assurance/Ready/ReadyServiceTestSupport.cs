using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Ready;

internal static class ReadyServiceTestSupport
{
    public static ReadyService CreateService (
        IProjectContextResolver? projectContextResolver = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IDaemonPingInfoClient? daemonPingInfoClient = null,
        IUnityRequestExecutor? unityRequestExecutor = null,
        IReadIndexArtifactReader? readIndexArtifactReader = null,
        IReadIndexFreshnessEvaluator? freshnessEvaluator = null,
        TimeProvider? timeProvider = null)
    {
        return new ReadyService(
            projectContextResolver ?? new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create())),
            modeDecisionService ?? CreateModeDecisionService(
                UnityExecutionMode.Auto,
                daemonRunning: false,
                UnityExecutionTarget.Oneshot),
            daemonPingInfoClient ?? new RecordingDaemonPingInfoClient(CreateReadyPingResponse()),
            unityRequestExecutor ?? new RecordingUnityRequestExecutor(CreateReadyPingSuccess()),
            readIndexArtifactReader ?? new RecordingReadIndexArtifactReader(),
            freshnessEvaluator ?? new RecordingReadIndexFreshnessEvaluator(),
            timeProvider ?? TimeProvider.System);
    }

    public static ReadyCommandInput CreateExecutionInput (
        UnityExecutionMode? mode = UnityExecutionMode.Auto,
        bool failFast = false,
        int timeoutMilliseconds = 10000)
    {
        return new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: mode,
            TimeoutMilliseconds: timeoutMilliseconds,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: failFast);
    }

    public static ReadyCommandInput CreateReadIndexInput (
        UnityExecutionMode? mode = null,
        ReadIndexMode? readIndexMode = ReadIndexMode.AllowStale,
        bool isReadIndexModeSpecified = true)
    {
        return new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.ReadIndex,
            Mode: mode,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: readIndexMode,
            IsReadIndexModeSpecified: isReadIndexModeSpecified,
            FailFast: false);
    }

    public static StubModeDecisionService CreateModeDecisionService (
        UnityExecutionMode requestedMode,
        bool daemonRunning,
        UnityExecutionTarget executionTarget)
    {
        return new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
            requestedMode,
            daemonRunning,
            executionTarget,
            TimeSpan.FromSeconds(10))));
    }

    public static UnityRequestExecutionResult CreateReadyPingSuccess (
        UnityEditorLifecycleState lifecycleState = UnityEditorLifecycleState.Ready,
        ProjectFingerprint? projectFingerprint = null)
    {
        return UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(CreateReadyPingResponse(
                lifecycleState,
                projectFingerprint)),
            []));
    }

    public static UnityEditorObservation CreateReadyPingResponse (
        UnityEditorLifecycleState lifecycleState = UnityEditorLifecycleState.Ready,
        ProjectFingerprint? projectFingerprint = null)
    {
        return new UnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: projectFingerprint ?? ProjectContextTestFactory.ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: UnityEditorMode.Batchmode,
                lifecycleState: lifecycleState,
                compileState: lifecycleState == UnityEditorLifecycleState.CompileFailed
                    ? UnityEditorCompileState.Failed
                    : UnityEditorCompileState.Ready,
                generations: new UnityEditorGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 4,
                    PlayModeGeneration: 2),
                playMode: new UnityEditorPlayModeSnapshot(
                    State: UnityEditorPlayModeState.Stopped,
                    Transition: UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            actionRequired: lifecycleState == UnityEditorLifecycleState.CompileFailed
                ? UnityEditorActionRequired.FixCompileErrors
                : null,
            primaryDiagnostic: null);
    }

    public static StartupFailureDetail CreateStartupFailureDetail ()
    {
        return new StartupFailureDetail(
            Startup: new DaemonStartupObservationOutput(
                StartupStatus: DaemonStartupStatus.Blocked,
                StartupBlockingReason: DaemonStartupBlockingReason.Compile,
                LaunchAttemptId: null,
                EditorMode: UnityEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: true,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                ElapsedMilliseconds: null,
                ProcessAction: DaemonStartupProcessAction.Terminated,
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix),
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: DaemonDiagnosisReason.UnityScriptCompilationFailed,
                Message: "Unity startup is blocked.",
                ReportedBy: DaemonDiagnosisReportedBy.Cli,
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:06+00:00"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                UnityLogPath: "/repo/.ucli/local/logs/unity.log",
                StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                ActionRequired: UnityEditorActionRequired.FixCompileErrors,
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
                    Code: "CS0246",
                    File: "Assets/Scripts/Broken.cs",
                    Line: 10,
                    Column: 5,
                    Message: "error CS0246")),
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            SafeToRetryImmediately: false);
    }
}
