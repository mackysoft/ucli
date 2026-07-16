using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon lifecycle observation contract fields. </summary>
internal sealed record DaemonLifecycleJsonContract
{
    /// <summary> Initializes persisted daemon lifecycle observation contract fields. </summary>
    [JsonConstructor]
    public DaemonLifecycleJsonContract (
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        UnityEditorStateSnapshot state,
        DateTimeOffset? observedAtUtc,
        DaemonDiagnosisActionRequired? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic,
        Guid sidecarGenerationId,
        string? serverVersion,
        Guid? editorInstanceId,
        DaemonLifecycleRecoveryLease? recoveryLease)
    {
        var validatedState = state ?? throw new ArgumentNullException(nameof(state));

        if (processId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process identifier must be positive when specified.");
        }

        if (processStartedAtUtc.HasValue)
        {
            ContractArgumentGuard.RequireUtcTimestamp(processStartedAtUtc.Value, nameof(processStartedAtUtc));
        }

        if (observedAtUtc.HasValue)
        {
            ContractArgumentGuard.RequireUtcTimestamp(observedAtUtc.Value, nameof(observedAtUtc));
        }

        if (editorInstanceId.HasValue)
        {
            ContractArgumentGuard.RequireNonEmptyGuid(editorInstanceId.Value, nameof(editorInstanceId));
        }

        if (sidecarGenerationId == Guid.Empty)
        {
            throw new ArgumentException("Sidecar generation identifier must not be empty.", nameof(sidecarGenerationId));
        }

        if (actionRequired.HasValue && !ContractLiteralCodec.IsDefined(actionRequired.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(actionRequired), actionRequired, "Unsupported daemon diagnosis action.");
        }

        if (recoveryLease is not null
            && (validatedState.LifecycleState != IpcEditorLifecycleState.Recovering
                || observedAtUtc is not DateTimeOffset observationTimestamp
                || recoveryLease.ExpiresAtUtc <= observationTimestamp))
        {
            throw new ArgumentException(
                "Recovery lease requires a recovering observation and an expiration after its observation timestamp.",
                nameof(recoveryLease));
        }

        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        State = validatedState;
        ObservedAtUtc = observedAtUtc;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
        SidecarGenerationId = sidecarGenerationId;
        ServerVersion = serverVersion;
        EditorInstanceId = editorInstanceId;
        RecoveryLease = recoveryLease;
    }

    /// <summary> Gets the observed Unity process identifier. </summary>
    public int? ProcessId { get; }

    /// <summary> Gets the observed Unity process start timestamp. </summary>
    public DateTimeOffset? ProcessStartedAtUtc { get; }

    /// <summary> Gets the comparable Unity Editor state. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorStateSnapshot State { get; private init; }

    /// <summary> Gets the UTC timestamp when lifecycle values were observed. </summary>
    public DateTimeOffset? ObservedAtUtc { get; }

    /// <summary> Gets the normalized action required to resolve the current lifecycle state. </summary>
    public DaemonDiagnosisActionRequired? ActionRequired { get; }

    /// <summary> Gets the primary machine-readable diagnostic for the current lifecycle state. </summary>
    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }

    /// <summary> Gets the lifecycle sidecar writer generation that owns this persisted observation. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid SidecarGenerationId { get; private init; }

    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServerVersion { get; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? EditorInstanceId { get; }

    /// <summary> Gets the bounded domain-reload recovery lease, when this observation was written before reload. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DaemonLifecycleRecoveryLease? RecoveryLease { get; }
}
