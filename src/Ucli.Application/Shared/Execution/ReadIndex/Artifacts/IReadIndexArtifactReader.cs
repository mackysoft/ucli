namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Reads persisted read-index artifact contracts for one storage root and project fingerprint. </summary>
internal interface IReadIndexArtifactReader
{
    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>types.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>schemas.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
        string storageRoot,
        string projectFingerprint,
        string scenePath,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);
}
