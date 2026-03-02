using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class StringValueNormalizerTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void TrimToNull_ReturnsNull_WhenValueIsNullOrWhitespace (string? value)
    {
        var result = StringValueNormalizer.TrimToNull(value);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimToNull_ReturnsTrimmedValue_WhenValueContainsNonWhitespaceCharacters ()
    {
        var result = StringValueNormalizer.TrimToNull("  value  ");

        Assert.Equal("value", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimOrEmpty_ReturnsEmpty_WhenValueIsNull ()
    {
        var result = StringValueNormalizer.TrimOrEmpty(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimOrFallback_ReturnsFallback_WhenValueIsWhitespace ()
    {
        var result = StringValueNormalizer.TrimOrFallback("   ", "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimOrFallback_ReturnsTrimmedValue_WhenValueHasContent ()
    {
        var result = StringValueNormalizer.TrimOrFallback("  keep  ", "fallback");

        Assert.Equal("keep", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimOrFallback_Throws_WhenFallbackIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = StringValueNormalizer.TrimOrFallback("value", null!);
        });
    }
}