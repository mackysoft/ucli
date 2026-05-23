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
    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    public string? ServerVersion { get; init; }

    /// <summary> Gets whether the observed daemon accepted normal execution requests at observation time. </summary>
    public bool CanAcceptExecutionRequests { get; init; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    public string? EditorInstanceId { get; init; }

    /// <summary> Gets the Play Mode subsystem snapshot captured with the lifecycle observation. </summary>
    public IpcPlayModeSnapshot? PlayMode { get; init; }

    /// <summary> Gets a value indicating whether this observation means the same Unity process may recover its endpoint. </summary>
    public bool IsRecovering => string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.Recovering, StringComparison.Ordinal)
        || string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, StringComparison.Ordinal);
}
