using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines IPC response framing mode literals. </summary>
public enum IpcResponseMode
{
    /// <summary> Single terminal response mode. </summary>
    [UcliContractLiteral("single")]
    Single = 0,

    /// <summary> Progress-frame stream followed by one terminal response mode. </summary>
    [UcliContractLiteral("stream")]
    Stream = 1,
}
