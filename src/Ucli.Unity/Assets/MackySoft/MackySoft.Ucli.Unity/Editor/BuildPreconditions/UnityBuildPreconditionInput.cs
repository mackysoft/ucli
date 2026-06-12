using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents build inputs resolved before Unity BuildPipeline precondition probing. </summary>
    /// <param name="TargetStableName"> The uCLI build target stable name. </param>
    /// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum literal. </param>
    /// <param name="SceneSource"> The build profile scene source literal. </param>
    /// <param name="ScenePaths"> The explicit scene paths, or an empty list when scene source is Editor Build Settings. </param>
    /// <param name="Development"> Whether the development build option is enabled. </param>
    internal sealed record UnityBuildPreconditionInput (
        string TargetStableName,
        string UnityBuildTarget,
        string SceneSource,
        IReadOnlyList<string> ScenePaths,
        bool Development);
}
