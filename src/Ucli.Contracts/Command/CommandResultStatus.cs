using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines the closed set of top-level CLI command result outcomes. </summary>
public enum CommandResultStatus
{
    /// <summary> Indicates that the command completed successfully. </summary>
    [UcliContractLiteral("ok")]
    Ok = 1,

    /// <summary> Indicates that the command failed. </summary>
    [UcliContractLiteral("error")]
    Error = 2,
}
