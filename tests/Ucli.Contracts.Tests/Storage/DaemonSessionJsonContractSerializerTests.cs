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
              "schemaVersion": 2,
              "sessionToken": "token-123",
              "projectFingerprint": "fingerprint-abc",
              "issuedAtUtc": "2026-03-02T00:00:00+00:00",
              "editorMode": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-endpoint",
              "processId": 1234,
              "processStartedAtUtc": "2026-03-02T00:00:01+00:00",
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
        Assert.Equal(DateTimeOffset.Parse("2026-03-02T00:00:01+00:00"), contract.ProcessStartedAtUtc);
        Assert.Equal(5678, contract.OwnerProcessId);
        Assert.Null(contract.EditorInstanceId);
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
    public void Deserialize_WhenCanShutdownProcessIsMissing_ThrowsJsonException ()
    {
        const string Json = """
            {
              "schemaVersion": 2,
              "sessionToken": "token-123",
              "projectFingerprint": "fingerprint-abc",
              "issuedAtUtc": "2026-03-02T00:00:00+00:00",
              "editorMode": "gui",
              "ownerKind": "user",
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-endpoint",
              "processId": null,
              "processStartedAtUtc": null,
              "ownerProcessId": 5678
            }
            """;

        var exception = Assert.Throws<JsonException>(() =>
            DaemonSessionJsonContractSerializer.Deserialize(Json));

        Assert.Contains("canShutdownProcess", exception.Message, StringComparison.Ordinal);
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
            ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-02T00:00:01+00:00"),
            OwnerProcessId: 5678)
        {
            EditorInstanceId = "11111111111111111111111111111111",
        };

        var json = DaemonSessionJsonContractSerializer.Serialize(contract);
        using var jsonDocument = JsonDocument.Parse(json);

        JsonAssert.For(jsonDocument.RootElement)
            .MatchesSchema(SessionJsonSchema, nameof(SessionJsonSchema));
        Assert.False(jsonDocument.RootElement.TryGetProperty("runtimeKind", out _));
        Assert.True(jsonDocument.RootElement.TryGetProperty("editorInstanceId", out var editorInstanceId));
        Assert.Equal("11111111111111111111111111111111", editorInstanceId.GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithEditorInstanceId_ReturnsContract ()
    {
        const string Json = """
            {
              "schemaVersion": 2,
              "sessionToken": "token-123",
              "projectFingerprint": "fingerprint-abc",
              "issuedAtUtc": "2026-03-02T00:00:00+00:00",
              "editorMode": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-endpoint",
              "processId": 1234,
              "processStartedAtUtc": "2026-03-02T00:00:01+00:00",
              "ownerProcessId": 5678,
              "editorInstanceId": "11111111111111111111111111111111"
            }
            """;

        var contract = DaemonSessionJsonContractSerializer.Deserialize(Json);

        Assert.NotNull(contract);
        Assert.Equal("11111111111111111111111111111111", contract.EditorInstanceId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WhenEditorInstanceIdIsNull_OmitsEditorInstanceId ()
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
            ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-02T00:00:01+00:00"),
            OwnerProcessId: 5678);

        var json = DaemonSessionJsonContractSerializer.Serialize(contract);
        using var jsonDocument = JsonDocument.Parse(json);

        JsonAssert.For(jsonDocument.RootElement)
            .MatchesSchema(SessionJsonSchema, nameof(SessionJsonSchema));
        Assert.False(jsonDocument.RootElement.TryGetProperty("editorInstanceId", out _));
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
            .Required("processStartedAtUtc", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
            .Required("ownerProcessId", JsonSchemaNode.Value(JsonSchemaType.Int32))
            .Optional("editorInstanceId", JsonSchemaNode.Value(JsonSchemaType.String)),
        allowAdditionalProperties: false);
}
