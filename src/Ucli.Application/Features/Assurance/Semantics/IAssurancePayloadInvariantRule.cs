using System.Text.Json;

namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Validates command-specific invariants that apply to an assurance payload as a whole. </summary>
internal interface IAssurancePayloadInvariantRule
{
    /// <summary> Validates command-specific payload-level invariants. </summary>
    /// <param name="payload"> The assurance payload root. </param>
    /// <param name="violations"> The violation sink shared with the common validator. </param>
    void ValidatePayload (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations);
}
