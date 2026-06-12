using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Probes Unity build target support without coupling callers to static BuildPipeline APIs. </summary>
    internal interface IUnityBuildTargetSupportProbe
    {
        /// <summary> Resolves one Unity build target literal and reports whether the current installation supports it. </summary>
        /// <param name="unityBuildTargetLiteral"> The Unity <see cref="BuildTarget" /> enum literal. </param>
        /// <returns> The target support probe result. </returns>
        UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral);
    }
}
