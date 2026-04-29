namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported IPC transport kinds. </summary>
public enum IpcTransportKind
{
    /// <summary> Uses Windows named pipe transport. </summary>
    NamedPipe = 0,

    /// <summary> Uses Unix domain socket transport. </summary>
    UnixDomainSocket = 1,
}
