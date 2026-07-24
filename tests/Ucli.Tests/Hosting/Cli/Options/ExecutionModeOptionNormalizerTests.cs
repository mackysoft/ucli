using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Options;

public sealed class ExecutionModeOptionNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenValueIsOmitted_ReturnsUnspecifiedResult ()
    {
        var result = ExecutionModeOptionNormalizer.Normalize(null);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsSpecified);
        Assert.Null(result.Mode);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("auto", (int)UnityExecutionMode.Auto)]
    [InlineData("AUTO", (int)UnityExecutionMode.Auto)]
    [InlineData("Daemon", (int)UnityExecutionMode.Daemon)]
    [InlineData(" daemon ", (int)UnityExecutionMode.Daemon)]
    [InlineData("oneshot", (int)UnityExecutionMode.Oneshot)]
    [InlineData("OnEShOt", (int)UnityExecutionMode.Oneshot)]
    public void Normalize_WhenValueIsSupported_ReturnsParsedMode (
        string value,
        int expectedMode)
    {
        var result = ExecutionModeOptionNormalizer.Normalize(value);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsSpecified);
        Assert.Equal((UnityExecutionMode)expectedMode, result.Mode);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("one-shot")]
    [InlineData("batch")]
    [InlineData("1")]
    public void Normalize_WhenValueIsUnsupported_ReturnsInvalidArgument (string value)
    {
        var result = ExecutionModeOptionNormalizer.Normalize(value);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsSpecified);
        Assert.Null(result.Mode);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal("Mode must be auto, daemon, or oneshot.", result.Error.Message);
    }
}
