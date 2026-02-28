using MackySoft.Ucli.Execution;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class UnityExecutionModeParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WithNullValue_ReturnsAuto ()
    {
        var result = UnityExecutionModeParser.TryParse(null, out var mode);

        Assert.True(result);
        Assert.Equal(UnityExecutionMode.Auto, mode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("", "Auto")]
    [InlineData(" ", "Auto")]
    [InlineData("auto", "Auto")]
    [InlineData("AUTO", "Auto")]
    [InlineData("Daemon", "Daemon")]
    [InlineData(" daemon ", "Daemon")]
    [InlineData("oneshot", "Oneshot")]
    [InlineData("OnEShOt", "Oneshot")]
    public void TryParse_WithSupportedValue_ReturnsParsedMode (
        string value,
        string expectedMode)
    {
        var result = UnityExecutionModeParser.TryParse(value, out var mode);

        Assert.True(result);
        Assert.Equal(expectedMode, mode.ToString());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("one-shot")]
    [InlineData("batch")]
    [InlineData("1")]
    public void TryParse_WithUnsupportedValue_ReturnsFalse (string value)
    {
        var result = UnityExecutionModeParser.TryParse(value, out var mode);

        Assert.False(result);
        Assert.Equal(default, mode);
    }
}
