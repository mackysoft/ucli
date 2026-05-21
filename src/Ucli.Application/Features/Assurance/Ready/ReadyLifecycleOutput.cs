using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents normalized Unity lifecycle evidence emitted by ready. </summary>
internal sealed record ReadyLifecycleOutput (
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
    ReadyPrimaryDiagnosticOutput? PrimaryDiagnostic,
    PlayModeSnapshotOutput? PlayMode = null);
