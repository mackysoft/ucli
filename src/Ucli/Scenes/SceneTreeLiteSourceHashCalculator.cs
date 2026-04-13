using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Paths;

namespace MackySoft.Ucli.Scenes;

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
        var absoluteScenePath = Path.Combine(
            Path.GetFullPath(projectRootPath),
            PathStringNormalizer.ToPlatformSeparated(normalizedScenePath));
        var absoluteMetaPath = absoluteScenePath + ".meta";

        var sceneHash = await TryHashFile(absoluteScenePath, cancellationToken).ConfigureAwait(false);
        if (sceneHash == null)
        {
            return null;
        }

        var metaHash = await TryHashFile(absoluteMetaPath, cancellationToken).ConfigureAwait(false);
        if (metaHash == null)
        {
            return null;
        }

        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(string.Concat(sceneHash, "\n", metaHash)));
    }

    private static async ValueTask<string?> TryHashFile (
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            return null;
        }

        return Sha256LowerHex.Compute(bytes);
    }
}