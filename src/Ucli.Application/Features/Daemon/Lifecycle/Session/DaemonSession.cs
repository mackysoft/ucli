using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents persisted daemon session metadata bound to one project fingerprint. </summary>
/// <param name="SchemaVersion"> The schema version used for JSON persistence compatibility. </param>
/// <param name="SessionToken"> The daemon session token used for IPC authorization. </param>
/// <param name="ProjectFingerprint"> The project fingerprint associated with this daemon session. </param>
/// <param name="IssuedAtUtc"> The UTC timestamp when this session was issued. </param>
/// <param name="EditorMode"> The daemon Editor mode. </param>
/// <param name="OwnerKind"> The daemon owner kind. </param>
/// <param name="CanShutdownProcess"> Whether daemon management is allowed to shutdown the process. </param>
/// <param name="EndpointTransportKind"> The transport kind string used by daemon endpoint. </param>
/// <param name="EndpointAddress"> The endpoint address string used by daemon endpoint. </param>
/// <param name="ProcessId"> The daemon process identifier when available. </param>
/// <param name="ProcessStartedAtUtc"> The daemon process start timestamp when available. </param>
/// <param name="OwnerProcessId"> The owner process identifier when available. </param>
internal sealed record DaemonSession (
    int SchemaVersion,
    string SessionToken,
    string ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    string EditorMode,
    string OwnerKind,
    bool CanShutdownProcess,
    string EndpointTransportKind,
    string EndpointAddress,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    int? OwnerProcessId)
{
    /// <summary> Gets the schema version used by daemon session persistence. </summary>
    public const int CurrentSchemaVersion = DaemonSessionStorageContract.CurrentSchemaVersion;

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    public string? EditorInstanceId { get; init; }
}
