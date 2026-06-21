using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class IpcBuildOutputLayoutResolverTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("standaloneOSX", "appBundle", "Player.app")]
    [InlineData("standaloneWindows", "file", "Player.exe")]
    [InlineData("standaloneWindows64", "file", "Player.exe")]
    [InlineData("standaloneLinux64", "file", "Player")]
    [InlineData("android", "file", "Player.apk")]
    [InlineData("ios", "directory", "Player")]
    [InlineData("tvos", "directory", "Player")]
    [InlineData("webgl", "directory", "Player")]
    public void TryResolve_WithSupportedTarget_ReturnsCommandDerivedPlayerLayout (
        string buildTarget,
        string expectedShape,
        string expectedFileName)
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));

        var resolved = IpcBuildOutputLayoutResolver.TryResolve(outputDirectory, buildTarget, out var layout);

        Assert.True(resolved);
        Assert.NotNull(layout);
        Assert.Equal(expectedShape, layout!.Shape);
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
        Assert.Equal("file", layout!.Shape);
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
