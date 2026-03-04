using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Index;

/// <summary> Provides read-index freshness policy evaluation and mode constraints. </summary>
internal static class IndexFreshnessPolicy
{
    /// <summary> Evaluates freshness by comparing inputs-manifest values with current snapshot hashes. </summary>
    /// <param name="manifest"> The persisted inputs manifest contract. </param>
    /// <param name="currentSnapshot"> The current inputs snapshot hash. </param>
    /// <returns> The evaluated freshness value. </returns>
    public static IndexFreshness EvaluateFreshness (
        IndexInputsManifestJsonContract manifest,
        IndexInputHashSnapshot currentSnapshot)
    {
        return string.Equals(manifest.ScriptAssembliesHash, currentSnapshot.ScriptAssembliesHash, StringComparison.Ordinal)
            && string.Equals(manifest.PackagesManifestHash, currentSnapshot.PackagesManifestHash, StringComparison.Ordinal)
            && string.Equals(manifest.PackagesLockHash, currentSnapshot.PackagesLockHash, StringComparison.Ordinal)
            && string.Equals(manifest.AssemblyDefinitionHash, currentSnapshot.AssemblyDefinitionHash, StringComparison.Ordinal)
            && string.Equals(manifest.CombinedHash, currentSnapshot.CombinedHash, StringComparison.Ordinal)
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
}
