using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Tests.CodeCatalog;

public sealed class UcliCodeTests
{
    private static readonly string[] ValidCodeValues =
    [
        "IPC_TIMEOUT",
        "UNITY_COMPILE_NO_ERRORS",
        "A1_B2",
        "SOME.FUTURE_CODE",
    ];

    private static readonly string?[] InvalidCodeValues =
    [
        null,
        string.Empty,
        " ",
        "lowercase_code",
        "Code",
        "CODE-WITH-HYPHEN",
        "1_CODE",
        ".CODE",
        "CODE.",
        "A..B",
        "ERROR.2ND_PHASE",
        new string('A', UcliCode.MaximumLength + 1),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidValue_WithValidCode_ReturnsTrue ()
    {
        foreach (var value in ValidCodeValues)
        {
            Assert.True(UcliCode.IsValidValue(value));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValidCode_ExposesRawValueExplicitly ()
    {
        var code = new UcliCode("UNITY_READY_EXECUTION");

        Assert.Equal("UNITY_READY_EXECUTION", code.Value);
        Assert.Equal("UNITY_READY_EXECUTION", code.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnknownValidCode_UsesValueEquality ()
    {
        UcliCode code = new("FUTURE_DAEMON_FAILURE");

        Assert.Equal("FUTURE_DAEMON_FAILURE", code.Value);
        Assert.Equal("FUTURE_DAEMON_FAILURE", code.ToString());
        Assert.Equal(new UcliCode("FUTURE_DAEMON_FAILURE"), code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonSerialization_WithValidCode_RoundTripsStringValue ()
    {
        var code = new UcliCode("SOME.FUTURE_CODE");

        var json = JsonSerializer.Serialize(code);
        var deserialized = JsonSerializer.Deserialize<UcliCode>(json);

        Assert.Equal("\"SOME.FUTURE_CODE\"", json);
        Assert.Equal(code, deserialized);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonSerialization_WithNullNullableCode_WritesNull ()
    {
        UcliCode? code = null;

        var json = JsonSerializer.Serialize(code);
        var deserialized = JsonSerializer.Deserialize<UcliCode?>(json);

        Assert.Equal("null", json);
        Assert.Null(deserialized);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("42")]
    [InlineData("\"lowercase_code\"")]
    public void JsonDeserialization_WithInvalidCode_ThrowsJsonException (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UcliCode>(json));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithValidValue_ReturnsCode ()
    {
        var result = UcliCode.TryCreate("UNITY_READY_MUTATION", out var code);

        Assert.True(result);
        Assert.NotNull(code);
        Assert.Equal("UNITY_READY_MUTATION", code.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConstructionModel_UsesPrivateValidatedValueConstructor ()
    {
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InvalidCodeValues_AreRejectedByAllConstructionPaths ()
    {
        foreach (var value in InvalidCodeValues)
        {
            Assert.False(UcliCode.IsValidValue(value));

            var result = UcliCode.TryCreate(value, out var code);
            Assert.False(result);
            Assert.Null(code);

            Assert.Throws<ArgumentException>(() => new UcliCode(value!));
        }
    }
}
