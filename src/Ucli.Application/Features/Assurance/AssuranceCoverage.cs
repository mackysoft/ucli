using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Defines the finite evidence coverage values emitted for assurance claims. </summary>
internal enum AssuranceCoverage
{
    /// <summary> Evidence covers the complete claim. </summary>
    [UcliContractLiteral("full")]
    Full = 1,

    /// <summary> Evidence covers only part of the claim. </summary>
    [UcliContractLiteral("partial")]
    Partial = 2,

    /// <summary> No evidence coverage is available. </summary>
    [UcliContractLiteral("none")]
    None = 3,
}
