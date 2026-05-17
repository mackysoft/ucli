using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Contributes compile assurance claim codes to the code catalog. </summary>
internal sealed class CompileCodeCatalogContributor : ICodeCatalogContributor
{
    private static readonly IReadOnlyList<string> ClaimAppearsIn =
    [
        "payload.claims[].id",
        "payload.verifiers[].primaryClaims[]",
    ];

    private static readonly IReadOnlyList<UcliCommand> AppliesToCompile =
    [
        UcliCommandIds.Compile,
    ];

    private static readonly IReadOnlyList<string> Inspect =
    [
        "payload.verdict",
        "payload.claims[]",
        "payload.compile",
    ];

    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return
        [
            Create(CompileClaimCodes.UnityCompileNoErrors, "Unity script compilation completed without compiler errors."),
            Create(CompileClaimCodes.UnityDomainReloadSettled, "Unity domain reload state settled after compile observation."),
            Create(CompileClaimCodes.UnityLifecycleReadyAfterCompile, "Unity lifecycle is ready after compile observation."),
        ];
    }

    private static CodeCatalogDescriptor Create (
        UcliCode code,
        string summary)
    {
        return new CodeCatalogDescriptor(
            Code: code,
            Kind: CodeCatalogKindValues.Claim,
            Category: "compile",
            Summary: summary,
            Meaning: summary,
            AppearsIn: ClaimAppearsIn,
            AppliesTo: AppliesToCompile,
            CoverageImpact: null,
            VerdictSemantics: null,
            ExecutionSemantics: null,
            Inspect: Inspect,
            RelatedCodes: []);
    }
}
