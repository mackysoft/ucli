using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines supported daemon session owner kinds. </summary>
public enum DaemonSessionOwnerKind
{
    /// <summary> The daemon session is owned by uCLI. </summary>
    [UcliContractLiteral("cli")]
    Cli = 0,

    /// <summary> The daemon session is owned by the user-controlled Unity Editor process. </summary>
    [UcliContractLiteral("user")]
    User = 1,
}
