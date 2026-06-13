using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines build assurance verdict literals. </summary>
internal enum BuildVerdict
{
    /// <summary> Required claims passed. </summary>
    [UcliContractLiteral("pass")]
    Pass = 0,

    /// <summary> At least one required claim failed. </summary>
    [UcliContractLiteral("fail")]
    Fail = 1,

    /// <summary> Required claims did not reach terminal evidence. </summary>
    [UcliContractLiteral("incomplete")]
    Incomplete = 2,
}
