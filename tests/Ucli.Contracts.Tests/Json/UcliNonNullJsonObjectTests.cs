using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Tests.Json;

public sealed class UcliNonNullJsonObjectTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters =
        {
            new UcliNonNullJsonObjectJsonConverterFactory(),
        },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    [Fact]
    [Trait("Size", "Small")]
    public void Wrap_WithPrimitiveOrArray_IsRejectedAtConstructionByEffectiveSerializerContract ()
    {
        var primitive = Assert.Throws<ArgumentException>(
            () => UcliNonNullJsonObject.Wrap(42, SerializerOptions));
        var array = Assert.Throws<ArgumentException>(
            () => UcliNonNullJsonObject.Wrap(new[] { 1, 2, 3 }, SerializerOptions));

        Assert.Equal("serializerType", primitive.ParamName);
        Assert.Equal("serializerType", array.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Wrap_WithObject_PreservesAuthoritativeSerializerContract ()
    {
        var value = UcliNonNullJsonObject.Wrap(
            new EvidenceData("ready"),
            SerializerOptions);

        var json = JsonSerializer.Serialize(value, SerializerOptions);

        Assert.Equal("""{"State":"ready"}""", json);
    }

    private sealed record EvidenceData (string State);
}
