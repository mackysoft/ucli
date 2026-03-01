namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one resolved IPC endpoint for Unity daemon communication. </summary>
/// <param name="TransportKind"> The transport kind used to connect the endpoint. </param>
/// <param name="Address"> The transport-specific address value. </param>
public sealed record IpcEndpoint (
    IpcTransportKind TransportKind,
    string Address);