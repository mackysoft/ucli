using System.Text;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class Base64UrlCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Encode_ProducesUnpaddedBase64UrlText ()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");

        var encoded = Base64UrlCodec.Encode(bytes);

        Assert.Equal("aGVsbG8", encoded);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Encode_Throws_WhenInputArrayIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = Base64UrlCodec.Encode((byte[])null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EncodeAndTryDecode_RoundTripsBytes ()
    {
        var source = new byte[]
        {
            0x00,
            0x10,
            0x20,
            0x30,
            0x40,
            0x50,
            0x60,
            0x70,
            0x80,
            0x90,
            0xA0,
            0xB0,
            0xC0,
            0xD0,
            0xE0,
            0xF0,
        };

        var encoded = Base64UrlCodec.Encode(source);
        var decoded = Base64UrlCodec.TryDecode(encoded, out var bytes);

        Assert.True(decoded);
        Assert.Equal(source, bytes);
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryDecode_ReturnsFalse_WhenInputIsNullOrWhitespace (string? text)
    {
        var result = Base64UrlCodec.TryDecode(text, out var bytes);

        Assert.False(result);
        Assert.Empty(bytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_ReturnsFalse_WhenLengthModuloIsInvalid ()
    {
        var result = Base64UrlCodec.TryDecode("abcde", out _);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_ReturnsFalse_WhenTextContainsInvalidCharacters ()
    {
        var result = Base64UrlCodec.TryDecode("a*bc", out _);

        Assert.False(result);
    }
}