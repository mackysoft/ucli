using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunPlatformCodecTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("editmode", (int)TestRunPlatform.EditMode)]
    [InlineData("EDITMODE", (int)TestRunPlatform.EditMode)]
    [InlineData("playmode", (int)TestRunPlatform.PlayMode)]
    [InlineData("PlayMode", (int)TestRunPlatform.PlayMode)]
    public void TryParse_WithSupportedLiteral_ReturnsTrue (
        string value,
        int expected)
    {
        var success = TestRunPlatformCodec.TryParse(value, out var parsed);

        Assert.True(success);
        Assert.Equal((TestRunPlatform)expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    public void TryParse_WithUnsupportedLiteral_ReturnsFalse (string? value)
    {
        var success = TestRunPlatformCodec.TryParse(value, out var parsed);

        Assert.False(success);
        Assert.Equal(default, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("editmode", (int)TestRunPlatform.EditMode)]
    [InlineData("PLAYMODE", (int)TestRunPlatform.PlayMode)]
    [InlineData(null, (int)TestRunPlatform.Unknown)]
    [InlineData("", (int)TestRunPlatform.Unknown)]
    [InlineData("unknown", (int)TestRunPlatform.Unknown)]
    public void ParseOrUnknown_ReturnsParsedValueOrUnknown (
        string? value,
        int expected)
    {
        var actual = TestRunPlatformCodec.ParseOrUnknown(value);

        Assert.Equal((TestRunPlatform)expected, actual);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)TestRunPlatform.EditMode, "editmode")]
    [InlineData((int)TestRunPlatform.PlayMode, "playmode")]
    public void ToValue_ReturnsContractLiteral (
        int value,
        string expected)
    {
        var actual = TestRunPlatformCodec.ToValue((TestRunPlatform)value);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)TestRunPlatform.EditMode, "EditMode")]
    [InlineData((int)TestRunPlatform.PlayMode, "PlayMode")]
    public void ToUnityValue_ReturnsUnityLiteral (
        int value,
        string expected)
    {
        var actual = TestRunPlatformCodec.ToUnityValue((TestRunPlatform)value);

        Assert.Equal(expected, actual);
    }
}