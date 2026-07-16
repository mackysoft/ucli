using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildTargetStableNameUnityBuildTargetResolverTests
{
    private static readonly BuildTargetStableNameMapping[] SupportedTargetMappings =
    [
        new(BuildTargetStableName.StandaloneOsx, "StandaloneOSX"),
        new(BuildTargetStableName.StandaloneWindows, "StandaloneWindows"),
        new(BuildTargetStableName.StandaloneWindows64, "StandaloneWindows64"),
        new(BuildTargetStableName.StandaloneLinux64, "StandaloneLinux64"),
        new(BuildTargetStableName.Ios, "iOS"),
        new(BuildTargetStableName.Android, "Android"),
        new(BuildTargetStableName.Webgl, "WebGL"),
        new(BuildTargetStableName.WsaPlayer, "WSAPlayer"),
        new(BuildTargetStableName.Tvos, "tvOS"),
        new(BuildTargetStableName.Switch, "Switch"),
        new(BuildTargetStableName.LinuxHeadlessSimulation, "LinuxHeadlessSimulation"),
        new(BuildTargetStableName.GameCoreXboxSeries, "GameCoreXboxSeries"),
        new(BuildTargetStableName.GameCoreXboxOne, "GameCoreXboxOne"),
        new(BuildTargetStableName.Ps4, "PS4"),
        new(BuildTargetStableName.Ps5, "PS5"),
        new(BuildTargetStableName.XboxOne, "XboxOne"),
        new(BuildTargetStableName.EmbeddedLinux, "EmbeddedLinux"),
        new(BuildTargetStableName.Qnx, "QNX"),
        new(BuildTargetStableName.VisionOs, "VisionOS"),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void SupportedBuildTargetMappings_RoundTripStableAndUnityValues ()
    {
        foreach (var mapping in SupportedTargetMappings)
        {
            Assert.True(BuildTargetStableNameUnityBuildTargetResolver.TryResolve(mapping.StableName, out var unityBuildTargetName));
            Assert.Equal(mapping.UnityBuildTargetName, unityBuildTargetName);
            Assert.True(BuildTargetStableNameUnityBuildTargetResolver.TryResolveStableName(mapping.UnityBuildTargetName, out var stableName));
            Assert.Equal(mapping.StableName, stableName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_CoversEveryDefinedStableBuildTarget ()
    {
        foreach (BuildTargetStableName stableName in Enum.GetValues(typeof(BuildTargetStableName)))
        {
            Assert.True(BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableName, out var unityBuildTargetName));
            Assert.False(string.IsNullOrWhiteSpace(unityBuildTargetName));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnsupportedValues_AreRejected ()
    {
        Assert.False(BuildTargetStableNameUnityBuildTargetResolver.TryResolve(default, out _));
        Assert.False(BuildTargetStableNameUnityBuildTargetResolver.TryResolveStableName("UnknownTarget", out _));
    }

    private sealed record BuildTargetStableNameMapping (
        BuildTargetStableName StableName,
        string UnityBuildTargetName);
}
