using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Ops;

/// <summary> Persists the local <c>ops.catalog.json</c> read-index snapshot. </summary>
internal interface IOpsCatalogStore
{
    /// <summary> Writes one ops catalog snapshot and matching inputs manifest. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="generatedAtUtc"> The snapshot generation timestamp. </param>
    /// <param name="operations"> The operation entries to persist. </param>
    /// <param name="inputSnapshot"> The input fingerprint snapshot used for freshness checks. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when persistence finishes. </returns>
    ValueTask Write (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        IndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default);
}