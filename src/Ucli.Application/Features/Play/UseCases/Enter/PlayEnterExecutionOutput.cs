using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Represents normalized output payload values for one Play Mode enter command execution. </summary>
internal sealed record PlayEnterExecutionOutput (
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
    IpcPlayTransitionResult Transition,
    int TimeoutMilliseconds);
