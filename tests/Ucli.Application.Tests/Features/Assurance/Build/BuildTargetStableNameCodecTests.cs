using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildTargetStableNameCodecTests
{
    [Theory]
    [MemberData(nameof(SupportedTargetCases))]
    [Trait("Size", "Small")]
    public void TryResolve_WithSupportedTarget_ReturnsResolvedTarget (
        string stableName,
        string expectedStableNameValueName,
        string expectedUnityBuildTargetLiteral)
    {
        var expectedStableNameValue = Enum.Parse<BuildTargetStableName>(expectedStableNameValueName);

        var resolved = BuildTargetStableNameCodec.TryResolve(stableName, out var target);

        Assert.True(resolved);
        Assert.Equal(expectedStableNameValue, target.StableNameValue);
        Assert.Equal(stableName, target.StableName);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedStableNameValue), target.StableName);
        Assert.Equal(expectedUnityBuildTargetLiteral, target.UnityBuildTargetLiteral);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_SupportsEveryBuildTargetStableNameLiteral ()
    {
        foreach (var stableName in ContractLiteralCodec.GetLiterals<BuildTargetStableName>())
        {
            var resolved = BuildTargetStableNameCodec.TryResolve(stableName, out var target);

            Assert.True(resolved, $"Build target stable name '{stableName}' must resolve.");
            Assert.Equal(stableName, target.StableName);
        }
    }

    public static TheoryData<string, string, string> SupportedTargetCases ()
    {
        return new TheoryData<string, string, string>
        {
            { "standaloneOSX", nameof(BuildTargetStableName.StandaloneOsx), "StandaloneOSX" },
            { "standaloneWindows", nameof(BuildTargetStableName.StandaloneWindows), "StandaloneWindows" },
            { "standaloneWindows64", nameof(BuildTargetStableName.StandaloneWindows64), "StandaloneWindows64" },
            { "standaloneLinux64", nameof(BuildTargetStableName.StandaloneLinux64), "StandaloneLinux64" },
            { "ios", nameof(BuildTargetStableName.Ios), "iOS" },
            { "android", nameof(BuildTargetStableName.Android), "Android" },
            { "webgl", nameof(BuildTargetStableName.Webgl), "WebGL" },
            { "wsaPlayer", nameof(BuildTargetStableName.WsaPlayer), "WSAPlayer" },
            { "tvos", nameof(BuildTargetStableName.Tvos), "tvOS" },
            { "switch", nameof(BuildTargetStableName.Switch), "Switch" },
            { "linuxHeadlessSimulation", nameof(BuildTargetStableName.LinuxHeadlessSimulation), "LinuxHeadlessSimulation" },
            { "gameCoreXboxSeries", nameof(BuildTargetStableName.GameCoreXboxSeries), "GameCoreXboxSeries" },
            { "gameCoreXboxOne", nameof(BuildTargetStableName.GameCoreXboxOne), "GameCoreXboxOne" },
            { "ps4", nameof(BuildTargetStableName.Ps4), "PS4" },
            { "ps5", nameof(BuildTargetStableName.Ps5), "PS5" },
            { "xboxOne", nameof(BuildTargetStableName.XboxOne), "XboxOne" },
            { "embeddedLinux", nameof(BuildTargetStableName.EmbeddedLinux), "EmbeddedLinux" },
            { "qnx", nameof(BuildTargetStableName.Qnx), "QNX" },
            { "visionOS", nameof(BuildTargetStableName.VisionOs), "VisionOS" },
        };
    }
}
