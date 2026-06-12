using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines build assurance coverage literals. </summary>
internal enum BuildCoverage
{
    /// <summary> The claim was fully covered by evidence. </summary>
    [UcliContractLiteral("full")]
    Full = 0,

    /// <summary> The claim has no evidence coverage. </summary>
    [UcliContractLiteral("none")]
    None = 1,
}
