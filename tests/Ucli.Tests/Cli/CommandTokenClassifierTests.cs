using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class CommandTokenClassifierTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void IsRootCommandToken_ReturnsTrue_ForEmptyOrOptionToken (string? token)
    {
        var result = CommandTokenClassifier.IsRootCommandToken(token);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("status")]
    [InlineData("test")]
    [InlineData("init")]
    [InlineData("daemon")]
    public void IsRootCommandToken_ReturnsFalse_ForCommandToken (string token)
    {
        var result = CommandTokenClassifier.IsRootCommandToken(token);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("--help", true)]
    [InlineData("-h", true)]
    [InlineData("--unknown", false)]
    [InlineData("status", false)]
    [InlineData(null, false)]
    public void IsHelpOptionToken_ReturnsExpectedResult (string? token, bool expected)
    {
        var result = CommandTokenClassifier.IsHelpOptionToken(token);

        Assert.Equal(expected, result);
    }
}