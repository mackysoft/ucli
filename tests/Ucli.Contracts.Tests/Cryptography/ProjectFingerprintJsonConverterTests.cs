using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Tests.Cryptography;

public sealed class ProjectFingerprintJsonConverterTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    public static TheoryData<string> InvalidFingerprintValues => new()
    {
        string.Empty,
        new string('a', 63),
        new string('a', 65),
        "0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef",
        "g123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    };

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WritesCanonicalJsonString ()
    {
        var json = JsonSerializer.Serialize(new ProjectFingerprint(Fingerprint));

        Assert.Equal($"\"{Fingerprint}\"", json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithCanonicalJsonString_ReturnsFingerprint ()
    {
        var fingerprint = JsonSerializer.Deserialize<ProjectFingerprint>($"\"{Fingerprint}\"");

        Assert.Equal(new ProjectFingerprint(Fingerprint), fingerprint);
    }

    [Theory]
    [MemberData(nameof(InvalidFingerprintValues))]
    [Trait("Size", "Small")]
    public void Deserialize_WithNonCanonicalJsonString_ThrowsJsonException (string value)
    {
        var json = JsonSerializer.Serialize(value);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectFingerprint>(json));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [Trait("Size", "Small")]
    public void Deserialize_WithNonStringToken_ThrowsJsonException (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectFingerprint>(json));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WithNull_ThrowsJsonException ()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize<ProjectFingerprint>(null!));
    }
}
