namespace MackySoft.Ucli.Infrastructure.Index;

using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;

/// <summary> Computes source hashes for per-scene scene-tree-lite freshness checks. </summary>
internal interface ISceneTreeLiteSourceHashCalculator
{
    /// <summary> Tries to compute one source hash for the specified scene asset and its meta file. </summary>
    /// <param name="sceneFilePath"> The scene file path contained by its Unity project root. </param>
    /// <param name="metaFilePath"> The companion meta-file path contained by the same Unity project root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The computed source hash when successful; otherwise <see langword="null" />. </returns>
    ValueTask<Sha256Digest?> TryComputeAsync (
        ContainedPath sceneFilePath,
        ContainedPath metaFilePath,
        CancellationToken cancellationToken = default);
}
