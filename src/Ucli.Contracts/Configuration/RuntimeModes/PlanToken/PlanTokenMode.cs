using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines plan-token requirements configured in <c>.ucli/config.json</c>. </summary>
public enum PlanTokenMode
{
    /// <summary> Allows command execution with or without a plan token. </summary>
    [UcliContractLiteral("optional")]
    Optional = 0,

    /// <summary> Requires command execution to include a plan token. </summary>
    [UcliContractLiteral("required")]
    Required = 1,
}
