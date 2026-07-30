using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexOpsDescribeJsonMalformedTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""[]""")]
    [InlineData("""{}""")]
    [InlineData("""{"schemaVersion":"1","generatedAtUtc":"2026-03-03T00:00:00+00:00","operation":null}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":1,"operation":null}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"not-a-date","operation":null}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"2026-03-03T00:00:00+00:00","sourceInputsHash":1,"operation":null}""")]
    public void Serializer_ThrowsJsonException_WhenRootContractIsMalformed (string json)
    {
        Assert.Throws<JsonException>(() => IndexOpsDescribeJsonContractSerializer.Deserialize(json));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("resultContract")]
    [InlineData("verdictContract")]
    public void Serializer_ThrowsJsonException_WhenRequiredNullableOperationContractIsMissing (
        string propertyName)
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateGoDescribeIndexContract();
        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);
        var root = JsonNode.Parse(json)!.AsObject();
        var operation = root["operation"]!.AsObject();
        Assert.True(operation.Remove(propertyName));

        Assert.Throws<JsonException>(() =>
            IndexOpsDescribeJsonContractSerializer.Deserialize(root.ToJsonString()));
    }
}
