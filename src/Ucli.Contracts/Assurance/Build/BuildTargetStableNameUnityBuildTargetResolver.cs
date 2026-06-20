using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Resolves uCLI buildTarget stable names to Unity BuildTarget literals. </summary>
internal static class BuildTargetStableNameUnityBuildTargetResolver
{
    /// <summary> Tries to resolve one buildTarget stable-name literal to its Unity BuildTarget literal. </summary>
    public static bool TryResolve (
        string stableName,
        out string unityBuildTargetLiteral)
    {
        if (!ContractLiteralCodec.TryParse<BuildTargetStableName>(stableName, out var stableNameValue))
        {
            unityBuildTargetLiteral = null!;
            return false;
        }

        return TryResolve(stableNameValue, out unityBuildTargetLiteral);
    }

    /// <summary> Tries to resolve one buildTarget stable name to its Unity BuildTarget literal. </summary>
    public static bool TryResolve (
        BuildTargetStableName stableName,
        out string unityBuildTargetLiteral)
    {
        switch (stableName)
        {
            case BuildTargetStableName.StandaloneOsx:
                unityBuildTargetLiteral = "StandaloneOSX";
                return true;
            case BuildTargetStableName.StandaloneWindows:
                unityBuildTargetLiteral = "StandaloneWindows";
                return true;
            case BuildTargetStableName.StandaloneWindows64:
                unityBuildTargetLiteral = "StandaloneWindows64";
                return true;
            case BuildTargetStableName.StandaloneLinux64:
                unityBuildTargetLiteral = "StandaloneLinux64";
                return true;
            case BuildTargetStableName.Ios:
                unityBuildTargetLiteral = "iOS";
                return true;
            case BuildTargetStableName.Android:
                unityBuildTargetLiteral = "Android";
                return true;
            case BuildTargetStableName.Webgl:
                unityBuildTargetLiteral = "WebGL";
                return true;
            case BuildTargetStableName.WsaPlayer:
                unityBuildTargetLiteral = "WSAPlayer";
                return true;
            case BuildTargetStableName.Tvos:
                unityBuildTargetLiteral = "tvOS";
                return true;
            case BuildTargetStableName.Switch:
                unityBuildTargetLiteral = "Switch";
                return true;
            case BuildTargetStableName.LinuxHeadlessSimulation:
                unityBuildTargetLiteral = "LinuxHeadlessSimulation";
                return true;
            case BuildTargetStableName.GameCoreXboxSeries:
                unityBuildTargetLiteral = "GameCoreXboxSeries";
                return true;
            case BuildTargetStableName.GameCoreXboxOne:
                unityBuildTargetLiteral = "GameCoreXboxOne";
                return true;
            case BuildTargetStableName.Ps4:
                unityBuildTargetLiteral = "PS4";
                return true;
            case BuildTargetStableName.Ps5:
                unityBuildTargetLiteral = "PS5";
                return true;
            case BuildTargetStableName.XboxOne:
                unityBuildTargetLiteral = "XboxOne";
                return true;
            case BuildTargetStableName.EmbeddedLinux:
                unityBuildTargetLiteral = "EmbeddedLinux";
                return true;
            case BuildTargetStableName.Qnx:
                unityBuildTargetLiteral = "QNX";
                return true;
            case BuildTargetStableName.VisionOs:
                unityBuildTargetLiteral = "VisionOS";
                return true;
            default:
                unityBuildTargetLiteral = null!;
                return false;
        }
    }
}
