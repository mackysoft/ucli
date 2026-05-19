namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Calculates an assurance verdict from normalized claim and residual risk state. </summary>
internal static class AssuranceVerdictCalculator
{
    private const string CoverageFull = "full";
    private const string StatusFailed = "failed";
    private const string StatusPassed = "passed";
    private const string VerdictFail = "fail";
    private const string VerdictIncomplete = "incomplete";
    private const string VerdictPass = "pass";

    /// <summary> Calculates the verdict value. </summary>
    /// <param name="claims"> The normalized claim states. </param>
    /// <param name="payloadResidualRisks"> The normalized payload-level residual risk states. </param>
    /// <returns> The verdict literal. </returns>
    public static string Calculate (
        IReadOnlyList<AssuranceVerdictClaimState> claims,
        IReadOnlyList<AssuranceVerdictResidualRiskState> payloadResidualRisks)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(payloadResidualRisks);

        for (var i = 0; i < payloadResidualRisks.Count; i++)
        {
            if (payloadResidualRisks[i].Blocking)
            {
                return VerdictFail;
            }
        }

        var hasRequiredIncompleteClaim = false;
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            if (claim.HasBlockingResidualRisk)
            {
                return VerdictFail;
            }

            if (!claim.Required)
            {
                continue;
            }

            if (string.Equals(claim.Status, StatusFailed, StringComparison.Ordinal))
            {
                return VerdictFail;
            }

            if (!string.Equals(claim.Status, StatusPassed, StringComparison.Ordinal)
                || !string.Equals(claim.Coverage, CoverageFull, StringComparison.Ordinal))
            {
                hasRequiredIncompleteClaim = true;
            }
        }

        return hasRequiredIncompleteClaim ? VerdictIncomplete : VerdictPass;
    }
}
