using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines build assurance claim status literals. </summary>
internal enum BuildClaimStatus
{
    /// <summary> The claim was verified. </summary>
    [UcliContractLiteral("passed")]
    Passed = 0,

    /// <summary> The claim was checked and did not hold. </summary>
    [UcliContractLiteral("failed")]
    Failed = 1,

    /// <summary> The claim could not be evaluated to a terminal result. </summary>
    [UcliContractLiteral("indeterminate")]
    Indeterminate = 2,
}
