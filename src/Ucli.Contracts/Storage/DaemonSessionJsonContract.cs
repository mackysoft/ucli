using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon <c>session.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="SessionGenerationId"> The daemon session generation identifier. </param>
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
/// <param name="EditorInstanceId"> The Unity Editor process instance identifier when available. </param>
internal sealed record DaemonSessionJsonContract (
    int SchemaVersion,
    Guid SessionGenerationId,
    string? SessionToken,
    ProjectFingerprint? ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    DaemonEditorMode? EditorMode,
    DaemonSessionOwnerKind? OwnerKind,
    [property: JsonRequired] bool CanShutdownProcess,
    IpcTransportKind? EndpointTransportKind,
    string? EndpointAddress,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    int? OwnerProcessId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? EditorInstanceId)
{
    /// <inheritdoc />
    public override string ToString () => nameof(DaemonSessionJsonContract);
}
