using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

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
        string? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic,
        string? serverVersion = null,
        string? editorInstanceId = null)
    {
        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        State = state ?? throw new ArgumentNullException(nameof(state));
        ObservedAtUtc = observedAtUtc;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
        ServerVersion = serverVersion;
        EditorInstanceId = editorInstanceId;
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
    public string? ActionRequired { get; }

    /// <summary> Gets the primary machine-readable diagnostic for the current lifecycle state. </summary>
    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }

    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServerVersion { get; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EditorInstanceId { get; }
}
