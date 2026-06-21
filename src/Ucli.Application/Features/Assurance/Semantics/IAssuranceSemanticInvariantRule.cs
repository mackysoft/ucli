using System.Text.Json;

namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Validates command-specific semantic invariants inside a common assurance payload. </summary>
internal interface IAssuranceSemanticInvariantRule
{
    /// <summary> Validates command-specific payload-level invariants. </summary>
    /// <param name="payload"> The assurance payload root. </param>
    /// <param name="violations"> The violation sink shared with the common validator. </param>
    void ValidatePayload (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations);

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
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations);
}
