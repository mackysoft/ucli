namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Reads persisted read-index artifact contracts for one resolved Unity project. </summary>
internal interface IReadIndexArtifactReader
{
    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>types.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>schemas.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
