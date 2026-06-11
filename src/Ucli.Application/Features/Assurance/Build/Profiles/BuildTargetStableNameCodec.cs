namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Resolves uCLI build target stable names to Unity BuildTarget literals. </summary>
internal static class BuildTargetStableNameCodec
{
    /// <summary> Gets the stable name for Linux x64 standalone player builds. </summary>
    public const string StandaloneLinux64 = "standaloneLinux64";

    /// <summary> Tries to resolve one build target stable name. </summary>
    public static bool TryResolve (
        string stableName,
        out ResolvedBuildTarget target)
    {
        if (string.Equals(stableName, StandaloneLinux64, StringComparison.Ordinal))
        {
            target = new ResolvedBuildTarget(StandaloneLinux64, "StandaloneLinux64");
            return true;
        }

        target = null!;
        return false;
    }
}
