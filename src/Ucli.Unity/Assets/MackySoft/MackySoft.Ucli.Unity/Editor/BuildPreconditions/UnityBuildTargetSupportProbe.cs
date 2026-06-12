using System;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves Unity build target support using Unity BuildPipeline APIs. </summary>
    internal sealed class UnityBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
    {
        /// <inheritdoc />
        public UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral)
        {
            if (string.IsNullOrWhiteSpace(unityBuildTargetLiteral)
                || !Enum.TryParse(unityBuildTargetLiteral, ignoreCase: false, out BuildTarget target))
            {
                return UnityBuildTargetSupportProbeResult.Invalid();
            }

            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            var isSupported = BuildPipeline.IsBuildTargetSupported(targetGroup, target);
            return UnityBuildTargetSupportProbeResult.Resolved(target, targetGroup, isSupported);
        }
    }
}
