using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes deterministic input fingerprints from filesystem sources. </summary>
internal sealed class FileSystemIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
{
    /// <summary> Tries to compute one core input fingerprint snapshot without asset lookup hashes. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />. </exception>
    public ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreAsync (
        AbsolutePath projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (projectRootPath is null)
        {
            throw new ArgumentNullException(nameof(projectRootPath));
        }

        return TryComputeCoreInternalAsync(
            IndexInputSourcePaths.FromProjectRoot(projectRootPath),
            cancellationToken);
    }

    /// <summary> Tries to compute one input fingerprint snapshot from project files. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />. </exception>
    public async ValueTask<IndexInputHashSnapshot?> TryComputeAsync (
        AbsolutePath projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (projectRootPath is null)
        {
            throw new ArgumentNullException(nameof(projectRootPath));
        }

        var sourcePaths = IndexInputSourcePaths.FromProjectRoot(projectRootPath);
        var coreSnapshot = await TryComputeCoreInternalAsync(sourcePaths, cancellationToken).ConfigureAwait(false);
        if (coreSnapshot == null)
        {
            return null;
        }

        var assetsContentHash = await IndexInputFileHasher.TryHashDirectoryContentAsync(sourcePaths.AssetsPath, cancellationToken).ConfigureAwait(false);
        if (assetsContentHash == null)
        {
            return null;
        }

        return IndexInputHashSnapshotFactory.Create(coreSnapshot, assetsContentHash);
    }

    private static async ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreInternalAsync (
        IndexInputSourcePaths sourcePaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hashes = await IndexInputFileHasher.TryHashCoreInputsAsync(sourcePaths, cancellationToken).ConfigureAwait(false);
        return hashes == null ? null : IndexInputHashSnapshotFactory.CreateCore(hashes);
    }
}
