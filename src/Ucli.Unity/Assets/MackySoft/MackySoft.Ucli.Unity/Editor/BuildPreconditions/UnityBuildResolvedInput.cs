using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents the Unity BuildPipeline input resolved after build preconditions pass. </summary>
    /// <param name="Target"> The Unity build target. </param>
    /// <param name="TargetGroup"> The Unity build target group. </param>
    /// <param name="ScenePaths"> The project-relative scene paths passed to BuildPipeline. </param>
    /// <param name="Options"> The Unity build options. </param>
    internal sealed record UnityBuildResolvedInput (
        BuildTarget Target,
        BuildTargetGroup TargetGroup,
        string[] ScenePaths,
        BuildOptions Options);
}
