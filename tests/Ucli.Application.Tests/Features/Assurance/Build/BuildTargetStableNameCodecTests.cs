using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildTargetStableNameCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithSupportedTarget_ReturnsResolvedTarget ()
    {
        const string stableName = "standaloneLinux64";

        var resolved = BuildTargetStableNameCodec.TryResolve(stableName, out var target);

        Assert.True(resolved);
        Assert.Equal(BuildTargetStableName.StandaloneLinux64, target.StableNameValue);
        Assert.Equal(stableName, target.StableName);
        Assert.Equal("StandaloneLinux64", target.UnityBuildTargetLiteral);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithUnsupportedTarget_ReturnsFalse ()
    {
        var resolved = BuildTargetStableNameCodec.TryResolve("unknownTarget", out var target);

        Assert.False(resolved);
        Assert.Null(target);
    }
}
