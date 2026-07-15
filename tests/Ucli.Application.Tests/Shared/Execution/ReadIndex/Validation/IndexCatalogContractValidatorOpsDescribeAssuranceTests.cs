namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeAssuranceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenQueryDescribeDeclaresTouchedKinds ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry() with
        {
            Kind = "query",
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: [UcliTouchedResourceKind.Scene],
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Observe scene hierarchy without applying mutation.",
                callSemantics: "Read scene hierarchy without applying mutation.",
                touchedContract: "Invalid query touched resource declaration.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation did not complete.",
                dangerousNotes: Array.Empty<string>()),
        };
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(entry);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenPublicOperationMayCreatePreviewState ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry() with
        {
            Policy = "advanced",
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.MayCreatePreviewState,
                planSemantics: "Create request-local preview state before approval.",
                callSemantics: "Apply the requested operation.",
                touchedContract: "Reports no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the operation did not complete.",
                dangerousNotes: ["Preview-state planning is not public raw safe."]),
        };
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(entry);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }
}
