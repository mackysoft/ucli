namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes deterministic input fingerprints for read-index freshness checks. </summary>
internal interface IIndexInputFingerprintCalculator
{
    /// <summary> Tries to compute one core input fingerprint snapshot without asset lookup hashes. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreAsync (
        string projectRootPath,
        CancellationToken cancellationToken = default);

    /// <summary> Tries to compute one input fingerprint snapshot from project files. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    ValueTask<IndexInputHashSnapshot?> TryComputeAsync (
        string projectRootPath,
        CancellationToken cancellationToken = default);
}
