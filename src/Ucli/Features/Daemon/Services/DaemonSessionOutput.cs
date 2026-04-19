namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Represents normalized session payload values returned by daemon command workflows. </summary>
/// <param name="ProjectFingerprint"> The project fingerprint associated with daemon session. </param>
/// <param name="IssuedAtUtc"> The UTC timestamp when daemon session was issued. </param>
/// <param name="RuntimeKind"> The daemon runtime kind. </param>
/// <param name="OwnerKind"> The daemon owner kind. </param>
/// <param name="CanShutdownProcess"> Whether daemon session allows process shutdown. </param>
/// <param name="EndpointTransportKind"> The IPC endpoint transport kind literal. </param>
/// <param name="EndpointAddress"> The IPC endpoint address literal. </param>
/// <param name="ProcessId"> The daemon process identifier when available; otherwise <see langword="null" />. </param>
/// <param name="OwnerProcessId"> The owner process identifier when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionOutput (
    string ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    string RuntimeKind,
    string OwnerKind,
    bool CanShutdownProcess,
    string EndpointTransportKind,
    string EndpointAddress,
    int? ProcessId,
    int? OwnerProcessId);