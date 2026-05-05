namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Persistence;

/// <summary> Persists the local <c>ops.catalog.json</c> read-index snapshot. </summary>
internal interface IOpsCatalogStore
{
    /// <summary> Writes one ops catalog snapshot and optional inputs manifest. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="generatedAtUtc"> The snapshot generation timestamp. </param>
    /// <param name="operations"> The operation entries to persist. </param>
    /// <param name="sourceInputsHash"> The source-inputs hash persisted onto <c>ops.catalog.json</c>. </param>
    /// <param name="manifestInputSnapshot"> The optional input-fingerprint snapshot used to rewrite <c>inputs/manifest.json</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when persistence finishes. </returns>
    ValueTask Write (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        string sourceInputsHash,
        OpsCatalogInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default);
}
