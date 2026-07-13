using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized session payload values returned by daemon command workflows. </summary>
/// <param name="ProjectFingerprint"> The project fingerprint associated with daemon session. </param>
/// <param name="IssuedAtUtc"> The UTC timestamp when daemon session was issued. </param>
/// <param name="EditorMode"> The daemon Editor mode. </param>
/// <param name="OwnerKind"> The daemon owner kind. </param>
/// <param name="CanShutdownProcess"> Whether daemon session allows process shutdown. </param>
/// <param name="EndpointTransportKind"> The IPC endpoint transport kind literal. </param>
/// <param name="EndpointAddress"> The IPC endpoint address literal. </param>
/// <param name="ProcessId"> The daemon process identifier when available; otherwise <see langword="null" />. </param>
/// <param name="ProcessStartedAtUtc"> The daemon process start timestamp when available; otherwise <see langword="null" />. </param>
/// <param name="OwnerProcessId"> The owner process identifier when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionOutput (
    ProjectFingerprint ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    DaemonEditorMode EditorMode,
    DaemonSessionOwnerKind OwnerKind,
    bool CanShutdownProcess,
    IpcTransportKind EndpointTransportKind,
    string EndpointAddress,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    int? OwnerProcessId);
