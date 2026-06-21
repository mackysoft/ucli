using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildTargetStableNameUnityBuildTargetResolverTests
{
    [Theory]
    [MemberData(nameof(SupportedTargetCases))]
    [Trait("Size", "Small")]
    public void TryResolve_WithSupportedStableName_ReturnsUnityBuildTargetLiteral (
        string stableName,
        BuildTargetStableName stableNameValue,
        string expectedUnityBuildTargetLiteral)
    {
        var stringResolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableName, out var stringLiteral);
        var enumResolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableNameValue, out var enumLiteral);

        Assert.True(stringResolved);
        Assert.Equal(expectedUnityBuildTargetLiteral, stringLiteral);
        Assert.True(enumResolved);
        Assert.Equal(expectedUnityBuildTargetLiteral, enumLiteral);
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

    [Theory]
    [InlineData("")]
    [InlineData("unknownTarget")]
    [Trait("Size", "Small")]
    public void TryResolve_WithUnsupportedStableName_ReturnsFalse (string stableName)
    {
        var resolved = BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableName, out var unityBuildTargetLiteral);

        Assert.False(resolved);
        Assert.Null(unityBuildTargetLiteral);
    }

    public static TheoryData<string, BuildTargetStableName, string> SupportedTargetCases ()
    {
        return new TheoryData<string, BuildTargetStableName, string>
        {
            { "standaloneOSX", BuildTargetStableName.StandaloneOsx, "StandaloneOSX" },
            { "standaloneWindows", BuildTargetStableName.StandaloneWindows, "StandaloneWindows" },
            { "standaloneWindows64", BuildTargetStableName.StandaloneWindows64, "StandaloneWindows64" },
            { "standaloneLinux64", BuildTargetStableName.StandaloneLinux64, "StandaloneLinux64" },
            { "ios", BuildTargetStableName.Ios, "iOS" },
            { "android", BuildTargetStableName.Android, "Android" },
            { "webgl", BuildTargetStableName.Webgl, "WebGL" },
            { "wsaPlayer", BuildTargetStableName.WsaPlayer, "WSAPlayer" },
            { "tvos", BuildTargetStableName.Tvos, "tvOS" },
            { "switch", BuildTargetStableName.Switch, "Switch" },
            { "linuxHeadlessSimulation", BuildTargetStableName.LinuxHeadlessSimulation, "LinuxHeadlessSimulation" },
            { "gameCoreXboxSeries", BuildTargetStableName.GameCoreXboxSeries, "GameCoreXboxSeries" },
            { "gameCoreXboxOne", BuildTargetStableName.GameCoreXboxOne, "GameCoreXboxOne" },
            { "ps4", BuildTargetStableName.Ps4, "PS4" },
            { "ps5", BuildTargetStableName.Ps5, "PS5" },
            { "xboxOne", BuildTargetStableName.XboxOne, "XboxOne" },
            { "embeddedLinux", BuildTargetStableName.EmbeddedLinux, "EmbeddedLinux" },
            { "qnx", BuildTargetStableName.Qnx, "QNX" },
            { "visionOS", BuildTargetStableName.VisionOs, "VisionOS" },
        };
    }
}
