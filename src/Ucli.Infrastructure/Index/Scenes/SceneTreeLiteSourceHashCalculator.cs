using System.Text;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes one scene-tree-lite source hash from the target scene asset and meta file. </summary>
internal sealed class SceneTreeLiteSourceHashCalculator : ISceneTreeLiteSourceHashCalculator
{
    /// <inheritdoc />
    public async ValueTask<string?> TryCompute (
        string projectRootPath,
        string scenePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
        }

        if (string.IsNullOrWhiteSpace(scenePath))
        {
            throw new ArgumentException("Scene path must not be empty.", nameof(scenePath));
        }

        var normalizedScenePath = PathStringNormalizer.ToSlashSeparated(scenePath);
        var scenePathResult = RepositoryPathNormalizer.TryNormalize(
            projectRootPath,
            PathStringNormalizer.ToPlatformSeparated(normalizedScenePath));
        if (!scenePathResult.IsSuccess)
        {
            return null;
        }

        var absoluteScenePath = scenePathResult.FullPath!;
        var absoluteMetaPath = absoluteScenePath + ".meta";

        var sceneHash = await FileContentHash.TryComputeFileHash(absoluteScenePath, cancellationToken).ConfigureAwait(false);
        if (sceneHash == null)
        {
            return null;
        }

        var metaHash = await FileContentHash.TryComputeFileHash(absoluteMetaPath, cancellationToken).ConfigureAwait(false);
        if (metaHash == null)
        {
            return null;
        }

        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(string.Concat(sceneHash, "\n", metaHash)));
    }
}
