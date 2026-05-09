namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Reads persisted read-index artifact contracts for one resolved Unity project. </summary>
internal interface IReadIndexArtifactReader
{
    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>ops.describe/&lt;opKey&gt;.json</c> contract referenced by <c>ops.catalog.json</c>. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>> ReadOpsDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        IndexOpsCatalogEntryJsonContract catalogEntry,
        string sourceInputsHash,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>types.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>schemas.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookupAsync (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifestAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
