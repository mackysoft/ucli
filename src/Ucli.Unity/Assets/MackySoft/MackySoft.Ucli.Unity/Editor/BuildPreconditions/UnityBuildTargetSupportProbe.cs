using System;
using System.Collections.Generic;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves Unity build target support using Unity BuildPipeline APIs. </summary>
    internal sealed class UnityBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
    {
        private static readonly HashSet<string> BuildTargetNames = new HashSet<string>(
            Enum.GetNames(typeof(BuildTarget)),
            StringComparer.Ordinal);

        /// <inheritdoc />
        public UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral)
        {
            if (string.IsNullOrWhiteSpace(unityBuildTargetLiteral)
                || !BuildTargetNames.Contains(unityBuildTargetLiteral)
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
