using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Applies read-index freshness constraints required by application use cases. </summary>
internal static class IndexFreshnessPolicy
{
    /// <summary> Applies read-index mode constraint to one evaluated freshness value. </summary>
    /// <param name="mode"> The effective read-index mode. </param>
    /// <param name="freshness"> The evaluated freshness value. </param>
    /// <returns> One success or failure result after applying mode constraints. </returns>
    public static IndexFreshnessEvaluationResult ApplyModeConstraint (
        ReadIndexMode mode,
        IndexFreshness freshness)
    {
        if (mode == ReadIndexMode.RequireFresh && freshness != IndexFreshness.Fresh)
        {
            return IndexFreshnessEvaluationResult.Failure(
                freshness,
                new IndexServiceError(
                    IpcErrorCodes.ReadIndexFreshRequired,
                    "readIndexMode=requireFresh requires index freshness 'fresh'."));
        }

        return IndexFreshnessEvaluationResult.Success(freshness);
    }
}
