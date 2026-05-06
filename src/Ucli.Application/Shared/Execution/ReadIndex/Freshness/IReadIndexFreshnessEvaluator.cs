using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Evaluates persisted read-index freshness for application read-model policy. </summary>
internal interface IReadIndexFreshnessEvaluator
{
    /// <summary> Evaluates freshness for one catalog or asset lookup artifact. </summary>
    ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        ResolvedUnityProjectContext unityProject,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default);

    /// <summary> Observes freshness for one catalog or asset lookup artifact without applying read-index mode constraints. </summary>
    ValueTask<IndexFreshnessEvaluationResult> Observe (
        ResolvedUnityProjectContext unityProject,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        CancellationToken cancellationToken = default);

    /// <summary> Evaluates freshness for one scene-tree-lite artifact. </summary>
    ValueTask<IndexFreshnessEvaluationResult> EvaluateSceneTreeLite (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default);

    /// <summary> Observes freshness for one scene-tree-lite artifact without applying read-index mode constraints. </summary>
    ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLite (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        string? persistedSourceInputsHash,
        CancellationToken cancellationToken = default);
}
