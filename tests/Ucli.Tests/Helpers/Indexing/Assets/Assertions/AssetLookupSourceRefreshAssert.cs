using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Assets;

internal static class AssetLookupSourceRefreshAssert
{
    public static void FirstSnapshotReturnedAfterRetryFailureWithoutPersistence (
        AssetLookupRefreshResult result,
        RecordingReadIndexArtifactWriter artifactWriter,
        AssetLookupSnapshot expectedSnapshot,
        string expectedFallbackReason,
        string expectedRetryFailureMessage)
    {
        Assert.True(result.IsSuccess);
        Assert.Same(expectedSnapshot, result.Snapshot);
        Assert.Empty(artifactWriter.AssetLookupInvocations);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains(expectedFallbackReason, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains(expectedRetryFailureMessage, result.FallbackReason!, StringComparison.Ordinal);
    }

    public static void LastSnapshotReturnedAfterUnstableInputsWithoutPersistence (
        AssetLookupRefreshResult result,
        RecordingReadIndexArtifactWriter artifactWriter,
        AssetLookupSnapshot expectedSnapshot,
        string expectedFallbackReason)
    {
        Assert.True(result.IsSuccess);
        Assert.Same(expectedSnapshot, result.Snapshot);
        Assert.Empty(artifactWriter.AssetLookupInvocations);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains(expectedFallbackReason, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
    }
}
