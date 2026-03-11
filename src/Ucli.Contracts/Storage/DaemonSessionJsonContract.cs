namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon <c>session.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="SessionToken"> The daemon session token value. </param>
/// <param name="ProjectFingerprint"> The project fingerprint value. </param>
/// <param name="IssuedAtUtc"> The issued-at timestamp. </param>
/// <param name="RuntimeKind"> The daemon runtime kind value. </param>
/// <param name="OwnerKind"> The daemon owner kind value. </param>
/// <param name="CanShutdownProcess"> Whether daemon process shutdown is allowed. </param>
/// <param name="EndpointTransportKind"> The endpoint transport kind value. </param>
/// <param name="EndpointAddress"> The endpoint address value. </param>
/// <param name="ProcessId"> The process identifier value. </param>
/// <param name="OwnerProcessId"> The owner process identifier value. </param>
internal sealed record DaemonSessionJsonContract (
    int SchemaVersion,
    string? SessionToken,
    string? ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    string? RuntimeKind,
    string? OwnerKind,
    bool CanShutdownProcess,
    string? EndpointTransportKind,
    string? EndpointAddress,
    int? ProcessId,
    int? OwnerProcessId);