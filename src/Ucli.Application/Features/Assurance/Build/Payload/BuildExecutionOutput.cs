using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the build assurance payload emitted by the <c>build.run</c> command. </summary>
internal sealed record BuildExecutionOutput : IVerdictResult
{
    /// <summary> Initializes a build assurance payload and derives its verdict from the supplied evidence. </summary>
    /// <param name="Reports"> The finite build report artifacts. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Reports" /> is <see langword="null" />. </exception>
    public BuildExecutionOutput (
        ProjectIdentityInfo Project,
        BuildOutput Build,
        IReadOnlyList<BuildVerifierOutput> Verifiers,
        IReadOnlyList<BuildClaimOutput> Claims,
        BuildReportsOutput Reports,
        IReadOnlyList<BuildResidualRiskOutput> ResidualRisks)
    {
        ArgumentNullException.ThrowIfNull(Reports);
        ArgumentNullException.ThrowIfNull(Project);
        ArgumentNullException.ThrowIfNull(Verifiers);
        ArgumentNullException.ThrowIfNull(Claims);
        ArgumentNullException.ThrowIfNull(ResidualRisks);

        this.Project = Project;
        this.Build = Build ?? throw new ArgumentNullException(nameof(Build));
        this.Verifiers = Array.AsReadOnly(Verifiers.ToArray());
        this.Claims = Array.AsReadOnly(Claims.ToArray());
        this.Reports = Reports;
        this.ResidualRisks = Array.AsReadOnly(ResidualRisks.ToArray());
        Verdict = AssuranceVerdictCalculator.Calculate(this.Verifiers, this.Claims, this.ResidualRisks);
    }

    public Verdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    public BuildOutput Build { get; }

    public IReadOnlyList<BuildVerifierOutput> Verifiers { get; }

    public IReadOnlyList<BuildClaimOutput> Claims { get; }

    /// <summary> Gets the finite build report artifacts. </summary>
    public BuildReportsOutput Reports { get; }

    public IReadOnlyList<BuildResidualRiskOutput> ResidualRisks { get; }
}
