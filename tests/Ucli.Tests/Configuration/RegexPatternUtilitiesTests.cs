using MackySoft.Ucli.Configuration;

namespace MackySoft.Ucli.Tests.Configuration;

public sealed class RegexPatternUtilitiesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePattern_ReturnsTrue_ForValidPattern ()
    {
        var result = RegexPatternUtilities.TryValidatePattern("^ucli\\.", out var errorMessage);

        Assert.True(result);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePattern_ReturnsFalse_ForInvalidPattern ()
    {
        var result = RegexPatternUtilities.TryValidatePattern("[", out var errorMessage);

        Assert.False(result);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryIsMatch_ReturnsExpectedMatchResult_ForValidPattern ()
    {
        var success = RegexPatternUtilities.TryIsMatch("ucli.scene.open", "^ucli\\.", out var isMatch);

        Assert.True(success);
        Assert.True(isMatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryIsMatch_ReturnsFalse_WhenPatternIsInvalid ()
    {
        var success = RegexPatternUtilities.TryIsMatch("ucli.scene.open", "[", out var isMatch);

        Assert.False(success);
        Assert.False(isMatch);
    }
}