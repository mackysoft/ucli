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
}
