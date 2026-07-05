namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeInputTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsDescribe_ReturnsTrue_WhenDescribeContractHasMultiFieldVariant ()
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

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsDescribe_ReturnsFalse_WhenDescribeContractInputIsInvalid ()
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

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }
}
