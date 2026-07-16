using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Assets;

public sealed class AssetLookupSnapshotTests
{
    private static readonly DateTimeOffset GeneratedAtUtc =
        new(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);

    public static TheoryData<DateTimeOffset> InvalidGenerationTimestamps => new()
    {
        default,
        new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.FromHours(9)),
    };

    [Theory]
    [MemberData(nameof(InvalidGenerationTimestamps))]
    [Trait("Size", "Small")]
    public void TryCreate_ReturnsFalse_WhenGenerationTimestampIsNotNonDefaultUtc (DateTimeOffset generatedAtUtc)
    {
        var result = AssetLookupSnapshot.TryCreate(
            generatedAtUtc,
            [],
            [],
            out var snapshot,
            out var error);

        Assert.False(result);
        Assert.Null(snapshot);
        Assert.Equal("Lookup generation timestamp must be a non-default UTC value.", error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_ReturnsFalse_WhenLookupCollectionContainsNull ()
    {
        var assetSearchEntries = new AssetSearchLookupEntry[] { null! };

        var result = AssetLookupSnapshot.TryCreate(
            GeneratedAtUtc,
            assetSearchEntries,
            [],
            out var snapshot,
            out var error);

        Assert.False(result);
        Assert.Null(snapshot);
        Assert.Equal("Lookup entry collections must not contain null values.", error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_CopiesCallerOwnedLookupCollections ()
    {
        var assetGuid = Guid.Parse("3c65bbf1-f30a-4a4c-9cfb-d58891a799b7");
        var assetPath = new UnityAssetPath("Assets/Data.asset");
        var assetSearchEntry = new AssetSearchLookupEntry(
            assetPath,
            assetGuid,
            "Data",
            new UnityTypeId("Example.Data"),
            [new UnityTypeId("Example.Data")]);
        var guidPathEntry = new GuidPathLookupEntry(assetGuid, assetPath);
        var assetSearchEntries = new[] { assetSearchEntry };
        var guidPathEntries = new[] { guidPathEntry };

        var result = AssetLookupSnapshot.TryCreate(
            GeneratedAtUtc,
            assetSearchEntries,
            guidPathEntries,
            out var snapshot,
            out var error);

        Assert.True(result, error);
        assetSearchEntries[0] = null!;
        guidPathEntries[0] = null!;
        Assert.Same(assetSearchEntry, Assert.Single(snapshot!.AssetSearchEntries));
        Assert.Same(guidPathEntry, Assert.Single(snapshot.GuidPathEntries));
        Assert.Throws<NotSupportedException>(
            () => ((IList<AssetSearchLookupEntry>)snapshot.AssetSearchEntries).Add(assetSearchEntry));
        Assert.Throws<NotSupportedException>(
            () => ((IList<GuidPathLookupEntry>)snapshot.GuidPathEntries).Add(guidPathEntry));
    }
}
