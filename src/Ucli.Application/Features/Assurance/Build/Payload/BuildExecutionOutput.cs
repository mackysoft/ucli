using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the build assurance payload emitted by the <c>build.run</c> command. </summary>
internal sealed record BuildExecutionOutput
{
    /// <summary> Initializes a build assurance payload with a defined verdict. </summary>
    /// <param name="Reports"> The finite build report artifacts. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Reports" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Verdict" /> is not defined by the assurance contract. </exception>
    public BuildExecutionOutput (
        AssuranceVerdict Verdict,
        ProjectIdentityInfo Project,
        BuildOutput Build,
        IReadOnlyList<BuildVerifierOutput> Verifiers,
        IReadOnlyList<BuildClaimOutput> Claims,
        BuildReportsOutput Reports,
        IReadOnlyList<BuildResidualRiskOutput> ResidualRisks)
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

        if (ResidualRisks.Any(static item => item is null))
        {
            throw new ArgumentException("Residual risks must not contain null.", nameof(ResidualRisks));
        }

        this.Verdict = Verdict;
        this.Project = Project;
        this.Build = Build ?? throw new ArgumentNullException(nameof(Build));
        this.Verifiers = Array.AsReadOnly(Verifiers.ToArray());
        this.Claims = Array.AsReadOnly(Claims.ToArray());
        this.Reports = Reports;
        this.ResidualRisks = Array.AsReadOnly(ResidualRisks.ToArray());
    }

    public AssuranceVerdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    public BuildOutput Build { get; }

    public IReadOnlyList<BuildVerifierOutput> Verifiers { get; }

    public IReadOnlyList<BuildClaimOutput> Claims { get; }

    /// <summary> Gets the finite build report artifacts. </summary>
    public BuildReportsOutput Reports { get; }

    public IReadOnlyList<BuildResidualRiskOutput> ResidualRisks { get; }
}
