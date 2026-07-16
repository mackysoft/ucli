using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines log query target literals. </summary>
public enum IpcLogQueryTarget
{
    /// <summary> Searches the normalized message. </summary>
    [UcliContractLiteral("message")]
    Message = 1,

    /// <summary> Searches stack-trace or raw detail text. </summary>
    [UcliContractLiteral("stack")]
    Stack = 2,

    /// <summary> Searches both message and secondary detail text. </summary>
    [UcliContractLiteral("both")]
    Both = 3,
}
