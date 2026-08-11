using MackySoft.Ucli.Application.Features.Recording.Capability;

namespace MackySoft.Ucli.Application.Tests.Features.Recording.Capability;

public sealed class GameViewRecorderVersionCompatibilityTests
{
    [Theory]
    [InlineData("5.1.5", true)]
    [InlineData("5.1.9", true)]
    [InlineData("5.1.4", false)]
    [InlineData("5.2.0", false)]
    public void TryEvaluate_ResolvedRelease_EvaluatesBundledRange (
        string version,
        bool expectedSupported)
    {
        var evaluated = GameViewRecorderVersionCompatibility.TryEvaluate(
            version,
            out var supported);

        Assert.True(evaluated);
        Assert.Equal(expectedSupported, supported);
    }

    [Theory]
    [InlineData("5.1.5-pre.1")]
    [InlineData("5.1.5+build.1")]
    [InlineData("5.1")]
    [InlineData(" 5.1.5")]
    public void TryEvaluate_UnrecognizedPackageVersion_IsIndeterminate (string version)
    {
        Assert.False(GameViewRecorderVersionCompatibility.TryEvaluate(version, out _));
    }
}
