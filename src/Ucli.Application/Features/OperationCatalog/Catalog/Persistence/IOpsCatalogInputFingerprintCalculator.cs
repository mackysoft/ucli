namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Persistence;

/// <summary> Computes deterministic input fingerprints used when persisting refreshed ops catalogs. </summary>
internal interface IOpsCatalogInputFingerprintCalculator
{
    /// <summary> Tries to compute one core input fingerprint snapshot without asset lookup hashes. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    ValueTask<OpsCatalogCoreInputHashSnapshot?> TryComputeCore (
        string projectRootPath,
        CancellationToken cancellationToken = default);

    /// <summary> Tries to compute one full input fingerprint snapshot from project files. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    ValueTask<OpsCatalogInputHashSnapshot?> TryCompute (
        string projectRootPath,
        CancellationToken cancellationToken = default);
}
