using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents the Unity BuildPipeline input resolved after build preconditions pass. </summary>
    /// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> value. </param>
    /// <param name="UnityBuildTargetGroup"> The Unity <c>BuildTargetGroup</c> value. </param>
    /// <param name="ScenePaths"> The project-relative scene paths passed to BuildPipeline. </param>
    /// <param name="Options"> The Unity build options. </param>
    internal sealed record UnityBuildResolvedInput (
        BuildTarget UnityBuildTarget,
        BuildTargetGroup UnityBuildTargetGroup,
        SceneAssetPath[] ScenePaths,
        BuildOptions Options);
}
