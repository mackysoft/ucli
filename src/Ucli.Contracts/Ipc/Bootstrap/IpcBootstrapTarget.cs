using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the Unity process bootstrap target. </summary>
public enum IpcBootstrapTarget
{
    /// <summary> Starts the long-lived daemon host. </summary>
    [UcliContractLiteral("daemon")]
    Daemon = 1,

    /// <summary> Starts a transient one-shot host. </summary>
    [UcliContractLiteral("oneshot")]
    Oneshot = 2,
}
