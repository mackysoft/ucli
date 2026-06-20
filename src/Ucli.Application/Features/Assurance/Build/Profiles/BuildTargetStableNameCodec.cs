using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Resolves uCLI buildTarget stable names to Unity BuildTarget literals. </summary>
internal static class BuildTargetStableNameCodec
{
    /// <summary> Tries to resolve one buildTarget stable name. </summary>
    public static bool TryResolve (
        string stableName,
        out ResolvedBuildTarget target)
    {
        if (!ContractLiteralCodec.TryParse<BuildTargetStableName>(stableName, out var stableNameValue))
        {
            target = null!;
            return false;
        }

        if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableNameValue, out var unityBuildTargetLiteral))
        {
            target = null!;
            return false;
        }

        target = new ResolvedBuildTarget(
            StableNameValue: stableNameValue,
            StableName: ContractLiteralCodec.ToValue(stableNameValue),
            UnityBuildTargetLiteral: unityBuildTargetLiteral);
        return true;
    }
}
