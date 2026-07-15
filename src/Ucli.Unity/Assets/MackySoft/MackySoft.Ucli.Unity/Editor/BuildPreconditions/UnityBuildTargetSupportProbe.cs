using System;
using MackySoft.Ucli.Contracts.Assurance.Build;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves Unity build target support using Unity BuildPipeline APIs. </summary>
    internal sealed class UnityBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
    {
        /// <inheritdoc />
        public UnityBuildTargetSupportProbeResult Probe (BuildTargetStableName buildTarget)
        {
            if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolve(buildTarget, out var unityBuildTargetName)
                || !Enum.TryParse(unityBuildTargetName, ignoreCase: false, out BuildTarget target))
            {
                return UnityBuildTargetSupportProbeResult.Invalid();
            }

            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            var isSupported = BuildPipeline.IsBuildTargetSupported(targetGroup, target);
            return UnityBuildTargetSupportProbeResult.Resolved(target, targetGroup, isSupported);
        }

        /// <summary> Resolves a Unity build target to its stable contract value. </summary>
        public static bool TryGetStableName (
            BuildTarget target,
            out BuildTargetStableName stableName)
        {
            return BuildTargetStableNameUnityBuildTargetResolver.TryResolveStableName(target.ToString(), out stableName);
        }
    }
}
