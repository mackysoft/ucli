using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

public sealed class UnityExecutionTargetModeMapperTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)UnityExecutionTarget.Daemon, (int)UnityExecutionMode.Daemon)]
    [InlineData((int)UnityExecutionTarget.Oneshot, (int)UnityExecutionMode.Oneshot)]
    public void ToExplicitMode_WithResolvedTarget_ReturnsMatchingExplicitMode (
        int targetValue,
        int expectedModeValue)
    {
        var mode = UnityExecutionTargetModeMapper.ToExplicitMode((UnityExecutionTarget)targetValue);

        Assert.Equal((UnityExecutionMode)expectedModeValue, mode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToExplicitMode_WithUnsupportedTarget_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UnityExecutionTargetModeMapper.ToExplicitMode((UnityExecutionTarget)int.MaxValue));
    }
}
