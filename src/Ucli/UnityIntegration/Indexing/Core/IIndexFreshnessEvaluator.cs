using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Evaluates one read-index freshness state from persisted source-input hashes and current project inputs. </summary>
internal interface IIndexFreshnessEvaluator
{
    /// <summary> Evaluates index freshness for one Unity project context. </summary>
    /// <param name="projectRoot"> The Unity project root path. </param>
    /// <param name="target"> The read-index target being evaluated. </param>
    /// <param name="persistedSourceInputsHash"> The persisted source-inputs hash stored on the target artifact. </param>
    /// <param name="mode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to freshness evaluation result. </returns>
    ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string projectRoot,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default);
}
