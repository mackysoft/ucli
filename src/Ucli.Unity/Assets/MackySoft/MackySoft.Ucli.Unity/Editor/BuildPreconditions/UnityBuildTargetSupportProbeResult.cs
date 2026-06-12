using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents a Unity build target support probe result. </summary>
    /// <param name="IsValidTarget"> Whether the Unity build target literal could be parsed. </param>
    /// <param name="Target"> The parsed Unity build target. </param>
    /// <param name="TargetGroup"> The Unity build target group. </param>
    /// <param name="IsSupported"> Whether the current Unity installation supports the target. </param>
    internal sealed record UnityBuildTargetSupportProbeResult (
        bool IsValidTarget,
        BuildTarget Target,
        BuildTargetGroup TargetGroup,
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
            BuildTarget target,
            BuildTargetGroup targetGroup,
            bool isSupported)
        {
            return new UnityBuildTargetSupportProbeResult(
                true,
                target,
                targetGroup,
                isSupported);
        }
    }
}
