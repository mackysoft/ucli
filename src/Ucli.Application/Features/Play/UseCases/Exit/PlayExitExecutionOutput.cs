using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Represents normalized output payload values for one Play Mode exit command execution. </summary>
internal sealed record PlayExitExecutionOutput (
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
    PlayExitTransitionOutput Transition,
    int TimeoutMilliseconds);
