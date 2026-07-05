namespace MackySoft.Ucli.Tests.Helpers.Indexing.Assets;

internal static class AssetLookupSnapshotReaderAssert
{
    public static RecordingAssetLookupSnapshotReader.Invocation ReadRequested (
        RecordingAssetLookupSnapshotReader reader,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(reader.Invocations);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        return invocation;
    }
}
