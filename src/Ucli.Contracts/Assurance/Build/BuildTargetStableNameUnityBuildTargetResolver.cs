namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Maps stable build-target contract values to the corresponding Unity <c>BuildTarget</c> names. </summary>
internal static class BuildTargetStableNameUnityBuildTargetResolver
{
    /// <summary> Tries to resolve a stable build target to the Unity <c>BuildTarget</c> name carried by IPC observations. </summary>
    public static bool TryResolve (
        BuildTargetStableName stableName,
        out string unityBuildTargetName)
    {
        switch (stableName)
        {
            case BuildTargetStableName.StandaloneOsx:
                unityBuildTargetName = "StandaloneOSX";
                return true;
            case BuildTargetStableName.StandaloneWindows:
                unityBuildTargetName = "StandaloneWindows";
                return true;
            case BuildTargetStableName.StandaloneWindows64:
                unityBuildTargetName = "StandaloneWindows64";
                return true;
            case BuildTargetStableName.StandaloneLinux64:
                unityBuildTargetName = "StandaloneLinux64";
                return true;
            case BuildTargetStableName.Ios:
                unityBuildTargetName = "iOS";
                return true;
            case BuildTargetStableName.Android:
                unityBuildTargetName = "Android";
                return true;
            case BuildTargetStableName.Webgl:
                unityBuildTargetName = "WebGL";
                return true;
            case BuildTargetStableName.WsaPlayer:
                unityBuildTargetName = "WSAPlayer";
                return true;
            case BuildTargetStableName.Tvos:
                unityBuildTargetName = "tvOS";
                return true;
            case BuildTargetStableName.Switch:
                unityBuildTargetName = "Switch";
                return true;
            case BuildTargetStableName.LinuxHeadlessSimulation:
                unityBuildTargetName = "LinuxHeadlessSimulation";
                return true;
            case BuildTargetStableName.GameCoreXboxSeries:
                unityBuildTargetName = "GameCoreXboxSeries";
                return true;
            case BuildTargetStableName.GameCoreXboxOne:
                unityBuildTargetName = "GameCoreXboxOne";
                return true;
            case BuildTargetStableName.Ps4:
                unityBuildTargetName = "PS4";
                return true;
            case BuildTargetStableName.Ps5:
                unityBuildTargetName = "PS5";
                return true;
            case BuildTargetStableName.XboxOne:
                unityBuildTargetName = "XboxOne";
                return true;
            case BuildTargetStableName.EmbeddedLinux:
                unityBuildTargetName = "EmbeddedLinux";
                return true;
            case BuildTargetStableName.Qnx:
                unityBuildTargetName = "QNX";
                return true;
            case BuildTargetStableName.VisionOs:
                unityBuildTargetName = "VisionOS";
                return true;
            default:
                unityBuildTargetName = string.Empty;
                return false;
        }
    }

    /// <summary> Tries to resolve an observed Unity <c>BuildTarget</c> name to its stable contract value. </summary>
    public static bool TryResolveStableName (
        string unityBuildTargetName,
        out BuildTargetStableName stableName)
    {
        foreach (BuildTargetStableName candidate in Enum.GetValues(typeof(BuildTargetStableName)))
        {
            if (TryResolve(candidate, out var candidateName)
                && string.Equals(candidateName, unityBuildTargetName, StringComparison.Ordinal))
            {
                stableName = candidate;
                return true;
            }
        }

        stableName = default;
        return false;
    }
}
