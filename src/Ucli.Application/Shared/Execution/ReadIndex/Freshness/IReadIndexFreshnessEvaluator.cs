using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Evaluates persisted read-index freshness for application read-model policy. </summary>
internal interface IReadIndexFreshnessEvaluator
{
    /// <summary> Evaluates freshness for one catalog or asset lookup artifact. </summary>
    ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string projectRootPath,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default);

    /// <summary> Evaluates freshness for one scene-tree-lite artifact. </summary>
    ValueTask<IndexFreshnessEvaluationResult> EvaluateSceneTreeLite (
        string projectRootPath,
        string scenePath,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default);
}
