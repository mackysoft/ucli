using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Reads persisted read-index artifact contracts for one resolved Unity project. </summary>
internal interface IReadIndexArtifactReader
{
    /// <summary> Reads the core artifact set from one immutable current generation. </summary>
    ValueTask<ReadIndexGenerationArtifacts> ReadGenerationArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>> ReadOpsCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>ops/&lt;operationStorageKey&gt;.json</c> contract referenced by <c>ops.catalog.json</c>. </summary>
    ValueTask<ReadIndexArtifactReadResult<OpsDescribeSnapshot>> ReadOpsDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        ValidatedOpsCatalogEntry catalogEntry,
        Sha256Digest sourceInputsHash,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<AssetSearchLookupSnapshot>> ReadAssetSearchLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<GuidPathLookupSnapshot>> ReadGuidPathLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    ValueTask<ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>> ReadSceneTreeLiteLookupAsync (
        ResolvedUnityProjectContext unityProject,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    ValueTask<ReadIndexArtifactReadResult<ReadIndexInputsManifestSnapshot>> ReadInputsManifestAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
