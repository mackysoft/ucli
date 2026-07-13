using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

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
        string lifecycleState = "ready",
        bool canAcceptExecutionRequests = true,
        string projectFingerprint = "project-fingerprint")
    {
        return UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(CreateReadyPingResponse(
                lifecycleState,
                canAcceptExecutionRequests,
                projectFingerprint)),
            [],
            HasFailureStatus: false));
    }

    public static IpcPingResponse CreateReadyPingResponse (
        string lifecycleState = "ready",
        bool canAcceptExecutionRequests = true,
        string projectFingerprint = "project-fingerprint")
    {
        return new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: projectFingerprint,
            CompileState: "ready",
            LifecycleState: lifecycleState,
            BlockingReason: lifecycleState == "ready" ? null : "compileFailed",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            PlayMode: new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "2"));
    }

    public static StartupFailureDetail CreateStartupFailureDetail ()
    {
        return new StartupFailureDetail(
            Startup: new DaemonStartupObservationOutput(
                StartupStatus: "blocked",
                StartupBlockingReason: "compile",
                LaunchAttemptId: null,
                EditorMode: "batchmode",
                OwnerKind: "cli",
                CanShutdownProcess: true,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                ElapsedMilliseconds: null,
                ProcessAction: "terminated",
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: "retryAfterFix"),
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: "unityScriptCompilationFailed",
                Message: "Unity startup is blocked.",
                ReportedBy: "cli",
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:06+00:00"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                UnityLogPath: "/repo/.ucli/local/logs/unity.log",
                StartupPhase: "scriptCompilation",
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: "compiler",
                    Code: "CS0246",
                    File: "Assets/Scripts/Broken.cs",
                    Line: 10,
                    Column: 5,
                    Message: "error CS0246")),
            RetryDisposition: "retryAfterFix",
            SafeToRetryImmediately: false);
    }
}
