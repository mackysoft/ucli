using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines typed Play Mode subsystem states used by runtime lifecycle logic. </summary>
public enum IpcPlayModeState
{
    /// <summary> Play Mode is inactive and no transition is pending. </summary>
    [UcliContractLiteral("stopped")]
    Stopped,

    /// <summary> The Editor is entering Play Mode. </summary>
    [UcliContractLiteral("entering")]
    Entering,

    /// <summary> Play Mode is active. </summary>
    [UcliContractLiteral("playing")]
    Playing,

    /// <summary> The Editor is exiting Play Mode. </summary>
    [UcliContractLiteral("exiting")]
    Exiting,

    /// <summary> Play Mode state could not be classified. </summary>
    [UcliContractLiteral("unknown")]
    Unknown,
}
