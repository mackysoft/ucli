using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Probes scene-tree-lite source files through filesystem-backed project paths. </summary>
internal sealed class FileSceneTreeLiteSourceProbe : ISceneTreeLiteSourceProbe
{
    /// <inheritdoc />
    public ValueTask<SceneTreeLiteSourceProbeResult> EnsureCurrentAssetsSceneExistsAsync (
        SceneTreeLiteSourcePaths sourcePaths,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sourcePaths);

        if (!File.Exists(sourcePaths.SceneFilePath.Target.Value))
        {
            return ValueTask.FromResult(SceneTreeLiteSourceProbeResult.Failure(
                $"Scene path could not be resolved to a scene asset: {sourcePaths.SceneAssetPath.Value}."));
        }

        return ValueTask.FromResult(SceneTreeLiteSourceProbeResult.Success());
    }
}
