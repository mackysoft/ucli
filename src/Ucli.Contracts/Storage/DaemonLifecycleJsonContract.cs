using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon lifecycle observation contract fields. </summary>
/// <param name="ProcessId"> The observed Unity process identifier. </param>
/// <param name="ProcessStartedAtUtc"> The observed Unity process start timestamp. </param>
/// <param name="EditorMode"> The daemon Editor mode identifier. </param>
/// <param name="LifecycleState"> The editor lifecycle-state value. </param>
/// <param name="BlockingReason"> The editor blocking-reason value. </param>
/// <param name="CompileState"> The compile-state value. </param>
/// <param name="CompileGeneration"> The opaque compile generation. </param>
/// <param name="DomainReloadGeneration"> The opaque domain-reload generation. </param>
/// <param name="ObservedAtUtc"> The UTC timestamp when lifecycle values were observed. </param>
/// <param name="ActionRequired"> The normalized action required to resolve the current lifecycle state. </param>
/// <param name="PrimaryDiagnostic"> The primary machine-readable diagnostic for the current lifecycle state. </param>
internal sealed record DaemonLifecycleJsonContract (
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string? EditorMode,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    IpcPrimaryDiagnostic? PrimaryDiagnostic)
{
    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EditorInstanceId { get; init; }
}
