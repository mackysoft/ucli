using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Shared.Execution.ReadIndex;

/// <summary> Provides read-index freshness policy evaluation and mode constraints. </summary>
internal static class IndexFreshnessPolicy
{
    /// <summary> Evaluates freshness by comparing one persisted source-inputs hash with current snapshot hashes. </summary>
    /// <param name="persistedSourceInputsHash"> The persisted source-inputs hash value. </param>
    /// <param name="currentSnapshot"> The current inputs snapshot hash. </param>
    /// <param name="target"> The read-index target being evaluated. </param>
    /// <returns> The evaluated freshness value. </returns>
    public static IndexFreshness EvaluateFreshness (
        string? persistedSourceInputsHash,
        IndexInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return string.Equals(persistedSourceInputsHash, GetSnapshotHash(currentSnapshot, target), StringComparison.Ordinal)
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

    /// <summary> Evaluates freshness by comparing one persisted source-inputs hash with current core snapshot hashes. </summary>
    /// <param name="persistedSourceInputsHash"> The persisted source-inputs hash value. </param>
    /// <param name="currentSnapshot"> The current core inputs snapshot hash. </param>
    /// <param name="target"> The read-index target being evaluated. </param>
    /// <returns> The evaluated freshness value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="target" /> requires asset lookup hashes. </exception>
    public static IndexFreshness EvaluateFreshness (
        string? persistedSourceInputsHash,
        IndexCoreInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return string.Equals(persistedSourceInputsHash, GetSnapshotHash(currentSnapshot, target), StringComparison.Ordinal)
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

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

    private static string GetSnapshotHash (
        IndexInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return target switch
        {
            IndexFreshnessTarget.OpsCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.TypesCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.SchemasCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.AssetSearchLookup => currentSnapshot.AssetSearchHash,
            IndexFreshnessTarget.GuidPathLookup => currentSnapshot.GuidPathHash,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported read-index freshness target."),
        };
    }

    private static string GetSnapshotHash (
        IndexCoreInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return target switch
        {
            IndexFreshnessTarget.OpsCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.TypesCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.SchemasCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.AssetSearchLookup => throw new ArgumentOutOfRangeException(nameof(target), target, "Asset lookup freshness requires full input snapshot."),
            IndexFreshnessTarget.GuidPathLookup => throw new ArgumentOutOfRangeException(nameof(target), target, "Asset lookup freshness requires full input snapshot."),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported read-index freshness target."),
        };
    }
}
