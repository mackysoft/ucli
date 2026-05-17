using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Contributes ready assurance claim codes to the code catalog. </summary>
internal sealed class ReadyCodeCatalogContributor : ICodeCatalogContributor
{
    private static readonly IReadOnlyList<string> ClaimAppearsIn =
    [
        "payload.claims[].id",
        "payload.verifiers[].primaryClaims[]",
    ];

    private static readonly IReadOnlyList<UcliCommand> AppliesToReady =
    [
        UcliCommandIds.Ready,
    ];

    private static readonly IReadOnlyList<string> Inspect =
    [
        "payload.verdict",
        "payload.claims[]",
    ];

    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return
        [
            Create(ReadyClaimCodes.UnityReadyExecution, "Unity is ready for execution requests."),
            Create(ReadyClaimCodes.UnityReadyMutation, "Unity is ready for mutation requests."),
            Create(ReadyClaimCodes.UnityReadyTest, "Unity is ready for Unity Test Runner requests."),
            Create(ReadyClaimCodes.UnityReadyReadIndex, "Project-wide readIndex artifacts satisfy the selected mode."),
        ];
    }

    private static CodeCatalogDescriptor Create (
        string code,
        string summary)
    {
        return new CodeCatalogDescriptor(
            Code: code,
            Kind: CodeCatalogKindValues.Claim,
            Category: "ready",
            Summary: summary,
            Meaning: summary,
            AppearsIn: ClaimAppearsIn,
            AppliesTo: AppliesToReady,
            CoverageImpact: null,
            VerdictSemantics: null,
            ExecutionSemantics: null,
            Inspect: Inspect,
            RelatedCodes: []);
    }
}
