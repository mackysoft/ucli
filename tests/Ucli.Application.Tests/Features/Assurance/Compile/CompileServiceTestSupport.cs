using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

internal static class CompileServiceTestSupport
{
    public static readonly Guid RunId = Guid.Parse("0b143533-fbc2-41ee-bc33-08d80b4fc359");
    public static readonly Guid OtherRunId = Guid.Parse("5d948e1f-d4cd-4357-9f79-eb86604cd355");

    public static CompileService CreateService (
        IProjectContextResolver? projectContextResolver = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IUnityRequestExecutor? unityRequestExecutor = null,
        IGuidGenerator? runIdGenerator = null,
        ICompileRunArtifactStore? artifactStore = null,
        TimeProvider? timeProvider = null)
    {
        return new CompileService(
            projectContextResolver ?? new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create())),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor ?? new RecordingUnityRequestExecutor(CreateCompileResponseResult(CreateSummary())),
            runIdGenerator ?? new StaticGuidGenerator(RunId),
            artifactStore ?? new StubCompileRunArtifactStore(),
            timeProvider ?? TimeProvider.System);
    }

    public static UnityRequestExecutionResult CreateCompileResponseResult (IpcCompileSummary summary)
    {
        return UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(new IpcCompileResponse(summary)),
            []));
    }

    public static IpcCompileSummary CreateSummary (
        Guid? runId = null,
        ProjectFingerprint? projectFingerprint = null,
        int errorCount = 0)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new IpcPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new IpcCompileSummary(
            RunId: runId ?? RunId,
            ProjectFingerprint: projectFingerprint ?? ProjectContextTestFactory.ProjectFingerprint,
            Completed: true,
            StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
            Refresh: new IpcCompileSummary.RefreshEvidence(
                Origin: CompileRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                Completed: true),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                State: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: canAcceptExecutionRequests
                        ? IpcEditorLifecycleState.Ready
                        : IpcEditorLifecycleState.CompileFailed,
                    compileState: canAcceptExecutionRequests
                        ? IpcCompileState.Ready
                        : IpcCompileState.Failed,
                    generations: new IpcUnityGenerationSnapshot(
                        CompileGeneration: 14,
                        DomainReloadGeneration: 7,
                        AssetRefreshGeneration: 3,
                        PlayModeGeneration: 2),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:03Z"),
                ActionRequired: canAcceptExecutionRequests ? null : DaemonDiagnosisActionRequired.FixCompileErrors,
                PrimaryDiagnostic: primaryDiagnostic));
    }

    public static StartupFailureDetail CreateCompilerStartupFailure ()
    {
        return CreateStartupFailure(
            DaemonDiagnosisReason.UnityScriptCompilationFailed,
            new DaemonPrimaryDiagnosticOutput(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                Code: "CS0246",
                File: "Assets/Broken.cs",
                Line: 10,
                Column: 5,
                Message: "MissingType could not be found."));
    }

    public static StartupFailureDetail CreateStartupFailure (
        DaemonDiagnosisReason reason,
        DaemonPrimaryDiagnosticOutput? primaryDiagnostic)
    {
        return new StartupFailureDetail(
            Startup: new DaemonStartupObservationOutput(
                StartupStatus: DaemonStartupStatus.Blocked,
                StartupBlockingReason: reason == DaemonDiagnosisReason.UnityScriptCompilationFailed
                    ? DaemonStartupBlockingReason.Compile
                    : DaemonStartupBlockingReason.PackageResolution,
                LaunchAttemptId: null,
                EditorMode: DaemonEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: null,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                ElapsedMilliseconds: 1200,
                ProcessAction: DaemonStartupProcessAction.Unknown,
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: DaemonStartupRetryDisposition.ManualActionRequired),
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: reason,
                Message: reason == DaemonDiagnosisReason.UnityScriptCompilationFailed
                    ? "Unity script compilation failed."
                    : "Unity package resolution failed.",
                ReportedBy: DaemonDiagnosisReportedBy.Cli,
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                UnityLogPath: "/workspace/UnityProject/Logs/Editor.log",
                StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                ActionRequired: reason == DaemonDiagnosisReason.UnityScriptCompilationFailed
                    ? DaemonDiagnosisActionRequired.FixCompileErrors
                    : DaemonDiagnosisActionRequired.ResolvePackages,
                PrimaryDiagnostic: primaryDiagnostic),
            RetryDisposition: DaemonStartupRetryDisposition.ManualActionRequired,
            SafeToRetryImmediately: false);
    }
}
