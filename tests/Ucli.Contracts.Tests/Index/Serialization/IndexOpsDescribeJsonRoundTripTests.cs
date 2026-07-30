using System.Text.Json;
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
        AssertJsonEqual(
            expectedArgsContract.TypeMetadata.GetRawText(),
            actualArgsContract.TypeMetadata.GetRawText());
        AssertJsonEqual(
            expectedArgsContract.Schema.GetRawText(),
            actualArgsContract.Schema.GetRawText());
        var expectedResultContract = expectedOperation.ResultContract!.Value;
        var actualResultContract = deserialized.Operation.ResultContract!.Value;
        Assert.Equal(
            expectedResultContract.ContractDigest,
            actualResultContract.ContractDigest);
        AssertJsonEqual(
            expectedResultContract.TypeMetadata.GetRawText(),
            actualResultContract.TypeMetadata.GetRawText());
        AssertJsonEqual(
            expectedResultContract.Schema.GetRawText(),
            actualResultContract.Schema.GetRawText());
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

    private static void AssertJsonEqual (
        string expected,
        string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.Equal(
            JsonSerializer.Serialize(expectedDocument.RootElement),
            JsonSerializer.Serialize(actualDocument.RootElement));
    }
}
