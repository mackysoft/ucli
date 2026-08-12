using System.Text.Json;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexOpsDescribeJsonFullMetadataTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Writer_EmitsProductMetadataAlongsideUnchangedGeneratedContracts ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateCodeOperationIndexContract();

        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);

        using var document = JsonDocument.Parse(json);
        var operation = document.RootElement.GetProperty("operation");
        Assert.Equal(IndexOpsDescribeContractTestData.CodeOperationName, operation.GetProperty("name").GetString());
        Assert.Equal("mutation", operation.GetProperty("kind").GetString());
        Assert.Equal("dangerous", operation.GetProperty("policy").GetString());
        Assert.Equal(
            IndexOpsDescribeContractTestData.CodeOperationDescriptorDigest,
            operation.GetProperty("descriptorDigest").GetString());
        Assert.Equal(IndexOpsDescribeContractTestData.CodeOperationDescription, operation.GetProperty("description").GetString());
        AssertGeneratedContractEquals(
            IndexOpsDescribeContractTestData.ArgsContractDigest,
            "args",
            operation.GetProperty("argsContract"));
        AssertGeneratedContractEquals(
            IndexOpsDescribeContractTestData.ResultContractDigest,
            "result",
            operation.GetProperty("resultContract"));
        Assert.Equal(JsonValueKind.Null, operation.GetProperty("verdictContract").ValueKind);

        var assurance = operation.GetProperty("assurance");
        Assert.Contains(
            assurance.GetProperty("sideEffects").EnumerateArray().Select(static value => value.GetString()),
            value => string.Equals(value, "arbitrarySourceExecution", StringComparison.Ordinal));
        Assert.Equal("validationOnly", assurance.GetProperty("planMode").GetString());

        var codeContract = operation.GetProperty("codeContract");
        Assert.Equal("csharp", codeContract.GetProperty("language").GetString());
        Assert.Equal(
            "public static object? Run(ExampleContext context)",
            codeContract.GetProperty("entryPoint").GetProperty("signature").GetString());
        Assert.Equal(
            ["compilationUnit", "snippet"],
            codeContract.GetProperty("sourceForms")
                .EnumerateArray()
                .Select(static form => form.GetProperty("kind").GetString()));
    }

    private static void AssertGeneratedContractEquals (
        string expectedContractDigest,
        string expectedTitle,
        JsonElement actual)
    {
        Assert.Equal(
            expectedContractDigest,
            actual.GetProperty("contractDigest").GetString());
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(
                "{\"contractDigest\":\"" + expectedContractDigest + "\",\"title\":\"" + expectedTitle + "\"}"),
            JsonNode.Parse(actual.GetProperty("typeMetadata").GetRawText())));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(
                "{\"x-contract-digest\":\"" + expectedContractDigest + "\",\"type\":\"object\",\"properties\":{}}"),
            JsonNode.Parse(actual.GetProperty("schema").GetRawText())));
    }
}
