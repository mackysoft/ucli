namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Computes source hashes for per-scene scene-tree-lite freshness checks. </summary>
internal interface ISceneTreeLiteSourceHashCalculator
{
    /// <summary> Tries to compute one source hash for the specified scene asset and its meta file. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="scenePath"> The project-relative scene path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The computed source hash when successful; otherwise <see langword="null" />. </returns>
    ValueTask<string?> TryCompute (
        string projectRootPath,
        string scenePath,
        CancellationToken cancellationToken = default);
}