using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Provides hash-based read-index freshness evaluation. </summary>
internal static class IndexHashFreshnessPolicy
{
    /// <summary> Evaluates freshness by comparing one persisted source-inputs hash with current snapshot hashes. </summary>
    public static IndexFreshness EvaluateFreshness (
        Sha256Digest persistedSourceInputsHash,
        ReadIndexInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        ArgumentNullException.ThrowIfNull(persistedSourceInputsHash);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        return persistedSourceInputsHash == GetSnapshotHash(currentSnapshot, target)
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

    /// <summary> Evaluates catalog freshness by comparing one persisted source-inputs hash with the current core hash. </summary>
    public static IndexFreshness EvaluateCoreFreshness (
        Sha256Digest persistedSourceInputsHash,
        ReadIndexCoreInputHashSnapshot currentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(persistedSourceInputsHash);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        return persistedSourceInputsHash == currentSnapshot.CombinedHash
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

    /// <summary> Evaluates scene-tree-lite freshness by comparing one persisted source hash with a current source hash. </summary>
    public static IndexFreshness EvaluateSceneTreeLiteFreshness (
        Sha256Digest persistedSourceInputsHash,
        Sha256Digest currentSourceHash)
    {
        ArgumentNullException.ThrowIfNull(persistedSourceInputsHash);
        ArgumentNullException.ThrowIfNull(currentSourceHash);
        return persistedSourceInputsHash == currentSourceHash
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

    private static Sha256Digest GetSnapshotHash (
        ReadIndexInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return target switch
        {
            IndexFreshnessTarget.OpsCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.AssetSearchLookup => currentSnapshot.AssetSearchHash,
            IndexFreshnessTarget.GuidPathLookup => currentSnapshot.GuidPathHash,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported read-index freshness target."),
        };
    }
}
