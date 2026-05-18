using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;

/// <summary> Contributes verify assurance claim and risk codes to the code catalog. </summary>
internal sealed class VerifyCodeCatalogContributor : ICodeCatalogContributor
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

    private static readonly IReadOnlyList<UcliCommand> AppliesToVerify =
    [
        UcliCommandIds.Verify,
    ];

    private static readonly IReadOnlyList<string> Inspect =
    [
        "payload.verdict",
        "payload.profile",
        "payload.verifiers[]",
        "payload.claims[]",
    ];

    private static readonly IReadOnlyList<string> RiskInspect =
    [
        "payload.verdict",
        "payload.profile",
        "payload.residualRisks[]",
        "payload.claims[].residualRisks[]",
    ];

    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return
        [
            CreateClaim(VerifyClaimCodes.PersistenceUnitTouched, "Touched persistence units were observed from the input result."),
            CreateClaim(VerifyClaimCodes.ReadSurfaceSafe, "Read-postcondition requirements were observed for affected read surfaces."),
            CreateClaim(VerifyClaimCodes.PostMutationObserved, "Expected post-mutation state was observed when deterministic observation is available."),
            CreateClaim(VerifyClaimCodes.UnityTestsPassed, "Unity Test Runner execution passed."),
            CreateRisk(VerifyRiskCodes.FromDiagnosticCoverageUnbound, "Input diagnostics affected coverage but did not map to a generated post-read claim."),
        ];
    }

    private static CodeCatalogDescriptor CreateClaim (
        UcliCode code,
        string summary)
    {
        return new CodeCatalogDescriptor(
            Code: code,
            Kind: CodeCatalogKindValues.Claim,
            Category: "verify",
            Summary: summary,
            Meaning: summary,
            AppearsIn: ClaimAppearsIn,
            AppliesTo: AppliesToVerify,
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
            Category: "verify",
            Summary: summary,
            Meaning: summary,
            AppearsIn: RiskAppearsIn,
            AppliesTo: AppliesToVerify,
            CoverageImpact: "blocking",
            VerdictSemantics: "blocking residual risk fails the verify verdict",
            ExecutionSemantics: null,
            Inspect: RiskInspect,
            RelatedCodes: []);
    }
}
