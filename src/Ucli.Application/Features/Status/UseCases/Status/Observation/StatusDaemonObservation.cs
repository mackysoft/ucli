using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;

/// <summary> Represents daemon observation values that can be projected into status command payload. </summary>
/// <param name="DaemonStatus"> The daemon status value serialized into command payload. </param>
/// <param name="ServerVersion"> The daemon-side server version when reachable. </param>
/// <param name="LifecycleState"> The daemon-side lifecycle-state when reachable. </param>
/// <param name="BlockingReason"> The daemon-side blocking-reason when reachable. </param>
/// <param name="CompileState"> The daemon compile-state value when reachable. </param>
/// <param name="CompileGeneration"> The daemon compile generation when reachable. </param>
/// <param name="DomainReloadGeneration"> The daemon domain-reload generation when reachable. </param>
/// <param name="CanAcceptExecutionRequests"> Whether the daemon can currently accept execution requests. </param>
/// <param name="EditorMode"> The daemon Editor mode when reachable. </param>
/// <param name="ObservedAtUtc"> The daemon lifecycle observation timestamp when available. </param>
/// <param name="ActionRequired"> The normalized user action required by the lifecycle blocker when available. </param>
/// <param name="PrimaryDiagnostic"> The primary lifecycle diagnostic when available. </param>
/// <param name="PlayMode"> The Play Mode snapshot when daemon ping details are available. </param>
internal sealed record StatusDaemonObservation (
    DaemonStatusKind DaemonStatus,
    string? ServerVersion,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    string? EditorMode,
    DateTimeOffset? ObservedAtUtc = null,
    string? ActionRequired = null,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic = null,
    PlayModeSnapshotOutput? PlayMode = null);
