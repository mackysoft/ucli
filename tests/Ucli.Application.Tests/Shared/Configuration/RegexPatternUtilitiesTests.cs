using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Tests.Configuration;

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
    public void TryValidatePattern_Throws_WhenPatternIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = RegexPatternUtilities.TryValidatePattern(null!, out _);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryIsMatch_ReturnsExpectedMatchResult_ForValidPattern ()
    {
        var success = RegexPatternUtilities.TryIsMatch(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, "^ucli\\.", out var isMatch);

        Assert.True(success);
        Assert.True(isMatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryIsMatch_ReturnsFalse_WhenPatternIsInvalid ()
    {
        var success = RegexPatternUtilities.TryIsMatch(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, "[", out var isMatch);

        Assert.False(success);
        Assert.False(isMatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryIsMatch_ReturnsFalse_WhenPatternMatchTimesOut ()
    {
        var input = new string('a', 4096) + "!";

        var success = RegexPatternUtilities.TryIsMatch(input, "^(a+)+$", out var isMatch);

        Assert.False(success);
        Assert.False(isMatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryIsMatch_Throws_WhenInputIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = RegexPatternUtilities.TryIsMatch(null!, "^ucli\\.", out _);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryIsMatch_Throws_WhenPatternIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = RegexPatternUtilities.TryIsMatch(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, null!, out _);
        });
    }
}
