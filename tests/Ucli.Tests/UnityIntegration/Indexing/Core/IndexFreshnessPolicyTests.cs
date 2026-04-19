using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class IndexFreshnessPolicyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsFresh_WhenPersistedHashMatchesSnapshot ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexFreshnessPolicy.EvaluateFreshness(snapshot.CombinedHash, snapshot, IndexFreshnessTarget.OpsCatalog);

        Assert.Equal(IndexFreshness.Fresh, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsStale_WhenPersistedHashDoesNotMatchSnapshot ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexFreshnessPolicy.EvaluateFreshness("different-combined-hash", snapshot, IndexFreshnessTarget.OpsCatalog);

        Assert.Equal(IndexFreshness.Stale, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsFresh_ForGuidPathLookup_WhenOnlyCombinedHashDiffers ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexFreshnessPolicy.EvaluateFreshness(snapshot.GuidPathHash, snapshot, IndexFreshnessTarget.GuidPathLookup);

        Assert.Equal(IndexFreshness.Fresh, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsStale_ForAssetSearchLookup_WhenAssetSearchHashDiffers ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexFreshnessPolicy.EvaluateFreshness("different-asset-search-hash", snapshot, IndexFreshnessTarget.AssetSearchLookup);

        Assert.Equal(IndexFreshness.Stale, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplyModeConstraint_ReturnsFailure_WhenRequireFreshAndFreshnessIsStale ()
    {
        var result = IndexFreshnessPolicy.ApplyModeConstraint(ReadIndexMode.RequireFresh, IndexFreshness.Stale);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFreshRequired, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplyModeConstraint_ReturnsSuccess_WhenAllowStaleAndFreshnessIsStale ()
    {
        var result = IndexFreshnessPolicy.ApplyModeConstraint(ReadIndexMode.AllowStale, IndexFreshness.Stale);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.Null(result.Error);
    }

    private static IndexInputHashSnapshot CreateSnapshot ()
    {
        return new IndexInputHashSnapshot(
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asm-hash",
            AssetsContentHash: "assets-hash",
            AssetSearchHash: "asset-search-hash",
            GuidPathHash: "guid-path-hash",
            CombinedHash: "combined-hash");
    }
}