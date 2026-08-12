using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexOpsDescribeJsonRoundTripTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_DeserializesFixedOperationMetadataAndSchemaObjects ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateGoDescribeIndexContract();
        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);
        var deserialized = IndexOpsDescribeJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(1, deserialized.SchemaVersion);
        Assert.Equal("source-hash", deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Operation);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, deserialized.Operation.Name);
        Assert.Equal(UcliOperationPlayModeSupport.Disallowed, deserialized.Operation.PlayModeSupport);
        Assert.Equal(
            IndexOpsDescribeContractTestData.GoDescribeDescriptorDigest,
            deserialized.Operation.DescriptorDigest!.ToString());
        Assert.Equal(IndexOpsDescribeContractTestData.GoDescribeDescription, deserialized.Operation.Description);
        Assert.NotNull(deserialized.Operation.ArgsContract);
        Assert.NotNull(deserialized.Operation.ResultContract);
        var actualArgsContract = deserialized.Operation.ArgsContract!.Value;
        Assert.Equal(
            IndexOpsDescribeContractTestData.ArgsContractDigest,
            actualArgsContract.ContractDigest.ToString());
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(
                "{\"contractDigest\":\"" + IndexOpsDescribeContractTestData.ArgsContractDigest + "\",\"title\":\"args\"}"),
            JsonNode.Parse(actualArgsContract.TypeMetadata.GetRawText())));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(
                "{\"x-contract-digest\":\"" + IndexOpsDescribeContractTestData.ArgsContractDigest + "\",\"type\":\"object\",\"properties\":{}}"),
            JsonNode.Parse(actualArgsContract.Schema.GetRawText())));
        var actualResultContract = deserialized.Operation.ResultContract!.Value;
        Assert.Equal(
            IndexOpsDescribeContractTestData.ResultContractDigest,
            actualResultContract.ContractDigest.ToString());
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(
                "{\"contractDigest\":\"" + IndexOpsDescribeContractTestData.ResultContractDigest + "\",\"title\":\"result\"}"),
            JsonNode.Parse(actualResultContract.TypeMetadata.GetRawText())));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(
                "{\"x-contract-digest\":\"" + IndexOpsDescribeContractTestData.ResultContractDigest + "\",\"type\":\"object\",\"properties\":{}}"),
            JsonNode.Parse(actualResultContract.Schema.GetRawText())));
        Assert.Equal(
            IndexOpsDescribeContractTestData.GoDescribeVerdictDescription,
            deserialized.Operation.VerdictContract!.Description);
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
