namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Calculates an assurance verdict from normalized claim and residual risk state. </summary>
internal static class AssuranceVerdictCalculator
{
    /// <summary> Calculates the verdict value. </summary>
    /// <param name="claims"> The normalized claim states. </param>
    /// <param name="payloadResidualRisks"> The normalized payload-level residual risk states. </param>
    /// <returns> The calculated verdict. </returns>
    public static AssuranceVerdict Calculate (
        IReadOnlyList<AssuranceVerdictClaimState> claims,
        IReadOnlyList<AssuranceVerdictResidualRiskState> payloadResidualRisks)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(payloadResidualRisks);

        for (var i = 0; i < payloadResidualRisks.Count; i++)
        {
            if (payloadResidualRisks[i].Blocking)
            {
                return AssuranceVerdict.Fail;
            }
        }

        var hasRequiredIncompleteClaim = false;
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            if (claim.HasBlockingResidualRisk)
            {
                return AssuranceVerdict.Fail;
            }

            if (!claim.Required)
            {
                continue;
            }

            if (claim.Status == AssuranceClaimStatus.Failed)
            {
                return AssuranceVerdict.Fail;
            }

            if (claim.Status != AssuranceClaimStatus.Passed
                || claim.Coverage != AssuranceCoverage.Full)
            {
                hasRequiredIncompleteClaim = true;
            }
        }

        return hasRequiredIncompleteClaim ? AssuranceVerdict.Incomplete : AssuranceVerdict.Pass;
    }
}
