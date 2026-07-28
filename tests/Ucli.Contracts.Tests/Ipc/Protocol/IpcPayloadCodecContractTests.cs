using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

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
            .HasString("serverVersion", "v1");
        Assert.Single(jsonElement.EnumerateObject());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SerializePublicRawOperationResultToElement_UsesRegisteredObjectContract ()
    {
        var result = IpcPayloadCodec.SerializePublicRawOperationResultToElement(
            new PayloadEnvelope(ServerVersion: "v1"));

        JsonAssert.For(result)
            .HasString("serverVersion", "v1");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SerializePublicRawOperationResultToElement_WithNullResult_RejectsPayload ()
    {
        Assert.Throws<ArgumentNullException>(
            () => IpcPayloadCodec.SerializePublicRawOperationResultToElement<PayloadEnvelope>(null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SemanticStringValue_RoundTripsAsJsonString ()
    {
        using var document = JsonDocument.Parse("{\"path\":\"Assets/Scenes/Main.unity\"}");

        var result = IpcPayloadCodec.TryDeserialize<ScenePathArgs>(
            document.RootElement,
            out var args,
            out var error);

        Assert.True(result, error.Message);
        Assert.Equal("Assets/Scenes/Main.unity", args.Path.Value);

        var payload = IpcPayloadCodec.SerializeToElement(args);

        JsonAssert.For(payload)
            .HasString("path", "Assets/Scenes/Main.unity");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssetReferenceArgs_RoundTripsAssetGuidVariantAsStandardJsonGuid ()
    {
        var expectedAssetGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        AssetReferenceArgs expected = new AssetGuidReferenceArgs(expectedAssetGuid);
        var payload = IpcPayloadCodec.SerializeToElement(expected);

        var result = IpcPayloadCodec.TryDeserialize<AssetReferenceArgs>(
            payload,
            out var args,
            out var error);

        Assert.True(result, error.Message);
        var assetGuidReference = Assert.IsType<AssetGuidReferenceArgs>(args);
        Assert.Equal(expectedAssetGuid, assetGuidReference.AssetGuid);

        JsonAssert.For(payload)
            .HasString(
                UcliOperationContractPropertyNames.Kind,
                TextVocabulary.GetText(UcliReferenceKind.AssetGuid))
            .HasString("assetGuid", "11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSelectorArgs_WhenJsonAssetGuidIsEmpty_ReturnsDeserializeFailed ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            kind = TextVocabulary.GetText(UcliReferenceKind.AssetGuid),
            assetGuid = Guid.Empty,
        });

        var result = IpcPayloadCodec.TryDeserialize<ResolveSelectorArgs>(
            payload,
            out var args,
            out var error);

        Assert.False(result);
        Assert.Null(args);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
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
    public void TryDeserialize_WithNullLiteralForStructPayload_ReturnsNullPayloadError ()
    {
        using var document = JsonDocument.Parse("null");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out StructPayload payload,
            out var error);

        Assert.False(result);
        Assert.Equal(default, payload);
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

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserializeStrict_WithCaseVariantProperty_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse("""{"ServerVersion":"v1"}""");

        var result = IpcPayloadCodec.TryDeserializeStrict(
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
    public void TryDeserializeStrict_WithRequestLocalAliasVariant_ReturnsAlias ()
    {
        using var document = JsonDocument.Parse(
            $$"""{"kind":"{{TextVocabulary.GetText(UcliReferenceKind.Alias)}}","var":"created"}""");

        var result = IpcPayloadCodec.TryDeserializeStrict<AssetReferenceArgs>(
            document.RootElement,
            out var reference,
            out var error);

        Assert.True(result, error.Message);
        Assert.IsType<UcliAliasReferenceArgs>(reference);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserializePublicRawOperationArgs_WithRequestLocalAliasVariant_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse(
            $$"""{"kind":"{{TextVocabulary.GetText(UcliReferenceKind.Alias)}}","var":"created"}""");

        var result = IpcPayloadCodec.TryDeserializePublicRawOperationArgs<AssetReferenceArgs>(
            document.RootElement,
            out var reference,
            out var error);

        Assert.False(result);
        Assert.Null(reference);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("null", IpcPayloadReadErrorKind.NullPayload)]
    [InlineData("[]", IpcPayloadReadErrorKind.DeserializeFailed)]
    [InlineData("\"text\"", IpcPayloadReadErrorKind.DeserializeFailed)]
    public void TryDeserializePublicRawOperationArgs_WithNonObjectRoot_RejectsPayload (
        string json,
        IpcPayloadReadErrorKind expectedErrorKind)
    {
        using var document = JsonDocument.Parse(json);

        var result = IpcPayloadCodec.TryDeserializePublicRawOperationArgs<ScenePathArgs>(
            document.RootElement,
            out var args,
            out var error);

        Assert.False(result);
        Assert.Null(args);
        Assert.Equal(expectedErrorKind, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserializePublicRawOperationArgs_WithNestedDiscriminatorAfterDataProperties_ReturnsModel ()
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "sets": [
                {
                  "value": true,
                  "path": "m_Enabled"
                }
              ],
              "target": {
                "assetPath": "Assets/Data.asset",
                "kind": "{{Vocabulary.GetText(UcliReferenceKind.AssetPath)}}"
              }
            }
            """);

        var result = IpcPayloadCodec.TryDeserializePublicRawOperationArgs<AssetSetArgs>(
            document.RootElement,
            out var args,
            out var error);

        Assert.True(result, error.Message);
        var target = Assert.IsType<AssetPathReferenceArgs>(args.Target);
        Assert.Equal("Assets/Data.asset", target.AssetPath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WhenSemanticStringValueRejectsInput_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse("""{"value":"bad"}""");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out RejectingValueEnvelope? payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
        Assert.Contains("Rejected value.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WhenRuntimeWrapsContractRejection_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse("{} ");

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out RuntimeWrappedContract? payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
        Assert.Equal("Rejected wrapped contract.", error.Message);
    }

    private sealed record PayloadEnvelope (string ServerVersion);

    private sealed record RejectingValueEnvelope (RejectingStringValue Value);

    [JsonConverter(typeof(RuntimeWrappedContractJsonConverter))]
    private sealed record RuntimeWrappedContract;

    private readonly record struct StructPayload (string ServerVersion);

    private sealed class RejectingStringValue : UcliStringValue
    {
        public RejectingStringValue (string value)
            : base(value)
        {
            throw new ArgumentException("Rejected value.", nameof(value));
        }
    }

    private sealed class RuntimeWrappedContractJsonConverter : JsonConverter<RuntimeWrappedContract>
    {
        public override RuntimeWrappedContract Read (
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new TargetInvocationException(new ArgumentException("Rejected wrapped contract."));
        }

        public override void Write (
            Utf8JsonWriter writer,
            RuntimeWrappedContract value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
