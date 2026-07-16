namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeInputTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsTrue_WhenDescribeContractHasMultiFieldVariant ()
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(
                inputs:
                [
                    new UcliOperationInputContract(
                        name: "target",
                        valueType: "object",
                        description: "Object reference to resolve.",
                        constraints: Array.Empty<UcliOperationInputConstraintContract>(),
                        variants:
                        [
                            new UcliOperationInputVariantContract(
                                name: "sceneHierarchy",
                                description: "Use scene and hierarchy path.",
                                fields:
                                [
                                    new UcliOperationInputVariantFieldContract(
                                        name: "scene",
                                        argsPath: "$.target.scene",
                                        description: "Scene asset path.",
                                        constraints:
                                        [
                                            new UcliOperationInputConstraintContract("assetExists")
                                            {
                                                AssetKind = "scene",
                                            },
                                        ]),
                                    new UcliOperationInputVariantFieldContract(
                                        name: "hierarchyPath",
                                        argsPath: "$.target.hierarchyPath",
                                        description: "Hierarchy path.",
                                        constraints:
                                        [
                                            new UcliOperationInputConstraintContract("hierarchyPath"),
                                        ]),
                                ]),
                        ]),
                ]));

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenDescribeContractInputIsInvalid ()
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(
                inputs:
                [
                    new UcliOperationInputContract(
                        name: "var",
                        valueType: "object",
                        description: "Object reference to resolve.",
                        constraints: Array.Empty<UcliOperationInputConstraintContract>()),
                ]));

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }
}
