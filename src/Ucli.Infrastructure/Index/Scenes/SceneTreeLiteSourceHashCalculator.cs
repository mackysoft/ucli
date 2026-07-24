using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes one scene-tree-lite source hash from the target scene asset and meta file. </summary>
internal sealed class SceneTreeLiteSourceHashCalculator : ISceneTreeLiteSourceHashCalculator
{
    /// <inheritdoc />
    public async ValueTask<Sha256Digest?> TryComputeAsync (
        ContainedPath sceneFilePath,
        ContainedPath metaFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sceneFilePath is null)
        {
            throw new ArgumentNullException(nameof(sceneFilePath));
        }

        if (metaFilePath is null)
        {
            throw new ArgumentNullException(nameof(metaFilePath));
        }

        var sceneHash = await FileContentHash.TryComputeFileHashAsync(sceneFilePath.Target, cancellationToken).ConfigureAwait(false);
        if (sceneHash == null)
        {
            return null;
        }

        var metaHash = await FileContentHash.TryComputeFileHashAsync(metaFilePath.Target, cancellationToken).ConfigureAwait(false);
        if (metaHash == null)
        {
            return null;
        }

        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(string.Concat(sceneHash, "\n", metaHash)));
    }
}
