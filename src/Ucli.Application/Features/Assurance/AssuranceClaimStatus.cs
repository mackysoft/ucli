
namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Defines the finite status values emitted for assurance claims. </summary>
[VocabularyDefinition]
internal enum AssuranceClaimStatus
{
    /// <summary> The claim was verified. </summary>
    [VocabularyText("passed")]
    Passed = 1,

    /// <summary> The claim was checked and did not hold. </summary>
    [VocabularyText("failed")]
    Failed = 2,

    /// <summary> The available evidence could not establish a terminal result. </summary>
    [VocabularyText("indeterminate")]
    Indeterminate = 3,

    /// <summary> The claim was required but no verification was performed. </summary>
    [VocabularyText("unverified")]
    Unverified = 4,

    /// <summary> The claim does not apply to the observed operation. </summary>
    [VocabularyText("outOfScope")]
    OutOfScope = 5,
}
