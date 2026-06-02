using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines canonical result literals used by command progress entries. </summary>
public enum CommandProgressResult
{
    /// <summary> The observed progress step succeeded. </summary>
    [UcliContractLiteral("succeeded")]
    Succeeded = 0,

    /// <summary> The observed progress step failed. </summary>
    [UcliContractLiteral("failed")]
    Failed = 1,
}
