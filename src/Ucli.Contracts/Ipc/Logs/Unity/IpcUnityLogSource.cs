using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity log source literals. </summary>
public enum IpcUnityLogSource
{
    /// <summary> Unity compilation log source. </summary>
    [UcliContractLiteral("compile")]
    Compile = 1,

    /// <summary> Unity runtime log source. </summary>
    [UcliContractLiteral("runtime")]
    Runtime = 2,
}
