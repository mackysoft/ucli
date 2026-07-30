using System.Collections.ObjectModel;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents the compile assurance payload emitted by the <c>compile</c> command. </summary>
internal sealed record CompileExecutionOutput : IVerdictResult
{
    /// <summary> Initializes a compile assurance payload and derives its verdict from the supplied evidence. </summary>
    /// <param name="Reports"> The report map to copy with ordinal key semantics. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Reports" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a finite-vocabulary argument is not defined by the assurance contract. </exception>
    public CompileExecutionOutput (
        ProjectIdentityInfo Project,
        IReadOnlyList<CompileVerifierOutput> Verifiers,
        IReadOnlyList<CompileClaimOutput> Claims,
        IReadOnlyDictionary<string, AssuranceReportReference> Reports,
        IReadOnlyList<CompileResidualRiskOutput> ResidualRisks,
        AssuranceRequestedExecutionMode RequestedMode,
        AssuranceResolvedExecutionMode ResolvedMode,
        AssuranceSessionKind SessionKind,
        int TimeoutMilliseconds,
        CompileOutput Compile)
    {
        if (!TextVocabulary.IsDefined(SessionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(SessionKind), SessionKind, "Session kind must be defined by the assurance contract.");
        }
        if (!TextVocabulary.IsDefined(RequestedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(RequestedMode), RequestedMode, "Requested execution mode must be defined by the assurance contract.");
        }
        if (!TextVocabulary.IsDefined(ResolvedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(ResolvedMode), ResolvedMode, "Resolved execution mode must be defined by the assurance contract.");
        }
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
        this.RequestedMode = RequestedMode;
        this.ResolvedMode = ResolvedMode;
        this.SessionKind = SessionKind;
        this.TimeoutMilliseconds = TimeoutMilliseconds;
        this.Compile = Compile ?? throw new ArgumentNullException(nameof(Compile));
    }

    public Verdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    public IReadOnlyList<CompileVerifierOutput> Verifiers { get; }

    public IReadOnlyList<CompileClaimOutput> Claims { get; }

    /// <summary> Gets the immutable ordinal-keyed report snapshot. </summary>
    public IReadOnlyDictionary<string, AssuranceReportReference> Reports { get; }

    public IReadOnlyList<CompileResidualRiskOutput> ResidualRisks { get; }

    public AssuranceRequestedExecutionMode RequestedMode { get; }

    public AssuranceResolvedExecutionMode ResolvedMode { get; }

    public AssuranceSessionKind SessionKind { get; }

    public int TimeoutMilliseconds { get; }

    public CompileOutput Compile { get; }

    private static void EnsureReportReferencesResolve (
        IReadOnlyList<CompileVerifierOutput> verifiers,
        IReadOnlyList<CompileClaimOutput> claims,
        IReadOnlyDictionary<string, AssuranceReportReference> reports)
    {
        foreach (var verifier in verifiers)
        {
            if (!reports.ContainsKey(verifier.ReportRef.Value))
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
                if (evidence is CompileReferencedInlineEvidenceOutput referenced
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
