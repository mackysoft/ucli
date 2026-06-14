using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents a Unity build target support probe result. </summary>
    /// <param name="IsValidTarget"> Whether the Unity build target literal could be parsed. </param>
    /// <param name="UnityBuildTarget"> The parsed Unity <c>BuildTarget</c> value. </param>
    /// <param name="UnityBuildTargetGroup"> The Unity <c>BuildTargetGroup</c> value. </param>
    /// <param name="IsSupported"> Whether the current Unity installation supports the target. </param>
    internal sealed record UnityBuildTargetSupportProbeResult (
        bool IsValidTarget,
        BuildTarget UnityBuildTarget,
        BuildTargetGroup UnityBuildTargetGroup,
        bool IsSupported)
    {
        /// <summary> Creates a result for an invalid target literal. </summary>
        public static UnityBuildTargetSupportProbeResult Invalid ()
        {
            return new UnityBuildTargetSupportProbeResult(
                false,
                default,
                default,
                false);
        }

        /// <summary> Creates a result for a parsed target literal. </summary>
        public static UnityBuildTargetSupportProbeResult Resolved (
            BuildTarget unityBuildTarget,
            BuildTargetGroup unityBuildTargetGroup,
            bool isSupported)
        {
            return new UnityBuildTargetSupportProbeResult(
                true,
                unityBuildTarget,
                unityBuildTargetGroup,
                isSupported);
        }
    }
}
