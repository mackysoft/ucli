using System.Collections.ObjectModel;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents the verify assurance payload emitted by the <c>verify</c> command. </summary>
internal sealed record VerifyExecutionOutput
{
    /// <summary> Initializes a verify assurance payload with a defined verdict. </summary>
    /// <param name="Reports"> The report map to copy with ordinal key semantics. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Reports" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Verdict" /> is not defined by the assurance contract. </exception>
    public VerifyExecutionOutput (
        AssuranceVerdict Verdict,
        ProjectIdentityInfo Project,
        IReadOnlyList<VerifyVerifierOutput> Verifiers,
        IReadOnlyList<VerifyClaimOutput> Claims,
        IReadOnlyDictionary<string, AssuranceReportReference> Reports,
        IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks,
        VerifyProfileOutput Profile,
        int TimeoutMilliseconds)
    {
        if (!TextVocabulary.IsDefined(Verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
        }
        ArgumentNullException.ThrowIfNull(Reports);
        ArgumentNullException.ThrowIfNull(Project);
        ArgumentNullException.ThrowIfNull(Verifiers);
        ArgumentNullException.ThrowIfNull(Claims);
        ArgumentNullException.ThrowIfNull(ResidualRisks);
        if (Verifiers.Any(static item => item is null))
        {
            throw new ArgumentException("Verifiers must not contain null.", nameof(Verifiers));
        }

        if (Claims.Any(static item => item is null))
        {
            throw new ArgumentException("Claims must not contain null.", nameof(Claims));
        }

        if (Reports.Any(static item => string.IsNullOrWhiteSpace(item.Key) || item.Value is null))
        {
            throw new ArgumentException("Reports must contain non-empty keys and non-null references.", nameof(Reports));
        }

        if (ResidualRisks.Any(static item => item is null))
        {
            throw new ArgumentException("Residual risks must not contain null.", nameof(ResidualRisks));
        }

        this.Verdict = Verdict;
        this.Project = Project;
        this.Verifiers = Array.AsReadOnly(Verifiers.ToArray());
        this.Claims = Array.AsReadOnly(Claims.ToArray());
        this.Reports = new ReadOnlyDictionary<string, AssuranceReportReference>(
            new Dictionary<string, AssuranceReportReference>(Reports, StringComparer.Ordinal));
        this.ResidualRisks = Array.AsReadOnly(ResidualRisks.ToArray());
        this.Profile = Profile ?? throw new ArgumentNullException(nameof(Profile));
        this.TimeoutMilliseconds = TimeoutMilliseconds;
    }

    public AssuranceVerdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    public IReadOnlyList<VerifyVerifierOutput> Verifiers { get; }

    public IReadOnlyList<VerifyClaimOutput> Claims { get; }

    /// <summary> Gets the immutable ordinal-keyed report snapshot. </summary>
    public IReadOnlyDictionary<string, AssuranceReportReference> Reports { get; }

    public IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks { get; }

    public VerifyProfileOutput Profile { get; }

    public int TimeoutMilliseconds { get; }
}
