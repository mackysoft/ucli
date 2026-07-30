namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescriptorDigestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenDescriptorDigestIsMissing ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport
            .CreateValidOpsEntry() with
        {
            DescriptorDigest = null,
        };
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenDescriptorDigestDoesNotMatchSemanticDescriptor ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport
            .CreateValidOpsEntry() with
        {
            DescriptorDigest = Sha256DigestTestFactory.Compute("different descriptor"),
        };
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }
}
