using System.Text.Json;

namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Validates command-specific invariants that apply to one assurance claim. </summary>
internal interface IAssuranceClaimInvariantRule
{
    /// <summary> Validates one claim while the common validator reads the payload. </summary>
    /// <param name="payload"> The assurance payload root. </param>
    /// <param name="claimElement"> The claim JSON element. </param>
    /// <param name="claimPath"> The JSON path for <paramref name="claimElement" />. </param>
    /// <param name="claimId"> The claim identifier already read by the common validator. </param>
    /// <param name="violations"> The violation sink shared with the common validator. </param>
    void ValidateClaim (
        JsonElement payload,
        JsonElement claimElement,
        string claimPath,
        UcliCode claimId,
        List<AssuranceSemanticInvariantViolation> violations);
}
