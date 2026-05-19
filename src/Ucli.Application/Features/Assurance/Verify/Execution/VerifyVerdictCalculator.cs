using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;

/// <summary> Calculates the final verify verdict from claims and payload-level residual risks. </summary>
internal static class VerifyVerdictCalculator
{
    /// <summary> Calculates the final verify verdict. </summary>
    /// <param name="claims"> The claims emitted by executed verifiers. </param>
    /// <param name="payloadResidualRisks"> The payload-level residual risks emitted by executed verifiers. </param>
    /// <returns> The final verify verdict literal. </returns>
    public static string Calculate (
        IReadOnlyList<VerifyClaimOutput> claims,
        IReadOnlyList<VerifyResidualRiskOutput> payloadResidualRisks)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(payloadResidualRisks);

        var claimStates = new AssuranceVerdictClaimState[claims.Count];
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            claimStates[i] = new AssuranceVerdictClaimState(
                claim.Status,
                claim.Coverage,
                claim.Required,
                claim.ResidualRisks.Any(static risk => risk.Blocking));
        }

        var residualRiskStates = new AssuranceVerdictResidualRiskState[payloadResidualRisks.Count];
        for (var i = 0; i < payloadResidualRisks.Count; i++)
        {
            residualRiskStates[i] = new AssuranceVerdictResidualRiskState(payloadResidualRisks[i].Blocking);
        }

        return AssuranceVerdictCalculator.Calculate(claimStates, residualRiskStates);
    }
}
