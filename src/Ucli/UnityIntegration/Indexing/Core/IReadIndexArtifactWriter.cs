using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Persists read-index artifact contracts into filesystem-backed storage. </summary>
internal interface IReadIndexArtifactWriter
{
    /// <summary> Writes one ops catalog and an optional input manifest. </summary>
    ValueTask WriteOpsCatalogAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<ValidatedOpsOperation> operations,
        Sha256Digest sourceInputsHash,
        ReadIndexInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default);

    /// <summary> Writes asset lookup artifacts and the input manifest. </summary>
    ValueTask WriteAssetLookupsAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default);

    /// <summary> Writes one scene-tree-lite lookup artifact. </summary>
    ValueTask WriteSceneTreeLiteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        SceneAssetPath scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        Sha256Digest sourceInputsHash,
        CancellationToken cancellationToken = default);
}
