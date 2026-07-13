using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon <c>session.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="SessionToken"> The daemon session token value. </param>
/// <param name="ProjectFingerprint"> The project fingerprint value. </param>
/// <param name="IssuedAtUtc"> The issued-at timestamp. </param>
/// <param name="EditorMode"> The daemon Editor mode value. </param>
/// <param name="OwnerKind"> The daemon owner kind value. </param>
/// <param name="CanShutdownProcess"> Whether daemon process shutdown is allowed. </param>
/// <param name="EndpointTransportKind"> The endpoint transport kind value. </param>
/// <param name="EndpointAddress"> The endpoint address value. </param>
/// <param name="ProcessId"> The process identifier value. </param>
/// <param name="ProcessStartedAtUtc"> The observed process start timestamp in UTC when available. </param>
/// <param name="OwnerProcessId"> The owner process identifier value. </param>
internal sealed record DaemonSessionJsonContract (
    int SchemaVersion,
    string? SessionToken,
    string? ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    DaemonEditorMode? EditorMode,
    DaemonSessionOwnerKind? OwnerKind,
    bool CanShutdownProcess,
    IpcTransportKind? EndpointTransportKind,
    string? EndpointAddress,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    int? OwnerProcessId)
{
    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EditorInstanceId { get; init; }
}
