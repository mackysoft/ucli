using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class DaemonSessionJsonContractSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithValidJson_ReturnsContract ()
    {
        const string Json = """
            {
              "schemaVersion": 1,
              "sessionToken": "token-123",
              "projectFingerprint": "fingerprint-abc",
              "issuedAtUtc": "2026-03-02T00:00:00+00:00",
              "editorMode": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-endpoint",
              "processId": 1234,
              "ownerProcessId": 5678
            }
            """;

        var contract = DaemonSessionJsonContractSerializer.Deserialize(Json);

        Assert.NotNull(contract);
        Assert.Equal(DaemonSessionStorageContract.CurrentSchemaVersion, contract.SchemaVersion);
        Assert.Equal("token-123", contract.SessionToken);
        Assert.Equal("fingerprint-abc", contract.ProjectFingerprint);
        Assert.Equal(DateTimeOffset.Parse("2026-03-02T00:00:00+00:00"), contract.IssuedAtUtc);
        Assert.Equal("batchmode", contract.EditorMode);
        Assert.Equal("cli", contract.OwnerKind);
        Assert.True(contract.CanShutdownProcess);
        Assert.Equal("namedPipe", contract.EndpointTransportKind);
        Assert.Equal("ucli-daemon-endpoint", contract.EndpointAddress);
        Assert.Equal(1234, contract.ProcessId);
        Assert.Equal(5678, contract.OwnerProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithNullLiteral_ReturnsNull ()
    {
        var contract = DaemonSessionJsonContractSerializer.Deserialize("null");

        Assert.Null(contract);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithWhitespace_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            DaemonSessionJsonContractSerializer.Deserialize(" ");
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WithContract_WritesCamelCaseFields ()
    {
        var contract = new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionToken: "token-123",
            ProjectFingerprint: "fingerprint-abc",
            IssuedAtUtc: DateTimeOffset.Parse("2026-03-02T00:00:00+00:00"),
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            OwnerProcessId: 5678);

        var json = DaemonSessionJsonContractSerializer.Serialize(contract);
        using var jsonDocument = JsonDocument.Parse(json);

        JsonAssert.For(jsonDocument.RootElement)
            .MatchesSchema(SessionJsonSchema, nameof(SessionJsonSchema));
        Assert.False(jsonDocument.RootElement.TryGetProperty("runtimeKind", out _));
    }

    private static JsonSchemaNode SessionJsonSchema => JsonSchemaNode.Object(
        builder => builder
            .Required("schemaVersion", JsonSchemaNode.Value(JsonSchemaType.Int32))
            .Required("sessionToken", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("projectFingerprint", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("issuedAtUtc", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("editorMode", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("ownerKind", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("canShutdownProcess", JsonSchemaNode.Value(JsonSchemaType.Boolean))
            .Required("endpointTransportKind", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("endpointAddress", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("processId", JsonSchemaNode.Value(JsonSchemaType.Int32))
            .Required("ownerProcessId", JsonSchemaNode.Value(JsonSchemaType.Int32)),
        allowAdditionalProperties: false);
}
