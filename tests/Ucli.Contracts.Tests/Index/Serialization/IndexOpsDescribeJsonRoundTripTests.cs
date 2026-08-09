using MackySoft.Ucli.Contracts.Configuration;
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
        Assert.Equal(UcliOperationPlayModeSupport.Disallowed, deserialized.Operation.PlayModeSupport);
        Assert.Equal(expectedOperation.DescriptorDigest, deserialized.Operation.DescriptorDigest);
        Assert.Equal(expectedOperation.Description, deserialized.Operation.Description);
        Assert.NotNull(deserialized.Operation.ArgsContract);
        Assert.NotNull(deserialized.Operation.ResultContract);
        var expectedArgsContract = expectedOperation.ArgsContract!.Value;
        var actualArgsContract = deserialized.Operation.ArgsContract!.Value;
        Assert.Equal(
            expectedArgsContract.ContractDigest,
            actualArgsContract.ContractDigest);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(expectedArgsContract.TypeMetadata.GetRawText()),
            JsonNode.Parse(actualArgsContract.TypeMetadata.GetRawText())));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(expectedArgsContract.Schema.GetRawText()),
            JsonNode.Parse(actualArgsContract.Schema.GetRawText())));
        var expectedResultContract = expectedOperation.ResultContract!.Value;
        var actualResultContract = deserialized.Operation.ResultContract!.Value;
        Assert.Equal(
            expectedResultContract.ContractDigest,
            actualResultContract.ContractDigest);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(expectedResultContract.TypeMetadata.GetRawText()),
            JsonNode.Parse(actualResultContract.TypeMetadata.GetRawText())));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(expectedResultContract.Schema.GetRawText()),
            JsonNode.Parse(actualResultContract.Schema.GetRawText())));
        Assert.Equal(
            expectedOperation.VerdictContract,
            deserialized.Operation.VerdictContract);
        Assert.NotNull(deserialized.Operation.Assurance);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_AcceptsExplicitNullResultAndVerdictContracts ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateGoDescribeIndexContract();
        var operation = contract.Operation! with
        {
            ResultContract = null,
            VerdictContract = null,
        };
        var json = new IndexOpsDescribeJsonContractWriter().Write(contract with
        {
            Operation = operation,
        });

        var deserialized = IndexOpsDescribeJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Operation);
        Assert.Null(deserialized.Operation.ResultContract);
        Assert.Null(deserialized.Operation.VerdictContract);
    }
}
