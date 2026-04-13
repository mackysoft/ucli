using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Scenes;

/// <summary> Persists per-scene scene-tree-lite lookup artifacts. </summary>
internal interface ISceneTreeLiteStore
{
    /// <summary> Writes one scene-tree-lite lookup artifact. </summary>
    ValueTask Write (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        string scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        string sourceInputsHash,
        CancellationToken cancellationToken = default);
}