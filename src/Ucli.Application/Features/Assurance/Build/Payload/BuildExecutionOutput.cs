namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the build assurance payload emitted by the <c>build.run</c> command. </summary>
internal sealed record BuildExecutionOutput (
    string Verdict,
    ProjectIdentityInfo Project,
    BuildOutput Build,
    IReadOnlyList<BuildVerifierOutput> Verifiers,
    IReadOnlyList<BuildClaimOutput> Claims,
    IReadOnlyDictionary<string, BuildReportOutput> Reports,
    IReadOnlyList<BuildResidualRiskOutput> ResidualRisks);
