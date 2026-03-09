using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class DaemonDiagnosisJsonContractSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithValidJson_ReturnsContract ()
    {
        const string Json = """
            {
              "reason": "shutdownRequested",
              "message": "daemon shutdown completed",
              "updatedAtUtc": "2026-03-09T00:00:00+00:00",
              "processId": 1234,
              "sessionIssuedAtUtc": "2026-03-09T00:00:01+00:00"
            }
            """;

        var contract = DaemonDiagnosisJsonContractSerializer.Deserialize(Json);

        Assert.NotNull(contract);
        Assert.Equal("shutdownRequested", contract.Reason);
        Assert.Equal("daemon shutdown completed", contract.Message);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"), contract.UpdatedAtUtc);
        Assert.Equal(1234, contract.ProcessId);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:01+00:00"), contract.SessionIssuedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithNullLiteral_ReturnsNull ()
    {
        var contract = DaemonDiagnosisJsonContractSerializer.Deserialize("null");

        Assert.Null(contract);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithWhitespace_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            DaemonDiagnosisJsonContractSerializer.Deserialize(" ");
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WithContract_WritesCamelCaseFields ()
    {
        var contract = new DaemonDiagnosisJsonContract(
            Reason: "shutdownRequested",
            Message: "daemon shutdown completed",
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"),
            ProcessId: 1234,
            SessionIssuedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:01+00:00"));

        var json = DaemonDiagnosisJsonContractSerializer.Serialize(contract);
        using var jsonDocument = JsonDocument.Parse(json);

        JsonAssert.For(jsonDocument.RootElement)
            .MatchesSchema(DiagnosisJsonSchema, nameof(DiagnosisJsonSchema));
    }

    private static JsonSchemaNode DiagnosisJsonSchema => JsonSchemaNode.Object(
        builder => builder
            .Required("reason", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("message", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("updatedAtUtc", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("processId", JsonSchemaNode.Value(JsonSchemaType.Int32))
            .Required("sessionIssuedAtUtc", JsonSchemaNode.Value(JsonSchemaType.String)),
        allowAdditionalProperties: false);
}
