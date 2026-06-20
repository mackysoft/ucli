using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents build inputs resolved before Unity BuildPipeline precondition probing. </summary>
    /// <param name="InputKind"> The build profile input-kind literal. </param>
    /// <param name="BuildTarget"> The uCLI buildTarget stable name. </param>
    /// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum literal. </param>
    /// <param name="SceneSource"> The build profile scene source literal. </param>
    /// <param name="ScenePaths"> The explicit scene paths, or an empty list when scene source is Editor Build Settings. </param>
    /// <param name="Development"> Whether the development build option is enabled. </param>
    /// <param name="AllowedEditorModes"> The editor mode literals allowed by the resolved build runtime policy. </param>
    internal sealed record UnityBuildPreconditionInput (
        string InputKind,
        string BuildTarget,
        string UnityBuildTarget,
        string SceneSource,
        IReadOnlyList<string> ScenePaths,
        bool Development,
        IReadOnlyList<string> AllowedEditorModes);
}
