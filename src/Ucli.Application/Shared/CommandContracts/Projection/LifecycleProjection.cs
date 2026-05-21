using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.CommandContracts.Projection;

/// <summary> Represents normalized lifecycle values projected from one lifecycle-bearing IPC response. </summary>
internal sealed record LifecycleProjection (
    string? ServerVersion,
    string? UnityVersion,
    string? EditorMode,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    IpcPrimaryDiagnostic? PrimaryDiagnostic,
    PlayModeSnapshotOutput? PlayMode);
