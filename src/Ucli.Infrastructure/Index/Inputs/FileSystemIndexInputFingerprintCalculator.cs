using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes deterministic input fingerprints from filesystem sources. </summary>
internal sealed class FileSystemIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
{
    private const string ProjectRootPathEmptyMessage = "Project root path must not be empty.";

    /// <summary> Tries to compute one core input fingerprint snapshot without asset lookup hashes. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    public ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreAsync (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfProjectRootPathInvalid(projectRootPath);

        var sourcePaths = TryCreateSourcePaths(projectRootPath);
        if (sourcePaths is null)
        {
            return new ValueTask<IndexCoreInputHashSnapshot?>((IndexCoreInputHashSnapshot?)null);
        }

        return TryComputeCoreInternalAsync(sourcePaths, cancellationToken);
    }

    /// <summary> Tries to compute one input fingerprint snapshot from project files. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    public async ValueTask<IndexInputHashSnapshot?> TryComputeAsync (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfProjectRootPathInvalid(projectRootPath);

        var sourcePaths = TryCreateSourcePaths(projectRootPath);
        if (sourcePaths is null)
        {
            return null;
        }

        return await TryComputeInternalAsync(sourcePaths, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IndexInputHashSnapshot?> TryComputeInternalAsync (
        IndexInputSourcePaths sourcePaths,
        CancellationToken cancellationToken)
    {
        var coreSnapshot = await TryComputeCoreInternalAsync(sourcePaths, cancellationToken).ConfigureAwait(false);
        if (coreSnapshot == null)
        {
            return null;
        }

        return await TryComputeWithCoreSnapshotAsync(sourcePaths, coreSnapshot, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IndexInputHashSnapshot?> TryComputeWithCoreSnapshotAsync (
        IndexInputSourcePaths sourcePaths,
        IndexCoreInputHashSnapshot coreSnapshot,
        CancellationToken cancellationToken)
    {
        var assetsContentHash = await IndexInputFileHasher.TryHashAssetsContentAsync(sourcePaths, cancellationToken).ConfigureAwait(false);
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

    private static IndexInputSourcePaths? TryCreateSourcePaths (string projectRootPath)
    {
        var pathResult = PathNormalizer.TryNormalizeFullPath(projectRootPath);
        return pathResult.IsSuccess
            ? IndexInputSourcePaths.FromNormalizedProjectRoot(pathResult.FullPath!)
            : null;
    }

    private static void ThrowIfProjectRootPathInvalid (string projectRootPath)
    {
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException(ProjectRootPathEmptyMessage, nameof(projectRootPath));
        }
    }
}
