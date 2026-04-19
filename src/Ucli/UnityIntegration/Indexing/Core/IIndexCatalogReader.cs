using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Reads persisted index catalog contracts from one storage root and project fingerprint. </summary>
internal interface IIndexCatalogReader
{
    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    ValueTask<IndexAccessResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>types.catalog.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    ValueTask<IndexAccessResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>schemas.catalog.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    ValueTask<IndexAccessResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    ValueTask<IndexAccessResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    ValueTask<IndexAccessResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="scenePath"> The project-relative scene path represented by the lookup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    ValueTask<IndexAccessResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
        string storageRoot,
        string projectFingerprint,
        string scenePath,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to manifest-read result. </returns>
    ValueTask<IndexAccessResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);
}