using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents one persisted daemon lifecycle observation made outside the IPC endpoint. </summary>
internal sealed record DaemonLifecycleObservation
{
    public DaemonLifecycleObservation (
        int processId,
        DateTimeOffset processStartedAtUtc,
        UnityEditorStateSnapshot state,
        DateTimeOffset observedAtUtc,
        string? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic,
        string? serverVersion,
        string? editorInstanceId)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process identifier must be positive.");
        }

        if (processStartedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(processStartedAtUtc), processStartedAtUtc, "Process start timestamp must be specified.");
        }

        if (observedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(observedAtUtc), observedAtUtc, "Observation timestamp must be specified.");
        }

        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        State = state ?? throw new ArgumentNullException(nameof(state));
        ObservedAtUtc = observedAtUtc;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
        ServerVersion = serverVersion;
        EditorInstanceId = editorInstanceId;
    }

    public int ProcessId { get; }

    public DateTimeOffset ProcessStartedAtUtc { get; }

    public UnityEditorStateSnapshot State { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public string? ActionRequired { get; }

    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }

    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    public string? ServerVersion { get; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    public string? EditorInstanceId { get; }

    /// <summary> Gets the blocking reason required by the observed lifecycle state. </summary>
    public IpcEditorBlockingReason? BlockingReason =>
        IpcEditorLifecycleSemantics.ResolveBlockingReason(State.LifecycleState);

    /// <summary> Gets whether the observed lifecycle state permits normal execution requests. </summary>
    public bool CanAcceptExecutionRequests =>
        IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(State.LifecycleState);

    /// <summary> Gets a value indicating whether this observation means the same Unity process may recover its endpoint. </summary>
    public bool IsRecovering => State.LifecycleState is IpcEditorLifecycleState.Recovering
        or IpcEditorLifecycleState.DomainReloading;
}
