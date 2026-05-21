using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Status;

/// <summary> Represents normalized output payload values for one Play Mode status command execution. </summary>
/// <param name="Project"> The resolved Unity project identity. </param>
/// <param name="DaemonStatus"> The daemon status value. </param>
/// <param name="ServerVersion"> The daemon-side server version. </param>
/// <param name="EditorMode"> The daemon Editor mode. </param>
/// <param name="LifecycleState"> The daemon-side lifecycle-state. </param>
/// <param name="BlockingReason"> The daemon-side blocking-reason. </param>
/// <param name="CompileState"> The daemon compile-state value. </param>
/// <param name="CompileGeneration"> The daemon compile generation. </param>
/// <param name="DomainReloadGeneration"> The daemon domain-reload generation. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
/// <param name="ObservedAtUtc"> The daemon lifecycle observation timestamp when available. </param>
/// <param name="ActionRequired"> The normalized user action required by the lifecycle blocker when available. </param>
/// <param name="PrimaryDiagnostic"> The primary lifecycle diagnostic when available. </param>
/// <param name="PlayMode"> The Play Mode snapshot. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for the status IPC request. </param>
internal sealed record PlayStatusExecutionOutput (
    ProjectIdentityInfo Project,
    DaemonStatusKind DaemonStatus,
    string? ServerVersion,
    string EditorMode,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    PlayModeSnapshotOutput PlayMode,
    int TimeoutMilliseconds);
