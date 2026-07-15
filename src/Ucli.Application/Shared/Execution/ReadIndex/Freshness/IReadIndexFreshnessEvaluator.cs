using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Observes persisted read-index freshness for application read-model policy. </summary>
internal interface IReadIndexFreshnessEvaluator
{
    /// <summary> Observes freshness for one catalog or asset lookup artifact without applying read-index mode constraints. </summary>
    ValueTask<IndexFreshnessEvaluationResult> ObserveAsync (
        ResolvedUnityProjectContext unityProject,
        IndexFreshnessTarget target,
        Sha256Digest persistedSourceInputsHash,
        CancellationToken cancellationToken = default);

    /// <summary> Observes freshness for one scene-tree-lite artifact without applying read-index mode constraints. </summary>
    ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLiteAsync (
        ResolvedUnityProjectContext unityProject,
        SceneAssetPath scenePath,
        Sha256Digest persistedSourceInputsHash,
        CancellationToken cancellationToken = default);
}
