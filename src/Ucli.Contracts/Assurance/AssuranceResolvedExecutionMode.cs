using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the finite Unity execution modes resolved by assurance commands. </summary>
public enum AssuranceResolvedExecutionMode
{
    /// <summary> The command executed through a reusable Unity daemon. </summary>
    [UcliContractLiteral("daemon")]
    Daemon = 1,

    /// <summary> The command executed through a one-shot Unity process. </summary>
    [UcliContractLiteral("oneshot")]
    Oneshot = 2,

    /// <summary> The command did not require a live Unity process. </summary>
    [UcliContractLiteral("notApplicable")]
    NotApplicable = 3,
}
