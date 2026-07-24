using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents one persisted daemon lifecycle observation made outside the IPC endpoint. </summary>
internal sealed record DaemonLifecycleObservation
{
    public DaemonLifecycleObservation (
        int processId,
        DateTimeOffset processStartedAtUtc,
        UnityEditorStateSnapshot state,
        DateTimeOffset observedAtUtc,
        DaemonDiagnosisActionRequired? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic,
        string? serverVersion,
        Guid editorInstanceId,
        DaemonLifecycleRecoveryLease? recoveryLease)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process identifier must be positive.");
        }

        var validatedProcessStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            processStartedAtUtc,
            nameof(processStartedAtUtc));
        var validatedState = state ?? throw new ArgumentNullException(nameof(state));
        var validatedObservedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(observedAtUtc, nameof(observedAtUtc));
        var validatedEditorInstanceId = ContractArgumentGuard.RequireNonEmptyGuid(editorInstanceId, nameof(editorInstanceId));

        if (actionRequired.HasValue && !TextVocabulary.IsDefined(actionRequired.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(actionRequired), actionRequired, "Unsupported daemon diagnosis action.");
        }

        if (recoveryLease is not null
            && (validatedState.LifecycleState != IpcEditorLifecycleState.Recovering
                || recoveryLease.ExpiresAtUtc <= validatedObservedAtUtc))
        {
            throw new ArgumentException(
                "Recovery lease requires a recovering observation and an expiration after its observation timestamp.",
                nameof(recoveryLease));
        }

        ProcessId = processId;
        ProcessStartedAtUtc = validatedProcessStartedAtUtc;
        State = validatedState;
        ObservedAtUtc = validatedObservedAtUtc;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
        ServerVersion = serverVersion;
        EditorInstanceId = validatedEditorInstanceId;
        RecoveryLease = recoveryLease;
    }

    public int ProcessId { get; }

    public DateTimeOffset ProcessStartedAtUtc { get; }

    public UnityEditorStateSnapshot State { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public DaemonDiagnosisActionRequired? ActionRequired { get; }

    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }

    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    public string? ServerVersion { get; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    public Guid EditorInstanceId { get; }

    /// <summary> Gets the bounded domain-reload recovery lease, when the observation was written before reload. </summary>
    public DaemonLifecycleRecoveryLease? RecoveryLease { get; }

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
