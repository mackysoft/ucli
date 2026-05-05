using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class UnityExecutionModeCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WithNullValue_ReturnsAuto ()
    {
        var result = UnityExecutionModeCodec.TryParse(null, out var mode);

        Assert.True(result);
        Assert.Equal(UnityExecutionMode.Auto, mode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("auto", (int)UnityExecutionMode.Auto)]
    [InlineData("AUTO", (int)UnityExecutionMode.Auto)]
    [InlineData("Daemon", (int)UnityExecutionMode.Daemon)]
    [InlineData(" daemon ", (int)UnityExecutionMode.Daemon)]
    [InlineData("oneshot", (int)UnityExecutionMode.Oneshot)]
    [InlineData("OnEShOt", (int)UnityExecutionMode.Oneshot)]
    public void TryParse_WithSupportedValue_ReturnsParsedMode (
        string value,
        int expectedMode)
    {
        var result = UnityExecutionModeCodec.TryParse(value, out var mode);

        Assert.True(result);
        Assert.Equal((UnityExecutionMode)expectedMode, mode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("one-shot")]
    [InlineData("batch")]
    [InlineData("1")]
    public void TryParse_WithUnsupportedValue_ReturnsFalse (string value)
    {
        var result = UnityExecutionModeCodec.TryParse(value, out var mode);

        Assert.False(result);
        Assert.Equal(default, mode);
    }
}
