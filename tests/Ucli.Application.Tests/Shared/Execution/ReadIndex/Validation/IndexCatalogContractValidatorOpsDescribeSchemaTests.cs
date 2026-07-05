namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeSchemaTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsDescribe_ReturnsFalse_WhenDescribeContractIsMissing ()
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: new IndexOpEntryJsonContract(
                Name: "ucli.scene.open",
                Kind: "command",
                Policy: "safe",
                ArgsSchemaJson: """{"type":"object"}"""));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsDescribe_ReturnsFalse_WhenArgsSchemaUsesUnsupportedKeyword ()
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(argsSchemaJson: """{"type":"object","oneOf":[]}"""));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsDescribe_ReturnsFalse_WhenNoResultEntryHasResultSchema ()
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(resultSchemaJson: """{"type":"object"}"""));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"type":"object","additionalProperties":false,"properties":{"var":{"type":"string"}}}""")]
    [InlineData("""{"type":"object","additionalProperties":false,"properties":{"target":{"type":"object","additionalProperties":false,"properties":{"var":{"type":"string"}}}}}""")]
    [InlineData("""{"type":"object","additionalProperties":false,"required":["var"],"properties":{"target":{"type":"string"}}}""")]
    public void IsValidOpsDescribe_ReturnsFalse_WhenArgsSchemaExposesRequestLocalAliasProperty (string argsSchemaJson)
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry(argsSchemaJson: argsSchemaJson));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }
}
