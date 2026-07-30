using System.Collections.ObjectModel;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents the verify assurance payload emitted by the <c>verify</c> command. </summary>
internal sealed record VerifyExecutionOutput : IVerdictResult
{
    /// <summary> Initializes a verify assurance payload and derives its verdict from the supplied evidence. </summary>
    /// <param name="Reports"> The report map to copy with ordinal key semantics. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Reports" /> is <see langword="null" />. </exception>
    public VerifyExecutionOutput (
        ProjectIdentityInfo Project,
        IReadOnlyList<VerifyVerifierOutput> Verifiers,
        IReadOnlyList<VerifyClaimOutput> Claims,
        IReadOnlyDictionary<string, AssuranceReportReference> Reports,
        IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks,
        VerifyProfileOutput Profile,
        int TimeoutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(Reports);
        ArgumentNullException.ThrowIfNull(Project);
        ArgumentNullException.ThrowIfNull(Verifiers);
        ArgumentNullException.ThrowIfNull(Claims);
        ArgumentNullException.ThrowIfNull(ResidualRisks);
        if (Reports.Any(static item => string.IsNullOrWhiteSpace(item.Key) || item.Value is null))
        {
            throw new ArgumentException("Reports must contain non-empty keys and non-null references.", nameof(Reports));
        }

        this.Project = Project;
        this.Verifiers = Array.AsReadOnly(Verifiers.ToArray());
        this.Claims = Array.AsReadOnly(Claims.ToArray());
        this.Reports = new ReadOnlyDictionary<string, AssuranceReportReference>(
            new Dictionary<string, AssuranceReportReference>(Reports, StringComparer.Ordinal));
        this.ResidualRisks = Array.AsReadOnly(ResidualRisks.ToArray());
        Verdict = AssuranceVerdictCalculator.Calculate(this.Verifiers, this.Claims, this.ResidualRisks);
        EnsureReportReferencesResolve(this.Verifiers, this.Claims, this.Reports);
        this.Profile = Profile ?? throw new ArgumentNullException(nameof(Profile));
        this.TimeoutMilliseconds = TimeoutMilliseconds;
    }

    public Verdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    public IReadOnlyList<VerifyVerifierOutput> Verifiers { get; }

    public IReadOnlyList<VerifyClaimOutput> Claims { get; }

    /// <summary> Gets the immutable ordinal-keyed report snapshot. </summary>
    public IReadOnlyDictionary<string, AssuranceReportReference> Reports { get; }

    public IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks { get; }

    public VerifyProfileOutput Profile { get; }

    public int TimeoutMilliseconds { get; }

    private static void EnsureReportReferencesResolve (
        IReadOnlyList<VerifyVerifierOutput> verifiers,
        IReadOnlyList<VerifyClaimOutput> claims,
        IReadOnlyDictionary<string, AssuranceReportReference> reports)
    {
        foreach (var verifier in verifiers)
        {
            if (verifier.ReportRef != null
                && !reports.ContainsKey(verifier.ReportRef.Value))
            {
                throw new ArgumentException(
                    $"Verifier '{verifier.Id}' reportRef '{verifier.ReportRef}' does not resolve to a report.",
                    nameof(Reports));
            }
        }

        foreach (var claim in claims)
        {
            foreach (var evidence in claim.Evidence)
            {
                if (evidence is VerifyReferencedEvidenceOutput referenced
                    && !reports.ContainsKey(referenced.EvidenceRef.Value))
                {
                    throw new ArgumentException(
                        $"Claim '{claim.Id}' evidenceRef '{referenced.EvidenceRef}' does not resolve to a report.",
                        nameof(Reports));
                }
            }
        }
    }
}
