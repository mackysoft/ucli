using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class ContractLiteralJsonConverterFactoryTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters =
        {
            new ContractLiteralJsonConverterFactory(),
        },
    };

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WhenEnumHasContractLiterals_WritesCanonicalLiteral ()
    {
        var json = JsonSerializer.Serialize(OperationPolicy.Advanced, Options);

        Assert.Equal("\"advanced\"", json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WhenLiteralIsCanonical_ReturnsTypedValue ()
    {
        var value = JsonSerializer.Deserialize<OperationPolicy>("\"dangerous\"", Options);

        Assert.Equal(OperationPolicy.Dangerous, value);
    }

    [Theory]
    [InlineData("\"SAFE\"")]
    [InlineData("\" safe \"")]
    [InlineData("\"unsupported\"")]
    [InlineData("0")]
    [Trait("Size", "Small")]
    public void Deserialize_WhenValueIsNotCanonical_ThrowsJsonException (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OperationPolicy>(json, Options));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeDictionary_WhenKeyHasContractLiteral_WritesCanonicalPropertyName ()
    {
        var values = new Dictionary<OperationPolicy, int>
        {
            [OperationPolicy.Advanced] = 1,
        };

        var json = JsonSerializer.Serialize(values, Options);

        Assert.Equal("{\"advanced\":1}", json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeDictionary_WhenKeyIsUndefined_ThrowsJsonException ()
    {
        var values = new Dictionary<OperationPolicy, int>
        {
            [(OperationPolicy)999] = 1,
        };

        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(values, Options));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DeserializeDictionary_WhenPropertyNameIsCanonical_ReturnsTypedKey ()
    {
        var values = JsonSerializer.Deserialize<Dictionary<OperationPolicy, int>>(
            "{\"dangerous\":2}",
            Options);

        Assert.NotNull(values);
        Assert.Single(values);
        Assert.Equal(2, values[OperationPolicy.Dangerous]);
    }

    [Theory]
    [InlineData("{\"SAFE\":1}")]
    [InlineData("{\" safe \":1}")]
    [InlineData("{\"unsupported\":1}")]
    [Trait("Size", "Small")]
    public void DeserializeDictionary_WhenPropertyNameIsNotCanonical_ThrowsJsonException (string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Dictionary<OperationPolicy, int>>(json, Options));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CanConvert_WhenEnumHasNoContractLiteral_ReturnsFalse ()
    {
        var factory = new ContractLiteralJsonConverterFactory();

        Assert.False(factory.CanConvert(typeof(PlainEnum)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CanConvert_WhenRecognizedEnumHasUnmappedMember_ThrowsInvalidOperationException ()
    {
        var factory = new ContractLiteralJsonConverterFactory();

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CanConvert(typeof(PartiallyMappedEnum)));

        Assert.Contains("missing UcliContractLiteralAttribute", exception.Message, StringComparison.Ordinal);
    }

    private enum PlainEnum
    {
        Value = 0,
    }

    private enum PartiallyMappedEnum
    {
        [UcliContractLiteral("mapped")]
        Mapped = 0,

        Unmapped = 1,
    }
}
