using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Assets;

internal static class AssetLookupSnapshotAssert
{
    public static void Equal (AssetLookupSnapshot expected, AssetLookupSnapshot? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.GeneratedAtUtc, actual.GeneratedAtUtc);
        Assert.Equal(expected.AssetSearchEntries.Count, actual.AssetSearchEntries.Count);
        for (var i = 0; i < expected.AssetSearchEntries.Count; i++)
        {
            var expectedEntry = expected.AssetSearchEntries[i];
            var actualEntry = actual.AssetSearchEntries[i];
            Assert.Equal(expectedEntry.AssetPath, actualEntry.AssetPath);
            Assert.Equal(expectedEntry.AssetGuid, actualEntry.AssetGuid);
            Assert.Equal(expectedEntry.Name, actualEntry.Name);
            Assert.Equal(expectedEntry.TypeId, actualEntry.TypeId);
            Assert.Equal(expectedEntry.SearchTypeIds, actualEntry.SearchTypeIds);
        }

        Assert.Equal(expected.GuidPathEntries.Count, actual.GuidPathEntries.Count);
        for (var i = 0; i < expected.GuidPathEntries.Count; i++)
        {
            var expectedEntry = expected.GuidPathEntries[i];
            var actualEntry = actual.GuidPathEntries[i];
            Assert.Equal(expectedEntry.AssetGuid, actualEntry.AssetGuid);
            Assert.Equal(expectedEntry.AssetPath, actualEntry.AssetPath);
        }
    }
}
