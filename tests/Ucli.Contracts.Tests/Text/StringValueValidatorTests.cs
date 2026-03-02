using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class StringValueValidatorTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("value", false)]
    [InlineData(" value", true)]
    [InlineData("value ", true)]
    [InlineData(" value ", true)]
    [InlineData("va lue", false)]
    public void HasOuterWhitespace_ReturnsExpectedResult (
        string? value,
        bool expected)
    {
        var result = StringValueValidator.HasOuterWhitespace(value);

        Assert.Equal(expected, result);
    }
}