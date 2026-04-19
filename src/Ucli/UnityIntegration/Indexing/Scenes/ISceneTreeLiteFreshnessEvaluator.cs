using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Evaluates scene-tree-lite freshness for one scene path. </summary>
internal interface ISceneTreeLiteFreshnessEvaluator
{
    /// <summary> Evaluates one persisted scene-tree-lite source hash against the current scene files. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="scenePath"> The project-relative scene path. </param>
    /// <param name="persistedSourceInputsHash"> The persisted source-inputs hash. </param>
    /// <param name="mode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The freshness evaluation result. </returns>
    ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string projectRootPath,
        string scenePath,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default);
}