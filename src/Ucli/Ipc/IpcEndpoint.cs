namespace MackySoft.Ucli.Ipc;

/// <summary> Represents one resolved IPC endpoint for Unity daemon communication. </summary>
/// <param name="TransportKind"> The transport kind used to connect the endpoint. </param>
/// <param name="Address"> The transport-specific address value. </param>
internal sealed record IpcEndpoint (
    IpcTransportKind TransportKind,
    string Address);
