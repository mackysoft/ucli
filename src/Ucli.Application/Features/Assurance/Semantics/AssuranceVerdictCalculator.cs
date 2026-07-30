namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Calculates an assurance verdict from normalized claim and residual risk state. </summary>
internal static class AssuranceVerdictCalculator
{
    /// <summary> Calculates the verdict value. </summary>
    /// <param name="verifiers"> The verifiers that produced the claims. </param>
    /// <param name="claims"> The normalized claim states. </param>
    /// <param name="payloadResidualRisks"> The normalized payload-level residual risk states. </param>
    /// <returns> The calculated verdict. </returns>
    /// <exception cref="ArgumentException"> Thrown when verifier and claim relationships do not establish one valid assurance aggregate. </exception>
    public static Verdict Calculate<TVerifier, TClaim, TResidualRisk> (
        IReadOnlyList<TVerifier> verifiers,
        IReadOnlyList<TClaim> claims,
        IReadOnlyList<TResidualRisk> payloadResidualRisks)
        where TVerifier : class, IAssuranceVerdictVerifier
        where TClaim : class, IAssuranceVerdictClaim
        where TResidualRisk : class, IAssuranceVerdictResidualRisk
    {
        ArgumentNullException.ThrowIfNull(verifiers);
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(payloadResidualRisks);
        EnsureAggregateIsEstablished(verifiers, claims);
        if (payloadResidualRisks.Any(static risk => risk is null))
        {
            throw new ArgumentException(
                "Payload residual risks must not contain null.",
                nameof(payloadResidualRisks));
        }

        var requiredClaimsVerdict = CalculateRequiredClaimsVerdict(claims);
        if (requiredClaimsVerdict == Verdict.Fail
            || HasBlockingResidualRisk(claims, payloadResidualRisks))
        {
            return Verdict.Fail;
        }

        return requiredClaimsVerdict;
    }

    private static void EnsureAggregateIsEstablished<TVerifier, TClaim> (
        IReadOnlyList<TVerifier> verifiers,
        IReadOnlyList<TClaim> claims)
        where TVerifier : class, IAssuranceVerdictVerifier
        where TClaim : class, IAssuranceVerdictClaim
    {
        var verifierById = new Dictionary<AssuranceVerifierId, TVerifier>();
        for (var i = 0; i < verifiers.Count; i++)
        {
            var verifier = verifiers[i] ?? throw new ArgumentException(
                "Verifiers must not contain null.",
                nameof(verifiers));
            if (!verifierById.TryAdd(verifier.Id, verifier))
            {
                throw new ArgumentException(
                    $"Verifier id '{verifier.Id}' must be unique.",
                    nameof(verifiers));
            }
        }

        var claimById = new Dictionary<UcliCode, TClaim>();
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i] ?? throw new ArgumentException(
                "Claims must not contain null.",
                nameof(claims));
            if (!claimById.TryAdd(claim.Id, claim))
            {
                throw new ArgumentException(
                    $"Claim id '{claim.Id}' must be unique.",
                    nameof(claims));
            }
        }

        foreach (var claim in claims)
        {
            if (!verifierById.TryGetValue(claim.VerifierRef, out var verifier))
            {
                throw new ArgumentException(
                    $"Claim '{claim.Id}' verifierRef '{claim.VerifierRef}' does not resolve to a verifier.",
                    nameof(claims));
            }

            if (claim.Required && !verifier.Required)
            {
                throw new ArgumentException(
                    $"Required claim '{claim.Id}' must reference a required verifier.",
                    nameof(claims));
            }
        }

        foreach (var verifier in verifiers)
        {
            if (verifier.Required && verifier.PrimaryClaims.Count == 0)
            {
                throw new ArgumentException(
                    $"Required verifier '{verifier.Id}' must declare at least one primary claim.",
                    nameof(verifiers));
            }

            for (var i = 0; i < verifier.PrimaryClaims.Count; i++)
            {
                var claimId = verifier.PrimaryClaims[i];
                if (!claimById.TryGetValue(claimId, out var claim))
                {
                    throw new ArgumentException(
                        $"Verifier '{verifier.Id}' primary claim '{claimId}' does not resolve to a claim.",
                        nameof(verifiers));
                }

                if (claim.VerifierRef != verifier.Id)
                {
                    throw new ArgumentException(
                        $"Verifier '{verifier.Id}' primary claim '{claimId}' is owned by verifierRef '{claim.VerifierRef}'.",
                        nameof(verifiers));
                }

                if (verifier.Required && !claim.Required)
                {
                    throw new ArgumentException(
                        $"Required verifier '{verifier.Id}' primary claim '{claimId}' must be required.",
                        nameof(verifiers));
                }
            }
        }
    }

    private static Verdict CalculateRequiredClaimsVerdict<TClaim> (
        IReadOnlyList<TClaim> claims)
        where TClaim : class, IAssuranceVerdictClaim
    {
        var hasRequiredIncompleteClaim = false;
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            if (!claim.Required)
            {
                continue;
            }

            if (claim.Status == AssuranceClaimStatus.Failed)
            {
                return Verdict.Fail;
            }

            if (claim.Status != AssuranceClaimStatus.Passed
                || claim.Coverage != AssuranceCoverage.Full)
            {
                hasRequiredIncompleteClaim = true;
            }
        }

        return hasRequiredIncompleteClaim
            ? Verdict.Incomplete
            : Verdict.Pass;
    }

    private static bool HasBlockingResidualRisk<TClaim, TResidualRisk> (
        IReadOnlyList<TClaim> claims,
        IReadOnlyList<TResidualRisk> payloadResidualRisks)
        where TClaim : class, IAssuranceVerdictClaim
        where TResidualRisk : class, IAssuranceVerdictResidualRisk
    {
        for (var i = 0; i < payloadResidualRisks.Count; i++)
        {
            if (payloadResidualRisks[i].Blocking)
            {
                return true;
            }
        }

        for (var i = 0; i < claims.Count; i++)
        {
            if (claims[i].HasBlockingResidualRisk)
            {
                return true;
            }
        }

        return false;
    }
}
