using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported IPC transport kinds. </summary>
public enum IpcTransportKind
{
    /// <summary> Uses Windows named pipe transport. </summary>
    [UcliContractLiteral("namedPipe")]
    NamedPipe = 0,

    /// <summary> Uses Unix domain socket transport. </summary>
    [UcliContractLiteral("unixDomainSocket")]
    UnixDomainSocket = 1,
}
