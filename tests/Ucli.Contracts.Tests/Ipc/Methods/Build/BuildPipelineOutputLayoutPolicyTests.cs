using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class BuildPipelineOutputLayoutPolicyTests
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
    public void TryResolve_WithSupportedTarget_ReturnsGuardedPortablePlayerLayout ()
    {
        foreach (var testCase in SupportedBuildOutputLayoutCases)
        {
            var resolved = BuildPipelineOutputLayoutPolicy.TryResolve(
                testCase.BuildTarget,
                androidAppBundle: false,
                out var layout);

            Assert.True(resolved);
            Assert.NotNull(layout);
            Assert.Equal(testCase.ExpectedShape, layout!.Shape);
            Assert.Equal($"player/{testCase.ExpectedFileName}", layout.RunnerOutputPath.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithAndroidAppBundle_ReturnsAabPlayerLayout ()
    {
        var resolved = BuildPipelineOutputLayoutPolicy.TryResolve(
            BuildTargetStableName.Android,
            androidAppBundle: true,
            out var layout);

        Assert.True(resolved);
        Assert.NotNull(layout);
        Assert.Equal(IpcBuildOutputLayoutShape.File, layout!.Shape);
        Assert.Equal("player/Player.aab", layout.RunnerOutputPath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithUnsupportedTarget_ReturnsFalse ()
    {
        foreach (var buildTarget in UnsupportedBuildTargets)
        {
            var resolved = BuildPipelineOutputLayoutPolicy.TryResolve(
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
