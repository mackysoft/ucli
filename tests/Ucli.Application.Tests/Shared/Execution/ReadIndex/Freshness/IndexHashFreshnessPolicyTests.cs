namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexHashFreshnessPolicyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsFresh_WhenPersistedHashMatchesSnapshot ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexHashFreshnessPolicy.EvaluateFreshness(snapshot.CombinedHash, snapshot, IndexFreshnessTarget.OpsCatalog);

        Assert.Equal(IndexFreshness.Fresh, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsStale_WhenPersistedHashDoesNotMatchSnapshot ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexHashFreshnessPolicy.EvaluateFreshness(
            Sha256DigestTestFactory.Compute("different-combined-hash"),
            snapshot,
            IndexFreshnessTarget.OpsCatalog);

        Assert.Equal(IndexFreshness.Stale, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsFresh_ForGuidPathLookup_WhenOnlyCombinedHashDiffers ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexHashFreshnessPolicy.EvaluateFreshness(snapshot.GuidPathHash, snapshot, IndexFreshnessTarget.GuidPathLookup);

        Assert.Equal(IndexFreshness.Fresh, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsStale_ForAssetSearchLookup_WhenAssetSearchHashDiffers ()
    {
        var snapshot = CreateSnapshot();

        var result = IndexHashFreshnessPolicy.EvaluateFreshness(
            Sha256DigestTestFactory.Compute("different-asset-search-hash"),
            snapshot,
            IndexFreshnessTarget.AssetSearchLookup);

        Assert.Equal(IndexFreshness.Stale, result);
    }

    private static ReadIndexInputHashSnapshot CreateSnapshot ()
    {
        return new ReadIndexInputHashSnapshot(
            Sha256DigestTestFactory.Compute("script-hash"),
            Sha256DigestTestFactory.Compute("manifest-hash"),
            Sha256DigestTestFactory.Compute("lock-hash"),
            Sha256DigestTestFactory.Compute("asm-hash"),
            Sha256DigestTestFactory.Compute("assets-hash"),
            Sha256DigestTestFactory.Compute("asset-search-hash"),
            Sha256DigestTestFactory.Compute("guid-path-hash"),
            Sha256DigestTestFactory.Compute("combined-hash"));
    }
}
