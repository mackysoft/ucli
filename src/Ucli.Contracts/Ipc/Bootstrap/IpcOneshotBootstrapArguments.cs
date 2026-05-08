namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity oneshot bootstrap argument payload. </summary>
/// <param name="ParentProcessId"> The originating CLI process identifier used for parent-liveness monitoring. </param>
/// <param name="SessionToken"> The dedicated oneshot session token accepted by the temporary IPC host. </param>
/// <param name="ExitDeadlineUtc"> The absolute UTC deadline after which the oneshot host must exit itself. </param>
/// <param name="EndpointTransportKind"> The transport kind literal used by the oneshot IPC server endpoint. </param>
/// <param name="EndpointAddress"> The endpoint address used by the oneshot IPC server. </param>
public sealed record IpcOneshotBootstrapArguments (
    int ParentProcessId,
    string SessionToken,
    DateTimeOffset ExitDeadlineUtc,
    string EndpointTransportKind,
    string EndpointAddress)
    : IpcBatchmodeBootstrapArguments;
