namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Provides hash-based read-index freshness evaluation. </summary>
internal static class IndexHashFreshnessPolicy
{
    /// <summary> Evaluates freshness by comparing one persisted source-inputs hash with current snapshot hashes. </summary>
    public static IndexFreshness EvaluateFreshness (
        string? persistedSourceInputsHash,
        ReadIndexInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return string.Equals(persistedSourceInputsHash, GetSnapshotHash(currentSnapshot, target), StringComparison.Ordinal)
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

    /// <summary> Evaluates freshness by comparing one persisted source-inputs hash with current core snapshot hashes. </summary>
    public static IndexFreshness EvaluateFreshness (
        string? persistedSourceInputsHash,
        ReadIndexCoreInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return string.Equals(persistedSourceInputsHash, GetSnapshotHash(currentSnapshot, target), StringComparison.Ordinal)
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

    /// <summary> Evaluates scene-tree-lite freshness by comparing one persisted source hash with a current source hash. </summary>
    public static IndexFreshness EvaluateSceneTreeLiteFreshness (
        string? persistedSourceInputsHash,
        string currentSourceHash)
    {
        return string.Equals(persistedSourceInputsHash, currentSourceHash, StringComparison.Ordinal)
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
    }

    private static string GetSnapshotHash (
        ReadIndexInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return target switch
        {
            IndexFreshnessTarget.OpsCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.TypesCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.SchemasCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.AssetSearchLookup => currentSnapshot.AssetSearchHash,
            IndexFreshnessTarget.GuidPathLookup => currentSnapshot.GuidPathHash,
            IndexFreshnessTarget.SceneTreeLite => throw new ArgumentOutOfRangeException(nameof(target), target, "Scene-tree-lite freshness requires a scene source hash."),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported read-index freshness target."),
        };
    }

    private static string GetSnapshotHash (
        ReadIndexCoreInputHashSnapshot currentSnapshot,
        IndexFreshnessTarget target)
    {
        return target switch
        {
            IndexFreshnessTarget.OpsCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.TypesCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.SchemasCatalog => currentSnapshot.CombinedHash,
            IndexFreshnessTarget.AssetSearchLookup => throw new ArgumentOutOfRangeException(nameof(target), target, "Asset lookup freshness requires full input snapshot."),
            IndexFreshnessTarget.GuidPathLookup => throw new ArgumentOutOfRangeException(nameof(target), target, "Asset lookup freshness requires full input snapshot."),
            IndexFreshnessTarget.SceneTreeLite => throw new ArgumentOutOfRangeException(nameof(target), target, "Scene-tree-lite freshness requires a scene source hash."),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported read-index freshness target."),
        };
    }
}
