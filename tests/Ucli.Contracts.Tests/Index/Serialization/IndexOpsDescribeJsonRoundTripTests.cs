using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexOpsDescribeJsonRoundTripTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsOperationMetadataAndSchemaObjects ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateGoDescribeIndexContract();
        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);
        var deserialized = IndexOpsDescribeJsonContractSerializer.Deserialize(json);

        var expectedOperation = contract.Operation!;
        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Operation);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, deserialized.Operation.Name);
        Assert.Equal("""{"type":"object"}""", deserialized.Operation.ArgsSchemaJson);
        Assert.Equal("""{"type":"object"}""", deserialized.Operation.ResultSchemaJson);
        Assert.Equal(expectedOperation.Description, deserialized.Operation.Description);
        Assert.NotNull(deserialized.Operation.Inputs);
        Assert.NotNull(deserialized.Operation.ResultContract);
        Assert.Equal("GameObjectDescriptionResult", deserialized.Operation.ResultContract!.ResultType);
        Assert.NotNull(deserialized.Operation.Assurance);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsInputVariantFieldContracts ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateGoDescribeIndexContract();
        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);
        var deserialized = IndexOpsDescribeJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized?.Operation);
        var operation = deserialized!.Operation!;
        Assert.NotNull(operation.Inputs);
        var inputs = operation.Inputs!;
        var targetInput = inputs.Single(input =>
            string.Equals(input.Name, "target", StringComparison.Ordinal));
        var globalObjectIdVariant = targetInput.Variants!.Single(variant =>
            string.Equals(variant.Name, "byGlobalObjectId", StringComparison.Ordinal));
        var globalObjectIdField = Assert.Single(globalObjectIdVariant.Fields!);
        Assert.Equal("globalObjectId", globalObjectIdField.Name);
        Assert.Equal("$.target.globalObjectId", globalObjectIdField.ArgsPath);
        Assert.Equal("Resolved Unity GlobalObjectId.", globalObjectIdField.Description);
        Assert.Contains(globalObjectIdField.Constraints!, constraint => constraint.Kind == "globalObjectId");
    }
}
