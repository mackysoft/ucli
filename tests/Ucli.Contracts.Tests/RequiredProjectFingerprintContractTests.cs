using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests;

public sealed class RequiredProjectFingerprintContractTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ContractKind.DaemonBootstrap)]
    [InlineData(ContractKind.OneshotBootstrap)]
    [InlineData(ContractKind.CompileStarted)]
    [InlineData(ContractKind.CompileSummary)]
    [InlineData(ContractKind.DaemonLifecycleSnapshot)]
    [InlineData(ContractKind.PingResponse)]
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

        Assert.Equal("ProjectFingerprint", exception.ParamName);
    }

    private static object CreateWithNullProjectFingerprint (ContractKind contractKind)
    {
        return contractKind switch
        {
            ContractKind.DaemonBootstrap => new IpcDaemonBootstrapArguments(
                RepositoryRoot: "/repo",
                ProjectFingerprint: null!,
                SessionPath: "/repo/session.json",
                SessionIssuedAtUtc: Timestamp,
                EndpointTransportKind: "namedPipe",
                EndpointAddress: "ucli-endpoint"),
            ContractKind.OneshotBootstrap => new IpcOneshotBootstrapArguments(
                ParentProcessId: 1234,
                ProjectFingerprint: null!,
                SessionToken: "session-token",
                ExitDeadlineUtc: Timestamp,
                EndpointTransportKind: "namedPipe",
                EndpointAddress: "ucli-endpoint"),
            ContractKind.CompileStarted => new CompileStartedEntry(
                RunId: "compile-run",
                ProjectFingerprint: null!,
                RequestedMode: "auto",
                ResolvedMode: "daemon",
                SessionKind: "existing",
                TimeoutMilliseconds: 1000),
            ContractKind.CompileSummary => new IpcCompileSummary(
                RunId: "compile-run",
                ProjectFingerprint: null!,
                Completed: false,
                StartedAtUtc: Timestamp,
                CompletedAtUtc: null,
                Refresh: null!,
                ScriptCompilation: null!,
                DomainReload: null!,
                Lifecycle: null!),
            ContractKind.DaemonLifecycleSnapshot => new DaemonStartLifecycleSnapshotProgressEntry(
                PayloadKind: "lifecycleSnapshot",
                ProjectFingerprint: null!,
                TimeoutMilliseconds: 1000,
                EditorMode: null,
                OnStartupBlocked: "auto",
                LifecycleState: "ready",
                BlockingReason: null,
                CanAcceptExecutionRequests: true),
            ContractKind.PingResponse => new IpcPingResponse(
                ServerVersion: "1.0.0",
                EditorMode: "batchmode",
                UnityVersion: "6000.1.4f1",
                ProjectFingerprint: null!,
                CompileState: "ready"),
            ContractKind.DaemonStartupObservation => new DaemonStartStartupObservationProgressEntry(
                PayloadKind: "startupObservation",
                ProjectFingerprint: null!,
                TimeoutMilliseconds: 1000,
                EditorMode: null,
                OnStartupBlocked: "auto",
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
                OnStartupBlocked: "auto",
                Result: null,
                StartStatus: null,
                DaemonStatus: null,
                ErrorCode: null),
            ContractKind.GuiSupervisorManifest => new GuiSupervisorManifestJsonContract(
                SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
                SessionToken: "session-token",
                ProjectFingerprint: null!,
                EndpointTransportKind: "namedPipe",
                EndpointAddress: "ucli-endpoint",
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
                RunId: "build-run",
                ProjectFingerprint: null!,
                LifecycleBefore: null!,
                LifecycleAfter: null!,
                DirtyState: null!,
                Input: null!,
                OutputLayout: null,
                UnityBuildProfile: null,
                Report: null,
                Logs: null!,
                ProjectMutation: null!),
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
        PingResponse,
        DaemonStartupObservation,
        DaemonStartProgress,
        GuiSupervisorManifest,
        GuiRebootstrapRequest,
        GuiRebootstrapResponse,
        BuildRunResponse,
    }
}
