using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests;

public sealed class RequiredProjectFingerprintContractTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid RunId = Guid.Parse("2df1e10d-6a6f-4e36-86e7-5e94040eb0e6");

    [Theory]
    [InlineData(ContractKind.DaemonBootstrap)]
    [InlineData(ContractKind.OneshotBootstrap)]
    [InlineData(ContractKind.CompileStarted)]
    [InlineData(ContractKind.CompileSummary)]
    [InlineData(ContractKind.DaemonLifecycleSnapshot)]
    [InlineData(ContractKind.UnityEditorObservation)]
    [InlineData(ContractKind.DaemonStartupObservation)]
    [InlineData(ContractKind.DaemonStartProgress)]
    [InlineData(ContractKind.GuiSupervisorManifest)]
    [InlineData(ContractKind.GuiRebootstrapRequest)]
    [InlineData(ContractKind.GuiRebootstrapResponse)]
    [InlineData(ContractKind.BuildRunResponse)]
    [Trait("Size", "Small")]
    public void Constructor_WhenRequiredProjectFingerprintIsNull_ThrowsArgumentNullException (
        ContractKind contractKind)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateWithNullProjectFingerprint(contractKind));

        Assert.Equal(
            contractKind == ContractKind.UnityEditorObservation
                ? "projectFingerprint"
                : "ProjectFingerprint",
            exception.ParamName);
    }

    private static object CreateWithNullProjectFingerprint (ContractKind contractKind)
    {
        return contractKind switch
        {
            ContractKind.DaemonBootstrap => new IpcDaemonBootstrapArguments(
                RepositoryRoot: "/repo",
                ProjectFingerprint: null!,
                SessionPath: "/repo/session.json",
                SessionGenerationId: Guid.NewGuid(),
                SessionIssuedAtUtc: Timestamp,
                Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint")),
            ContractKind.OneshotBootstrap => new IpcOneshotBootstrapEnvelope(
                BootstrapId: Guid.NewGuid(),
                ParentProcessId: 1234,
                ParentProcessStartedAtUtc: Timestamp.AddMinutes(-2),
                ProjectFingerprint: null!,
                SessionToken: IpcSessionToken.CreateRandom(),
                CreatedAtUtc: Timestamp.AddMinutes(-1),
                ExitDeadlineUtc: Timestamp,
                Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint")),
            ContractKind.CompileStarted => new CompileStartedEntry(
                RunId: RunId,
                ProjectFingerprint: null!,
                RequestedMode: AssuranceRequestedExecutionMode.Auto,
                ResolvedMode: AssuranceResolvedExecutionMode.Daemon,
                SessionKind: AssuranceSessionKind.Daemon,
                TimeoutMilliseconds: 1000),
            ContractKind.CompileSummary => new IpcCompileSummary(
                RunId: RunId,
                ProjectFingerprint: null!,
                Completed: false,
                StartedAtUtc: Timestamp,
                CompletedAtUtc: null,
                Refresh: null!,
                ScriptCompilation: null!,
                DomainReload: null!,
                Lifecycle: null!),
            ContractKind.DaemonLifecycleSnapshot => new DaemonStartLifecycleSnapshotProgressEntry(
                PayloadKind: DaemonStartProgressPayloadKind.LifecycleSnapshot,
                ProjectFingerprint: null!,
                TimeoutMilliseconds: 1000,
                EditorMode: null,
                OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                LifecycleState: IpcEditorLifecycleState.Ready,
                BlockingReason: null,
                Generations: new IpcUnityGenerationSnapshot(0, 0, 0, 0),
                CanAcceptExecutionRequests: true),
            ContractKind.UnityEditorObservation => new IpcUnityEditorObservation(
                serverVersion: "1.0.0",
                unityVersion: "6000.1.4f1",
                projectFingerprint: null!,
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: IpcEditorLifecycleState.Ready,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(0, 0, 0, 0),
                    playMode: new IpcPlayModeSnapshot(
                        IpcPlayModeState.Stopped,
                        IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: Timestamp,
                actionRequired: null,
                primaryDiagnostic: null),
            ContractKind.DaemonStartupObservation => new DaemonStartStartupObservationProgressEntry(
                PayloadKind: DaemonStartProgressPayloadKind.StartupObservation,
                ProjectFingerprint: null!,
                TimeoutMilliseconds: 1000,
                EditorMode: null,
                OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                LaunchAttemptId: null,
                OwnerKind: null,
                CanShutdownProcess: null,
                ProcessId: null,
                ProcessStartedAtUtc: null,
                StartupStatus: null,
                StartupBlockingReason: null,
                StartupPhase: null,
                RetryDisposition: null,
                Message: null,
                ErrorCode: null),
            ContractKind.DaemonStartProgress => new DaemonStartProgressEntry(
                ProjectFingerprint: null!,
                TimeoutMilliseconds: 1000,
                EditorMode: null,
                OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                Result: null,
                StartStatus: null,
                DaemonStatus: null,
                ErrorCode: null),
            ContractKind.GuiSupervisorManifest => new GuiSupervisorManifestJsonContract(
                SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
                SessionToken: IpcSessionToken.CreateRandom(),
                ProjectFingerprint: null!,
                Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint"),
                ProcessId: 1234,
                ProcessStartedAtUtc: Timestamp,
                IssuedAtUtc: Timestamp),
            ContractKind.GuiRebootstrapRequest => new IpcGuiRebootstrapRequest(
                ProjectFingerprint: null!,
                ReplaceExistingSession: false),
            ContractKind.GuiRebootstrapResponse => new IpcGuiRebootstrapResponse(
                Accepted: true,
                ProjectFingerprint: null!,
                ProcessId: 1234),
            ContractKind.BuildRunResponse => new IpcBuildRunResponse(
                RunId: RunId,
                ProjectFingerprint: null!,
                LifecycleBefore: null!,
                LifecycleAfter: null!,
                DirtyState: null!,
                Input: null!,
                OutputLayout: null,
                UnityBuildProfile: null,
                Report: null,
                Logs: null!,
                ProjectMutation: null!,
                RunnerResult: null),
            _ => throw new ArgumentOutOfRangeException(nameof(contractKind), contractKind, null),
        };
    }

    public enum ContractKind
    {
        DaemonBootstrap,
        OneshotBootstrap,
        CompileStarted,
        CompileSummary,
        DaemonLifecycleSnapshot,
        UnityEditorObservation,
        DaemonStartupObservation,
        DaemonStartProgress,
        GuiSupervisorManifest,
        GuiRebootstrapRequest,
        GuiRebootstrapResponse,
        BuildRunResponse,
    }
}
