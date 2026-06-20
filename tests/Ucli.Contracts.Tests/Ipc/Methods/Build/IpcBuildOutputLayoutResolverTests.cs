using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class IpcBuildOutputLayoutResolverTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("standaloneOSX", IpcBuildOutputLayoutShape.AppBundle, "Player.app")]
    [InlineData("standaloneWindows", IpcBuildOutputLayoutShape.File, "Player.exe")]
    [InlineData("standaloneWindows64", IpcBuildOutputLayoutShape.File, "Player.exe")]
    [InlineData("standaloneLinux64", IpcBuildOutputLayoutShape.File, "Player")]
    [InlineData("android", IpcBuildOutputLayoutShape.File, "Player.apk")]
    [InlineData("ios", IpcBuildOutputLayoutShape.Directory, "Player")]
    [InlineData("tvos", IpcBuildOutputLayoutShape.Directory, "Player")]
    [InlineData("webgl", IpcBuildOutputLayoutShape.Directory, "Player")]
    public void TryResolve_WithSupportedTarget_ReturnsCommandDerivedPlayerLayout (
        string buildTarget,
        IpcBuildOutputLayoutShape expectedShape,
        string expectedFileName)
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));

        var resolved = IpcBuildOutputLayoutResolver.TryResolve(outputDirectory, buildTarget, out var layout);

        Assert.True(resolved);
        Assert.NotNull(layout);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedShape), layout!.Shape);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(outputDirectory, "player", expectedFileName)),
            Path.GetFullPath(layout.LocationPathName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithAndroidAppBundle_ReturnsAabPlayerLayout ()
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));

        var resolved = IpcBuildOutputLayoutResolver.TryResolve(
            outputDirectory,
            "android",
            true,
            out var layout);

        Assert.True(resolved);
        Assert.NotNull(layout);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File), layout!.Shape);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(outputDirectory, "player", "Player.aab")),
            Path.GetFullPath(layout.LocationPathName));
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("unknownTarget")]
    [Trait("Size", "Small")]
    public void TryResolve_WithUnsupportedTarget_ReturnsFalse (string buildTarget)
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));

        var resolved = IpcBuildOutputLayoutResolver.TryResolve(outputDirectory, buildTarget, out var layout);

        Assert.False(resolved);
        Assert.Null(layout);
    }
}
