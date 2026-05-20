using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;

/// <summary> Collects verifier outputs while one verify profile is executing. </summary>
internal sealed class VerifyPacketBuilder
{
    private readonly Dictionary<string, VerifyReportOutput> reports = new(StringComparer.Ordinal);
    private readonly List<VerifyVerifierOutput> verifiers = [];
    private readonly List<VerifyClaimOutput> claims = [];
    private readonly List<VerifyResidualRiskOutput> residualRisks = [];

    /// <summary> Gets collected verifiers. </summary>
    public IReadOnlyList<VerifyVerifierOutput> Verifiers => verifiers;

    /// <summary> Gets collected claims. </summary>
    public IReadOnlyList<VerifyClaimOutput> Claims => claims;

    /// <summary> Gets collected reports. </summary>
    public IReadOnlyDictionary<string, VerifyReportOutput> Reports => reports;

    /// <summary> Gets collected payload-level residual risks. </summary>
    public IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks => residualRisks;

    /// <summary> Gets a value indicating whether logs should be collected for a non-passing claim. </summary>
    public bool HasNonPassingClaim => claims.Any(static claim =>
        string.Equals(claim.Status, VerifyClaimStatusValues.Failed, StringComparison.Ordinal)
        || string.Equals(claim.Status, VerifyClaimStatusValues.Indeterminate, StringComparison.Ordinal)
        || string.Equals(claim.Status, VerifyClaimStatusValues.Unverified, StringComparison.Ordinal)
        || (claim.Required
            && !string.Equals(claim.Coverage, VerifyCoverageValues.Full, StringComparison.Ordinal)));

    /// <summary> Adds one verifier output. </summary>
    /// <param name="verifier"> The verifier output. </param>
    public void AddVerifier (VerifyVerifierOutput verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        verifiers.Add(verifier);
    }

    /// <summary> Adds one claim output. </summary>
    /// <param name="claim"> The claim output. </param>
    public void AddClaim (VerifyClaimOutput claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        claims.Add(claim);
    }

    /// <summary> Adds one payload-level residual risk. </summary>
    /// <param name="residualRisk"> The residual risk output. </param>
    public void AddResidualRisk (VerifyResidualRiskOutput residualRisk)
    {
        ArgumentNullException.ThrowIfNull(residualRisk);
        residualRisks.Add(residualRisk);
    }

    /// <summary> Adds or replaces one report output. </summary>
    /// <param name="key"> The report reference key. </param>
    /// <param name="report"> The report output. </param>
    public void AddReport (
        string key,
        VerifyReportOutput report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(report);
        reports[key] = report;
    }
}
