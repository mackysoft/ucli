using MackySoft.Ucli.Contracts.Assurance.Build;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Probes Unity build target support without coupling callers to static BuildPipeline APIs. </summary>
    internal interface IUnityBuildTargetSupportProbe
    {
        /// <summary> Resolves one stable build target and reports whether the current installation supports it. </summary>
        /// <param name="buildTarget"> The stable build target. </param>
        /// <returns> The target support probe result. </returns>
        UnityBuildTargetSupportProbeResult Probe (BuildTargetStableName buildTarget);
    }
}
