using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Persists read-index artifact contracts into filesystem-backed storage. </summary>
internal interface IReadIndexArtifactWriter
{
    /// <summary> Writes one ops catalog and an optional input manifest. </summary>
    ValueTask WriteOpsCatalogAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        string sourceInputsHash,
        ReadIndexInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default);

    /// <summary> Writes asset lookup artifacts and the input manifest. </summary>
    ValueTask WriteAssetLookupsAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default);

    /// <summary> Writes one scene-tree-lite lookup artifact. </summary>
    ValueTask WriteSceneTreeLiteAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        string scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        string sourceInputsHash,
        CancellationToken cancellationToken = default);
}
