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
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateCsEvalIndexContract();

        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);

        using var document = JsonDocument.Parse(json);
        var operation = document.RootElement.GetProperty("operation");
        var expectedOperation = contract.Operation!;
        Assert.Equal(UcliPrimitiveOperationNames.CsEval, operation.GetProperty("name").GetString());
        Assert.Equal("mutation", operation.GetProperty("kind").GetString());
        Assert.Equal("dangerous", operation.GetProperty("policy").GetString());
        Assert.Equal(
            expectedOperation.DescriptorDigest!.ToString(),
            operation.GetProperty("descriptorDigest").GetString());
        Assert.Equal(expectedOperation.Description, operation.GetProperty("description").GetString());
        AssertGeneratedContractEquals(
            expectedOperation.ArgsContract!.Value,
            operation.GetProperty("argsContract"));
        AssertGeneratedContractEquals(
            expectedOperation.ResultContract!.Value,
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
            "public static object? | Task | Task<T> | ValueTask | ValueTask<T> Run(UcliCsEvalContext context)",
            codeContract.GetProperty("entryPoint").GetProperty("signature").GetString());
        Assert.Equal(
            ["compilationUnit", "snippet"],
            codeContract.GetProperty("sourceForms")
                .EnumerateArray()
                .Select(static form => form.GetProperty("kind").GetString()));
    }

    private static void AssertGeneratedContractEquals (
        UcliOperationJsonContract expected,
        JsonElement actual)
    {
        Assert.Equal(
            expected.ContractDigest.ToString(),
            actual.GetProperty("contractDigest").GetString());
        Assert.Equal(
            expected.TypeMetadata.GetRawText(),
            JsonSerializer.Serialize(actual.GetProperty("typeMetadata")));
        Assert.Equal(
            expected.Schema.GetRawText(),
            JsonSerializer.Serialize(actual.GetProperty("schema")));
    }
}
