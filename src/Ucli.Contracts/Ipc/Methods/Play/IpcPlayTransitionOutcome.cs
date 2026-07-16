using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the outcome of one requested Play Mode transition. </summary>
public enum IpcPlayTransitionOutcome
{
    /// <summary> Indicates that this request entered Play Mode. </summary>
    [UcliContractLiteral("entered")]
    Entered = 1,

    /// <summary> Indicates that Play Mode was already active. </summary>
    [UcliContractLiteral("alreadyEntered")]
    AlreadyEntered = 2,

    /// <summary> Indicates that this request exited Play Mode. </summary>
    [UcliContractLiteral("exited")]
    Exited = 3,

    /// <summary> Indicates that Play Mode was already stopped. </summary>
    [UcliContractLiteral("alreadyExited")]
    AlreadyExited = 4,

    /// <summary> Indicates that the requested transition exceeded its deadline. </summary>
    [UcliContractLiteral("timeout")]
    Timeout = 5,

    /// <summary> Indicates that the Editor state blocked the requested transition. </summary>
    [UcliContractLiteral("blocked")]
    Blocked = 6,
}
