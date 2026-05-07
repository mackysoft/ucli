namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Probes whether a scene-tree-lite source can be used without exposing filesystem details to application policy. </summary>
internal interface ISceneTreeLiteSourceProbe
{
    /// <summary> Ensures the current project source contains the specified assets scene. </summary>
    ValueTask<SceneTreeLiteSourceProbeResult> EnsureCurrentAssetsSceneExists (
        ResolvedUnityProjectContext project,
        string normalizedScenePath,
        CancellationToken cancellationToken = default);
}
