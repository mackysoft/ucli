using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class StringValueValidatorTests
{
    private static readonly string?[] ValuesWithoutOuterWhitespace =
    [
        null,
        "",
        "value",
        "va lue",
    ];

    private static readonly string[] ValuesWithOuterWhitespace =
    [
        " value",
        "value ",
        " value ",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void HasOuterWhitespace_ReturnsFalse_WhenValueDoesNotHaveOuterWhitespace ()
    {
        foreach (string? value in ValuesWithoutOuterWhitespace)
        {
            var result = StringValueValidator.HasOuterWhitespace(value);

            Assert.False(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasOuterWhitespace_ReturnsTrue_WhenValueHasLeadingOrTrailingWhitespace ()
    {
        foreach (string value in ValuesWithOuterWhitespace)
        {
            var result = StringValueValidator.HasOuterWhitespace(value);

            Assert.True(result);
        }
    }
}
