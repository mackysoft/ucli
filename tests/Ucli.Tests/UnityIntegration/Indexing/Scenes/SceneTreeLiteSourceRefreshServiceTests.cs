using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Indexing;
using MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteSourceRefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_PersistsLookup_WhenSnapshotIsStable ()
    {
        var reader = new RecordingSceneTreeLiteSnapshotReader();
        var response = CreateResponse("Assets/Scenes/Main.unity", "Root");
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(response));
        var store = RecordingReadIndexArtifactWriter.ForSceneTreeLite();
        var calculator = RecordingReadIndexSceneSourceHashProvider.ForQueuedResults();
        calculator.Enqueue("hash-1");
        calculator.Enqueue("hash-1");
        var service = new SceneTreeLiteSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            "readIndex stale.",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        SceneTreeLiteSnapshotReaderAssert.ReadRequested(reader, UnityExecutionMode.Auto, expectedFailFast: true);
        ReadIndexArtifactWriterAssert.SceneTreeLiteWritten(store, "hash-1");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsFirstSnapshot_WhenRetryReadFails ()
    {
        var reader = new RecordingSceneTreeLiteSnapshotReader();
        var firstResponse = CreateResponse("Assets/Scenes/Main.unity", "First");
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(firstResponse));
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Failure("retry read timed out", UcliCoreErrorCodes.InternalError));
        var store = RecordingReadIndexArtifactWriter.ForSceneTreeLite();
        var calculator = RecordingReadIndexSceneSourceHashProvider.ForQueuedResults();
        calculator.Enqueue("hash-1");
        calculator.Enqueue("hash-2");
        calculator.Enqueue("hash-2");
        var service = new SceneTreeLiteSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            "readIndex stale.",
            cancellationToken: CancellationToken.None);

        SceneTreeLiteSourceRefreshAssert.FirstSnapshotReturnedAfterRetryFailureWithoutPersistence(
            result,
            store,
            firstResponse,
            expectedFallbackReason: "readIndex stale.",
            expectedRetryFailureMessage: "retry snapshot read failed. retry read timed out");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenSceneIsOutsideAssets_SkipsPersistence ()
    {
        var reader = new RecordingSceneTreeLiteSnapshotReader();
        var response = CreateResponse("Packages/com.example/Scenes/Main.unity", "PackageRoot");
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(response));
        var store = RecordingReadIndexArtifactWriter.ForSceneTreeLite();
        var calculator = RecordingReadIndexSceneSourceHashProvider.ForQueuedResults();
        var service = new SceneTreeLiteSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Packages/com.example/Scenes/Main.unity",
            "scene-tree-lite readIndex is unavailable for non-Assets scene paths.",
            cancellationToken: CancellationToken.None);

        SceneTreeLiteSourceRefreshAssert.LiveOnlySceneReturnedWithoutPersistence(
            result,
            reader,
            calculator,
            store,
            response,
            expectedFallbackReason: "scene-tree-lite readIndex is unavailable for non-Assets scene paths.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenSnapshotIsDirtyLoadedSource_SkipsPersistence ()
    {
        var reader = new RecordingSceneTreeLiteSnapshotReader();
        var response = CreateResponse(
            "Assets/Scenes/Main.unity",
            "DirtyRoot",
            new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: true));
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(response));
        var store = RecordingReadIndexArtifactWriter.ForSceneTreeLite();
        var calculator = RecordingReadIndexSceneSourceHashProvider.ForQueuedResults();
        calculator.Enqueue("hash-1");
        var service = new SceneTreeLiteSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            "readIndex stale.",
            cancellationToken: CancellationToken.None);

        SceneTreeLiteSourceRefreshAssert.DirtyLiveSourceReturnedWithoutPersistence(
            result,
            reader,
            store,
            response);
    }

    private static IpcIndexSceneTreeLiteReadResponse CreateResponse (
        string scenePath,
        string rootName,
        SceneTreeSourceState? sourceState = null)
    {
        return new IpcIndexSceneTreeLiteReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
            ScenePath: scenePath,
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(rootName, "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ],
            SourceState: sourceState ?? new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false));
    }

}
