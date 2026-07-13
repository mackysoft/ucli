using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents one daemon lifecycle observation persisted outside the IPC endpoint. </summary>
internal sealed record DaemonLifecycleObservation (
    int ProcessId,
    DateTimeOffset ProcessStartedAtUtc,
    string EditorMode,
    IpcEditorLifecycleState LifecycleState,
    IpcCompileState CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    DateTimeOffset ObservedAtUtc,
    string? ActionRequired,
    IpcPrimaryDiagnostic? PrimaryDiagnostic)
{
    /// <summary> Gets the blocking reason required by <see cref="LifecycleState" />. </summary>
    public IpcEditorBlockingReason? BlockingReason =>
        IpcEditorLifecycleSemantics.ResolveBlockingReason(LifecycleState);

    /// <summary> Gets whether <see cref="LifecycleState" /> permits normal execution requests. </summary>
    public bool CanAcceptExecutionRequests =>
        IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(LifecycleState);

    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    public string? ServerVersion { get; init; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    public string? EditorInstanceId { get; init; }

    /// <summary> Gets the Play Mode subsystem snapshot captured with the lifecycle observation. </summary>
    public IpcPlayModeSnapshot? PlayMode { get; init; }

    /// <summary> Gets a value indicating whether this observation means the same Unity process may recover its endpoint. </summary>
    public bool IsRecovering => LifecycleState is IpcEditorLifecycleState.Recovering
        or IpcEditorLifecycleState.DomainReloading;
}
