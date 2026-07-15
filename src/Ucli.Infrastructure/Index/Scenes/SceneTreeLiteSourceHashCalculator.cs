using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes one scene-tree-lite source hash from the target scene asset and meta file. </summary>
internal sealed class SceneTreeLiteSourceHashCalculator : ISceneTreeLiteSourceHashCalculator
{
    /// <inheritdoc />
    public async ValueTask<Sha256Digest?> TryComputeAsync (
        string projectRootPath,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
        }

        if (scenePath is null)
        {
            throw new ArgumentNullException(nameof(scenePath));
        }

        var scenePathResult = RepositoryPathNormalizer.TryNormalize(
            projectRootPath,
            scenePath.Value);
        if (!scenePathResult.IsSuccess)
        {
            return null;
        }

        var absoluteScenePath = scenePathResult.FullPath!;
        var absoluteMetaPath = absoluteScenePath + ".meta";

        var sceneHash = await FileContentHash.TryComputeFileHashAsync(absoluteScenePath, cancellationToken).ConfigureAwait(false);
        if (sceneHash == null)
        {
            return null;
        }

        var metaHash = await FileContentHash.TryComputeFileHashAsync(absoluteMetaPath, cancellationToken).ConfigureAwait(false);
        if (metaHash == null)
        {
            return null;
        }

        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(string.Concat(sceneHash, "\n", metaHash)));
    }
}
