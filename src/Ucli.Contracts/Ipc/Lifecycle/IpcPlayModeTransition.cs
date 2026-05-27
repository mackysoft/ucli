using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines typed Play Mode subsystem transitions used by runtime lifecycle logic. </summary>
public enum IpcPlayModeTransition
{
    /// <summary> No Play Mode transition is active. </summary>
    [UcliContractLiteral("none")]
    None,

    /// <summary> The Editor is entering Play Mode. </summary>
    [UcliContractLiteral("entering")]
    Entering,

    /// <summary> The Editor is exiting Play Mode. </summary>
    [UcliContractLiteral("exiting")]
    Exiting,
}
