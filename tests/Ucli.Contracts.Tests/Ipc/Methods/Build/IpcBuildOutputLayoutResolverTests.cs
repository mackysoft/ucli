using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class IpcBuildOutputLayoutResolverTests
{
    private static readonly SupportedBuildOutputLayoutCase[] SupportedBuildOutputLayoutCases =
    [
        new(BuildTargetStableName.StandaloneOsx, IpcBuildOutputLayoutShape.AppBundle, ExpectedFileName: "Player.app"),
        new(BuildTargetStableName.StandaloneWindows, IpcBuildOutputLayoutShape.File, ExpectedFileName: "Player.exe"),
        new(BuildTargetStableName.StandaloneWindows64, IpcBuildOutputLayoutShape.File, ExpectedFileName: "Player.exe"),
        new(BuildTargetStableName.StandaloneLinux64, IpcBuildOutputLayoutShape.File, ExpectedFileName: "Player"),
        new(BuildTargetStableName.Android, IpcBuildOutputLayoutShape.File, ExpectedFileName: "Player.apk"),
        new(BuildTargetStableName.Ios, IpcBuildOutputLayoutShape.Directory, ExpectedFileName: "Player"),
        new(BuildTargetStableName.Tvos, IpcBuildOutputLayoutShape.Directory, ExpectedFileName: "Player"),
        new(BuildTargetStableName.Webgl, IpcBuildOutputLayoutShape.Directory, ExpectedFileName: "Player"),
    ];

    private static readonly BuildTargetStableName[] UnsupportedBuildTargets =
    [
        BuildTargetStableName.Switch,
        default,
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithSupportedTarget_ReturnsCommandDerivedPlayerLayout ()
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));

        foreach (var testCase in SupportedBuildOutputLayoutCases)
        {
            var resolved = IpcBuildOutputLayoutResolver.TryResolve(
                outputDirectory,
                testCase.BuildTarget,
                androidAppBundle: false,
                out var layout);

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
            BuildTargetStableName.Android,
            androidAppBundle: true,
            out var layout);

        Assert.True(resolved);
        Assert.NotNull(layout);
        Assert.Equal(IpcBuildOutputLayoutShape.File, layout!.Shape);
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
            var resolved = IpcBuildOutputLayoutResolver.TryResolve(
                outputDirectory,
                buildTarget,
                androidAppBundle: false,
                out var layout);

            Assert.False(resolved);
            Assert.Null(layout);
        }
    }

    private sealed record SupportedBuildOutputLayoutCase (
        BuildTargetStableName BuildTarget,
        IpcBuildOutputLayoutShape ExpectedShape,
        string ExpectedFileName);
}
