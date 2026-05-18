using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;

/// <summary> Contributes verify assurance claim codes to the code catalog. </summary>
internal sealed class VerifyCodeCatalogContributor : ICodeCatalogContributor
{
    private static readonly IReadOnlyList<string> ClaimAppearsIn =
    [
        "payload.claims[].id",
        "payload.verifiers[].primaryClaims[]",
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

    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return
        [
            Create(VerifyClaimCodes.PersistenceUnitTouched, "Touched persistence units were observed from the input result."),
            Create(VerifyClaimCodes.ReadSurfaceSafe, "Read-postcondition requirements were observed for affected read surfaces."),
            Create(VerifyClaimCodes.PostMutationObserved, "Expected post-mutation state was observed when deterministic observation is available."),
            Create(VerifyClaimCodes.UnityTestsPassed, "Unity Test Runner execution passed."),
        ];
    }

    private static CodeCatalogDescriptor Create (
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
}
