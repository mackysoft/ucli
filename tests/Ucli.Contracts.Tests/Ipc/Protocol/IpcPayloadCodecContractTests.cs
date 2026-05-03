using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcPayloadCodecContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void SerializeToElement_UsesSharedCamelCaseNaming ()
    {
        var payload = new PayloadEnvelope(ServerVersion: "v1");

        var jsonElement = IpcPayloadCodec.SerializeToElement(payload);

        JsonAssert.For(jsonElement)
            .MatchesSchema(PayloadEnvelopeSchema, nameof(PayloadEnvelope))
            .HasString("serverVersion", "v1");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WithValidPayload_ReturnsModel ()
    {
        using var document = JsonDocument.Parse("""{"serverVersion":"v1"}""");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out PayloadEnvelope? payload,
            out var error);

        Assert.True(result);
        Assert.NotNull(payload);
        Assert.Equal("v1", payload.ServerVersion);
        Assert.Equal(IpcPayloadReadErrorKind.None, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WithNullLiteral_ReturnsNullPayloadError ()
    {
        using var document = JsonDocument.Parse("null");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out PayloadEnvelope? payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.Equal(IpcPayloadReadErrorKind.NullPayload, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WithInvalidShape_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse("""{"serverVersion":123}""");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out PayloadEnvelope? payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WithDuplicatedObjectProperty_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse("""{"serverVersion":"v1","serverVersion":"v2"}""");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out PayloadEnvelope? payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
        Assert.Contains("$.serverVersion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WithCaseVariantDuplicatedObjectProperty_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse("""{"serverVersion":"v1","ServerVersion":"v2"}""");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out PayloadEnvelope? payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
        Assert.Contains("$.ServerVersion", error.Message, StringComparison.Ordinal);
    }

    private sealed record PayloadEnvelope (string ServerVersion);

    private static JsonSchemaNode PayloadEnvelopeSchema => JsonSchemaNode.Object(
        builder => builder.Required("serverVersion", JsonSchemaNode.Value(JsonSchemaType.String)),
        allowAdditionalProperties: false);
}
