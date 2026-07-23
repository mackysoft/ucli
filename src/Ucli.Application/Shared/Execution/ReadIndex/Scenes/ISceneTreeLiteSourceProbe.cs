namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Observes whether a guarded scene source file currently exists. </summary>
internal interface ISceneTreeLiteSourceProbe
{
    /// <summary> Observes the current filesystem state for the guarded scene file. </summary>
    ValueTask<SceneTreeLiteSourceProbeResult> EnsureCurrentAssetsSceneExistsAsync (
        SceneTreeLiteSourcePaths sourcePaths,
        CancellationToken cancellationToken = default);
}
