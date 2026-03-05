using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Index;

/// <summary> Evaluates one read-index freshness state from persisted input manifest and current project inputs. </summary>
internal interface IIndexFreshnessEvaluator
{
    /// <summary> Evaluates index freshness for one Unity project context. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="projectRoot"> The Unity project root path. </param>
    /// <param name="mode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to freshness evaluation result. </returns>
    ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string storageRoot,
        string projectFingerprint,
        string projectRoot,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default);
}