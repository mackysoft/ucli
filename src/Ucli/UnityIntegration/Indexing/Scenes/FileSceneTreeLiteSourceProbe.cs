using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Probes scene-tree-lite source files through filesystem-backed project paths. </summary>
internal sealed class FileSceneTreeLiteSourceProbe : ISceneTreeLiteSourceProbe
{
    /// <inheritdoc />
    public ValueTask<SceneTreeLiteSourceProbeResult> EnsureCurrentAssetsSceneExistsAsync (
        ResolvedUnityProjectContext project,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scenePath);

        return ValueTask.FromResult(TryEnsureCurrentAssetsSceneExists(project.UnityProjectRoot, scenePath.Value, out var errorMessage)
            ? SceneTreeLiteSourceProbeResult.Success()
            : SceneTreeLiteSourceProbeResult.Failure(errorMessage));
    }

    private static bool TryEnsureCurrentAssetsSceneExists (
        string projectRootPath,
        string scenePath,
        out string errorMessage)
    {
        if (!TryResolveAbsoluteScenePath(projectRootPath, scenePath, out var absoluteScenePath, out errorMessage))
        {
            return false;
        }

        if (!File.Exists(absoluteScenePath))
        {
            errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryResolveAbsoluteScenePath (
        string projectRootPath,
        string scenePath,
        out string absoluteScenePath,
        out string errorMessage)
    {
        absoluteScenePath = string.Empty;
        errorMessage = string.Empty;

        try
        {
            var projectRoot = Path.GetFullPath(projectRootPath);
            var candidatePath = Path.GetFullPath(Path.Combine(
                projectRoot,
                PathStringNormalizer.ToPlatformSeparated(scenePath)));
            if (!PathIdentity.IsSameOrChildPath(projectRoot, candidatePath))
            {
                errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
                return false;
            }

            absoluteScenePath = candidatePath;
            return true;
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
            return false;
        }
    }

}
