using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Assets;

internal static class AssetLookupSourceRefreshAssert
{
    public static void FirstSnapshotReturnedAfterRetryFailureWithoutPersistence (
        AssetLookupRefreshResult result,
        RecordingReadIndexArtifactWriter artifactWriter,
        IpcIndexAssetsReadResponse expectedResponse,
        string expectedFallbackReason,
        string expectedRetryFailureMessage)
    {
        Assert.True(result.IsSuccess);
        Assert.Same(expectedResponse, result.Response);
        Assert.Empty(artifactWriter.AssetLookupInvocations);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains(expectedFallbackReason, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains(expectedRetryFailureMessage, result.FallbackReason!, StringComparison.Ordinal);
    }

    public static void LastSnapshotReturnedAfterUnstableInputsWithoutPersistence (
        AssetLookupRefreshResult result,
        RecordingReadIndexArtifactWriter artifactWriter,
        IpcIndexAssetsReadResponse expectedResponse,
        string expectedFallbackReason)
    {
        Assert.True(result.IsSuccess);
        Assert.Same(expectedResponse, result.Response);
        Assert.Empty(artifactWriter.AssetLookupInvocations);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains(expectedFallbackReason, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
    }
}
