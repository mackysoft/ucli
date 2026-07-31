using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents the compile assurance payload emitted by the <c>compile</c> command. </summary>
internal sealed record CompileExecutionOutput : IVerdictResult
{
    /// <summary>
    /// Initializes a compile assurance payload from the verdict fixed by the compile action.
    /// </summary>
    /// <param name="Reports"> The report map to copy with ordinal key semantics. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference or collection is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the Lifecycle Execution reference or report map violates the compile output contract. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Verdict" /> is not defined. </exception>
    public CompileExecutionOutput (
        ProjectIdentityInfo Project,
        ITerminalExecutionRef LifecycleExecutionRef,
        Verdict Verdict,
        IReadOnlyList<CompileVerifierOutput> Verifiers,
        IReadOnlyList<CompileClaimOutput> Claims,
        IReadOnlyDictionary<string, AssuranceReportReference> Reports,
        IReadOnlyList<CompileResidualRiskOutput> ResidualRisks,
        CompileOutput Compile)
    {
        ArgumentNullException.ThrowIfNull(Reports);
        ArgumentNullException.ThrowIfNull(Project);
        var completedLifecycleExecutionRef =
            LifecycleExecutionContractGuard.RequireCompletedTerminalReference(
                LifecycleExecutionRef,
                nameof(LifecycleExecutionRef),
                LifecycleExecutionKind.Compile);

        ArgumentNullException.ThrowIfNull(Verifiers);
        ArgumentNullException.ThrowIfNull(Claims);
        ArgumentNullException.ThrowIfNull(ResidualRisks);
        if (Reports.Any(static item => string.IsNullOrWhiteSpace(item.Key) || item.Value is null))
        {
            throw new ArgumentException("Reports must contain non-empty keys and non-null references.", nameof(Reports));
        }

        this.Project = Project;
        this.LifecycleExecutionRef = completedLifecycleExecutionRef;
        if (!TextVocabulary.IsDefined(Verdict))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Verdict),
                Verdict,
                "Compile verdict must be defined.");
        }
        this.Verdict = Verdict;
        this.Verifiers = Array.AsReadOnly(Verifiers.ToArray());
        this.Claims = Array.AsReadOnly(Claims.ToArray());
        this.Reports = new ReadOnlyDictionary<string, AssuranceReportReference>(
            new Dictionary<string, AssuranceReportReference>(Reports, StringComparer.Ordinal));
        this.ResidualRisks = Array.AsReadOnly(ResidualRisks.ToArray());
        EnsureReportReferencesResolve(this.Verifiers, this.Claims, this.Reports);
        this.Compile = Compile ?? throw new ArgumentNullException(nameof(Compile));
    }

    public Verdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    /// <summary> Gets the completed terminal reference for the compile Lifecycle Execution. </summary>
    public ITerminalExecutionRef LifecycleExecutionRef { get; }

    // These assurance packet members are retained for Verify composition. The public compile
    // command owns a smaller closed payload and therefore does not serialize this internal packet.
    [JsonIgnore]
    public IReadOnlyList<CompileVerifierOutput> Verifiers { get; }

    [JsonIgnore]
    public IReadOnlyList<CompileClaimOutput> Claims { get; }

    /// <summary> Gets the immutable ordinal-keyed report snapshot. </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, AssuranceReportReference> Reports { get; }

    [JsonIgnore]
    public IReadOnlyList<CompileResidualRiskOutput> ResidualRisks { get; }

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
