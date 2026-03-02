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
    public void IsRootCommandToken_ReturnsFalse_ForCommandToken (string token)
    {
        var result = CommandTokenClassifier.IsRootCommandToken(token);

        Assert.False(result);
    }
}