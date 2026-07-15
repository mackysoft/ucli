using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the requested Play Mode transition. </summary>
public enum IpcPlayTransitionCommand
{
    /// <summary> Requests entry into Play Mode. </summary>
    [UcliContractLiteral("enter")]
    Enter = 1,

    /// <summary> Requests exit from Play Mode. </summary>
    [UcliContractLiteral("exit")]
    Exit = 2,
}
