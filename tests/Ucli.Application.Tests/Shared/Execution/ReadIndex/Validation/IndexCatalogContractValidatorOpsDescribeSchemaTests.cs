namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeSchemaTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenDescribeContractIsMissing ()
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("source-hash").ToString(),
            Operation: new IndexOpEntryJsonContract(
                Name: "ucli.scene.open",
                Kind: "command",
                Policy: "safe",
                ArgsSchemaJson: """{"type":"object"}"""));

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenArgsSchemaUsesUnsupportedKeyword ()
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(argsSchemaJson: """{"type":"object","oneOf":[]}"""));

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenNoResultEntryHasResultSchema ()
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(resultSchemaJson: """{"type":"object"}"""));

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Theory]
    [InlineData("Command", "safe")]
    [InlineData("command", "Safe")]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenKindOrPolicyIsNotCanonical (
        string kind,
        string policy)
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry() with
        {
            Kind = kind,
            Policy = policy,
        };
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"type":"object","additionalProperties":false,"properties":{"var":{"type":"string"}}}""")]
    [InlineData("""{"type":"object","additionalProperties":false,"properties":{"target":{"type":"object","additionalProperties":false,"properties":{"var":{"type":"string"}}}}}""")]
    [InlineData("""{"type":"object","additionalProperties":false,"required":["var"],"properties":{"target":{"type":"string"}}}""")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenArgsSchemaExposesRequestLocalAliasProperty (string argsSchemaJson)
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(argsSchemaJson: argsSchemaJson));

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }
}
