using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Defines the finite status values emitted for assurance claims. </summary>
internal enum AssuranceClaimStatus
{
    /// <summary> The claim was verified. </summary>
    [UcliContractLiteral("passed")]
    Passed = 1,

    /// <summary> The claim was checked and did not hold. </summary>
    [UcliContractLiteral("failed")]
    Failed = 2,

    /// <summary> The available evidence could not establish a terminal result. </summary>
    [UcliContractLiteral("indeterminate")]
    Indeterminate = 3,

    /// <summary> The claim was required but no verification was performed. </summary>
    [UcliContractLiteral("unverified")]
    Unverified = 4,

    /// <summary> The claim does not apply to the observed operation. </summary>
    [UcliContractLiteral("outOfScope")]
    OutOfScope = 5,
}
