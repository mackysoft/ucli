using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents one daemon lifecycle observation persisted outside the IPC endpoint. </summary>
internal sealed record DaemonLifecycleObservation
{
    /// <summary> Initializes one validated daemon lifecycle observation. </summary>
    /// <param name="editorInstanceId"> The non-empty Unity Editor process instance identifier. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="editorInstanceId" /> is empty. </exception>
    public DaemonLifecycleObservation (
        int processId,
        DateTimeOffset processStartedAtUtc,
        string editorMode,
        string lifecycleState,
        string? blockingReason,
        string compileState,
        string? compileGeneration,
        string? domainReloadGeneration,
        DateTimeOffset observedAtUtc,
        string? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic,
        Guid editorInstanceId)
    {
        if (editorInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
        }

        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        EditorMode = editorMode;
        LifecycleState = lifecycleState;
        BlockingReason = blockingReason;
        CompileState = compileState;
        CompileGeneration = compileGeneration;
        DomainReloadGeneration = domainReloadGeneration;
        ObservedAtUtc = observedAtUtc;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
        EditorInstanceId = editorInstanceId;
    }

    /// <summary> Gets the observed Unity process identifier. </summary>
    public int ProcessId { get; init; }

    /// <summary> Gets the observed Unity process start timestamp. </summary>
    public DateTimeOffset ProcessStartedAtUtc { get; init; }

    /// <summary> Gets the daemon Editor mode identifier. </summary>
    public string EditorMode { get; init; }

    /// <summary> Gets the Editor lifecycle state. </summary>
    public string LifecycleState { get; init; }

    /// <summary> Gets the current blocking reason when one exists. </summary>
    public string? BlockingReason { get; init; }

    /// <summary> Gets the compile state. </summary>
    public string CompileState { get; init; }

    /// <summary> Gets the compile generation when one exists. </summary>
    public string? CompileGeneration { get; init; }

    /// <summary> Gets the domain-reload generation when one exists. </summary>
    public string? DomainReloadGeneration { get; init; }

    /// <summary> Gets the observation timestamp. </summary>
    public DateTimeOffset ObservedAtUtc { get; init; }

    /// <summary> Gets the action required to resolve the lifecycle state when one exists. </summary>
    public string? ActionRequired { get; init; }

    /// <summary> Gets the primary diagnostic when one exists. </summary>
    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; init; }

    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    public string? ServerVersion { get; init; }

    /// <summary> Gets whether the observed daemon accepted normal execution requests at observation time. </summary>
    public bool CanAcceptExecutionRequests { get; init; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    public Guid EditorInstanceId { get; }

    /// <summary> Gets the Play Mode subsystem snapshot captured with the lifecycle observation. </summary>
    public IpcPlayModeSnapshot? PlayMode { get; init; }

    /// <summary> Gets a value indicating whether this observation means the same Unity process may recover its endpoint. </summary>
    public bool IsRecovering => string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.Recovering, StringComparison.Ordinal)
        || string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, StringComparison.Ordinal);
}
