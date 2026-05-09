using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents one daemon lifecycle observation persisted outside the IPC endpoint. </summary>
internal sealed record DaemonLifecycleObservation (
    int ProcessId,
    DateTimeOffset ProcessStartedAtUtc,
    string EditorMode,
    string LifecycleState,
    string? BlockingReason,
    string CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    DateTimeOffset ObservedAtUtc,
    string? ActionRequired,
    IpcPrimaryDiagnostic? PrimaryDiagnostic)
{
    /// <summary> Gets a value indicating whether this observation means the same Unity process may recover its endpoint. </summary>
    public bool IsRecovering => string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.Recovering, StringComparison.Ordinal)
        || string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, StringComparison.Ordinal);
}
