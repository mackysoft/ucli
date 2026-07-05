using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class IpcBuildOutputLayoutResolverTests
{
    private static readonly SupportedBuildOutputLayoutCase[] SupportedBuildOutputLayoutCases =
    [
        new("standaloneOSX", ExpectedShape: "appBundle", ExpectedFileName: "Player.app"),
        new("standaloneWindows", ExpectedShape: "file", ExpectedFileName: "Player.exe"),
        new("standaloneWindows64", ExpectedShape: "file", ExpectedFileName: "Player.exe"),
        new("standaloneLinux64", ExpectedShape: "file", ExpectedFileName: "Player"),
        new("android", ExpectedShape: "file", ExpectedFileName: "Player.apk"),
        new("ios", ExpectedShape: "directory", ExpectedFileName: "Player"),
        new("tvos", ExpectedShape: "directory", ExpectedFileName: "Player"),
        new("webgl", ExpectedShape: "directory", ExpectedFileName: "Player"),
    ];

    private static readonly string[] UnsupportedBuildTargets =
    [
        "switch",
        "unknownTarget",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithSupportedTarget_ReturnsCommandDerivedPlayerLayout ()
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));

        foreach (var testCase in SupportedBuildOutputLayoutCases)
        {
            var resolved = IpcBuildOutputLayoutResolver.TryResolve(outputDirectory, testCase.BuildTarget, out var layout);

            Assert.True(resolved);
            Assert.NotNull(layout);
            Assert.Equal(testCase.ExpectedShape, layout!.Shape);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(outputDirectory, "player", testCase.ExpectedFileName)),
                Path.GetFullPath(layout.LocationPathName));
        }
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

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithUnsupportedTarget_ReturnsFalse ()
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));

        foreach (var buildTarget in UnsupportedBuildTargets)
        {
            var resolved = IpcBuildOutputLayoutResolver.TryResolve(outputDirectory, buildTarget, out var layout);

            Assert.False(resolved);
            Assert.Null(layout);
        }
    }

    private sealed record SupportedBuildOutputLayoutCase (
        string BuildTarget,
        string ExpectedShape,
        string ExpectedFileName);
}
