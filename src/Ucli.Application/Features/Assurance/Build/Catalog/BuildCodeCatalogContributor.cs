using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Catalog;

/// <summary> Contributes build assurance claim codes to the code catalog. </summary>
internal sealed class BuildCodeCatalogContributor : ICodeCatalogContributor
{
    private static readonly IReadOnlyList<string> ClaimAppearsIn =
    [
        "payload.claims[].id",
        "payload.verifiers[].primaryClaims[]",
    ];

    private static readonly IReadOnlyList<string> RiskAppearsIn =
    [
        "payload.residualRisks[].code",
        "payload.claims[].residualRisks[].code",
    ];

    private static readonly IReadOnlyList<UcliCommand> AppliesToBuildRun =
    [
        UcliCommandIds.BuildRun,
    ];

    private static readonly IReadOnlyList<string> Inspect =
    [
        "payload.verdict",
        "payload.claims[]",
        "payload.build",
        "payload.reports",
    ];

    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return
        [
            Create(BuildClaimCodes.UnityBuildProfileResolved, "Build profile resolved to a deterministic input digest."),
            Create(BuildClaimCodes.UnityReadyForBuild, "Unity lifecycle was ready before BuildPipeline execution."),
            Create(BuildClaimCodes.UnityBuildInputsResolved, "Unity BuildPipeline inputs were resolved."),
            Create(BuildClaimCodes.UnityBuildRunnerResolved, "Build runner was resolved before invocation."),
            Create(BuildClaimCodes.UnityBuildExecuteMethodResolved, "executeMethod runner method was resolved before invocation."),
            Create(BuildClaimCodes.UnityBuildExecuteMethodInvoked, "executeMethod runner method invocation started."),
            Create(BuildClaimCodes.UnityBuildExecuteMethodCompleted, "executeMethod runner terminal result was observed."),
            Create(BuildClaimCodes.UnityBuildCompleted, "Unity BuildPipeline produced a terminal BuildReport result."),
            Create(BuildClaimCodes.UnityBuildSucceeded, "Unity BuildPipeline reported a successful result."),
            Create(BuildClaimCodes.UnityBuildResultAccounted, "Build runner terminal result was persisted in build metadata."),
            Create(BuildClaimCodes.UnityBuildReportAccounted, "The BuildReport artifact was written and digested."),
            Create(BuildClaimCodes.UnityBuildArtifactsAccounted, "Build output artifacts were accounted in the output manifest."),
            Create(BuildClaimCodes.UnityBuildOutputDigested, "Build output manifest digest was verified."),
            Create(BuildClaimCodes.UnityBuildLogsAccounted, "The build log artifact was written and summarized."),
            Create(BuildClaimCodes.UnityBuildProjectMutationAccounted, "Project mutation audit was recorded according to build policy."),
            Create(BuildClaimCodes.UnityBuildValidForGeneration, "Build artifacts declare lifecycle generations they are valid for."),
            CreateRisk(BuildRiskCodes.ProjectMutationDetected, "Project mutation was detected by a non-blocking build audit policy."),
            CreateRisk(BuildRiskCodes.ProjectMutationAuditCoverageIncomplete, "Project mutation audit coverage was incomplete under a non-blocking build audit policy."),
        ];
    }

    private static CodeCatalogDescriptor Create (
        UcliCode code,
        string summary)
    {
        return new CodeCatalogDescriptor(
            Code: code,
            Kind: CodeCatalogKindValues.Claim,
            Category: "build",
            Summary: summary,
            Meaning: summary,
            AppearsIn: ClaimAppearsIn,
            AppliesTo: AppliesToBuildRun,
            CoverageImpact: null,
            VerdictSemantics: null,
            ExecutionSemantics: null,
            Inspect: Inspect,
            RelatedCodes: []);
    }

    private static CodeCatalogDescriptor CreateRisk (
        UcliCode code,
        string summary)
    {
        return new CodeCatalogDescriptor(
            Code: code,
            Kind: CodeCatalogKindValues.Risk,
            Category: "build",
            Summary: summary,
            Meaning: summary,
            AppearsIn: RiskAppearsIn,
            AppliesTo: AppliesToBuildRun,
            CoverageImpact: null,
            VerdictSemantics: "non-blocking residual risk does not fail the build verdict",
            ExecutionSemantics: null,
            Inspect: Inspect,
            RelatedCodes: []);
    }
}
