using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;

internal static class SceneTreeLiteSourceRefreshAssert
{
    public static void FirstSnapshotReturnedAfterRetryFailureWithoutPersistence (
        SceneTreeLiteRefreshResult result,
        RecordingReadIndexArtifactWriter artifactWriter,
        IpcIndexSceneTreeLiteReadResponse expectedResponse,
        string expectedFallbackReason,
        string expectedRetryFailureMessage)
    {
        Assert.True(result.IsSuccess);
        Assert.Same(expectedResponse, result.Response);
        Assert.Empty(artifactWriter.SceneTreeLiteInvocations);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains(expectedFallbackReason, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("scene source changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains(expectedRetryFailureMessage, result.FallbackReason!, StringComparison.Ordinal);
    }

    public static void LiveOnlySceneReturnedWithoutPersistence (
        SceneTreeLiteRefreshResult result,
        RecordingSceneTreeLiteSnapshotReader reader,
        RecordingReadIndexSceneSourceHashProvider sourceHashProvider,
        RecordingReadIndexArtifactWriter artifactWriter,
        IpcIndexSceneTreeLiteReadResponse expectedResponse,
        string expectedFallbackReason)
    {
        Assert.True(result.IsSuccess);
        Assert.Same(expectedResponse, result.Response);
        Assert.Equal(expectedFallbackReason, result.FallbackReason);
        SceneTreeLiteSnapshotReaderAssert.ReadRequested(reader, UnityExecutionMode.Auto, expectedFailFast: false);
        Assert.Empty(sourceHashProvider.Invocations);
        Assert.Empty(artifactWriter.SceneTreeLiteInvocations);
    }

    public static void DirtyLiveSourceReturnedWithoutPersistence (
        SceneTreeLiteRefreshResult result,
        RecordingSceneTreeLiteSnapshotReader reader,
        RecordingReadIndexArtifactWriter artifactWriter,
        IpcIndexSceneTreeLiteReadResponse expectedResponse)
    {
        Assert.True(result.IsSuccess);
        Assert.Same(expectedResponse, result.Response);
        SceneTreeLiteSnapshotReaderAssert.ReadRequested(reader, UnityExecutionMode.Auto, expectedFailFast: false);
        Assert.Empty(artifactWriter.SceneTreeLiteInvocations);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains("dirty live editor state", result.FallbackReason!, StringComparison.Ordinal);
    }
}
