using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildTargetStableNameUnityBuildTargetResolverTests
{
    private static readonly BuildTargetStableNameMapping[] SupportedTargetMappings =
    [
        new("standaloneOSX", BuildTargetStableName.StandaloneOsx, "StandaloneOSX"),
        new("standaloneWindows", BuildTargetStableName.StandaloneWindows, "StandaloneWindows"),
        new("standaloneWindows64", BuildTargetStableName.StandaloneWindows64, "StandaloneWindows64"),
        new("standaloneLinux64", BuildTargetStableName.StandaloneLinux64, "StandaloneLinux64"),
        new("ios", BuildTargetStableName.Ios, "iOS"),
        new("android", BuildTargetStableName.Android, "Android"),
        new("webgl", BuildTargetStableName.Webgl, "WebGL"),
        new("wsaPlayer", BuildTargetStableName.WsaPlayer, "WSAPlayer"),
        new("tvos", BuildTargetStableName.Tvos, "tvOS"),
        new("switch", BuildTargetStableName.Switch, "Switch"),
        new("linuxHeadlessSimulation", BuildTargetStableName.LinuxHeadlessSimulation, "LinuxHeadlessSimulation"),
        new("gameCoreXboxSeries", BuildTargetStableName.GameCoreXboxSeries, "GameCoreXboxSeries"),
        new("gameCoreXboxOne", BuildTargetStableName.GameCoreXboxOne, "GameCoreXboxOne"),
        new("ps4", BuildTargetStableName.Ps4, "PS4"),
        new("ps5", BuildTargetStableName.Ps5, "PS5"),
        new("xboxOne", BuildTargetStableName.XboxOne, "XboxOne"),
        new("embeddedLinux", BuildTargetStableName.EmbeddedLinux, "EmbeddedLinux"),
        new("qnx", BuildTargetStableName.Qnx, "QNX"),
        new("visionOS", BuildTargetStableName.VisionOs, "VisionOS"),
    ];

    private static readonly string[] UnsupportedStableNames =
    [
        "",
        "unknownTarget",
    ];

    private static readonly string[] UnsupportedUnityBuildTargetLiterals =
    [
        "",
        "UnknownTarget",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void SupportedBuildTargetMappings_RoundTripStableNameAndUnityBuildTargetLiteral ()
    {
        foreach (var mapping in SupportedTargetMappings)
        {
            var stringResolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolve(
                mapping.StableNameLiteral,
                out var stringLiteral);
            var enumResolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolve(
                mapping.StableName,
                out var enumLiteral);
            var stableNameResolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolveStableName(
                mapping.UnityBuildTargetLiteral,
                out var resolvedStableName);

            Assert.True(stringResolved, $"{mapping.StableNameLiteral} must resolve from string literal.");
            Assert.Equal(mapping.UnityBuildTargetLiteral, stringLiteral);
            Assert.True(enumResolved, $"{mapping.StableName} must resolve from enum value.");
            Assert.Equal(mapping.UnityBuildTargetLiteral, enumLiteral);
            Assert.True(stableNameResolved, $"{mapping.UnityBuildTargetLiteral} must resolve back to stable name.");
            Assert.Equal(mapping.StableName, resolvedStableName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_SupportsEveryBuildTargetStableNameLiteral ()
    {
        foreach (var stableNameValue in Enum.GetValues<BuildTargetStableName>())
        {
            var stableName = ContractLiteralCodec.ToValue(stableNameValue);

            var resolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableName, out var unityBuildTargetLiteral);

            Assert.True(resolved, $"Build target stable name '{stableName}' must resolve.");
            Assert.False(string.IsNullOrWhiteSpace(unityBuildTargetLiteral));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithUnsupportedStableName_ReturnsFalse ()
    {
        foreach (string stableName in UnsupportedStableNames)
        {
            var resolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableName, out var unityBuildTargetLiteral);

            Assert.False(resolved);
            Assert.Null(unityBuildTargetLiteral);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolveStableName_WithUnsupportedUnityBuildTargetLiteral_ReturnsFalse ()
    {
        foreach (string unityBuildTargetLiteral in UnsupportedUnityBuildTargetLiterals)
        {
            var resolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolveStableName(unityBuildTargetLiteral, out var stableName);

            Assert.False(resolved);
            Assert.Equal(default, stableName);
        }
    }

    private sealed record BuildTargetStableNameMapping (
        string StableNameLiteral,
        BuildTargetStableName StableName,
        string UnityBuildTargetLiteral);
}
