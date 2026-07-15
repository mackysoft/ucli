using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static QueryServiceTestSupport;

public sealed class QueryServiceSceneTreeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneTreeLookupSucceeds_ForwardsFailFastAndReturnsWindowedRoots ()
    {
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext));
        var assetSearchLookupAccessService = new RecordingAssetSearchLookupAccessService();
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService
        {
            Result = CreateSceneTreeLiteReadResult(),
        };
        var unityRequestExecutor = new UnexpectedUnityRequestExecutor();
        var service = new QueryService(projectContextResolver, assetSearchLookupAccessService, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QuerySceneTreeOperationRequest(
                    CommandName: "query.scene.tree",
                    OperationId: new IpcExecuteStepId("scene.tree"),
                    OperationName: UcliPrimitiveOperationNames.SceneTree,
                    ScenePath: new UnityScenePath("Assets/Scenes/Main.unity"),
                    Depth: 1,
                    WindowOptions: BoundedWindowOptions.CreateBounded(limit: 2, cursor: null)),
                failFast: true),
            CancellationToken.None);

        RequestReadIndexAccessInvocationAssert.SceneTreeServedBySceneTreeLiteOnly(
            result,
            assetSearchLookupAccessService,
            sceneTreeLiteAccessService,
            UcliCommandIds.Query,
            expectedScenePath: "Assets/Scenes/Main.unity",
            expectedReadIndexMode: ReadIndexMode.RequireFresh,
            expectedFailFast: true);

        var opResult = Assert.Single(result.OpResults);
        var payload = opResult.Result!.Value;
        Assert.Equal("Assets/Scenes/Main.unity", payload.GetProperty("path").GetString());
        Assert.Equal(1, payload.GetProperty("roots").GetArrayLength());
        Assert.Equal(1, payload.GetProperty("roots")[0].GetProperty("children").GetArrayLength());
        Assert.Equal("truncatedByWindow", payload.GetProperty("roots")[0].GetProperty("childrenState").GetString());
        Assert.Equal("readIndex", payload.GetProperty("sourceState").GetProperty("kind").GetString());
        Assert.False(payload.GetProperty("window").GetProperty("isComplete").GetBoolean());
        Assert.Equal(3, payload.GetProperty("window").GetProperty("totalCount").GetInt32());
        Assert.False(payload.GetProperty("window").TryGetProperty("after", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneTreeWindowHasCursor_ReturnsFragmentRootWindow ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(1);
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext));
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService
        {
            Result = CreateSceneTreeLiteReadResult(),
        };
        var service = new QueryService(
            projectContextResolver,
            new RecordingAssetSearchLookupAccessService(),
            sceneTreeLiteAccessService,
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QuerySceneTreeOperationRequest(
                    CommandName: "query.scene.tree",
                    OperationId: new IpcExecuteStepId("scene.tree"),
                    OperationName: UcliPrimitiveOperationNames.SceneTree,
                    ScenePath: new UnityScenePath("Assets/Scenes/Main.unity"),
                    Depth: 1,
                    WindowOptions: BoundedWindowOptions.CreateBounded(limit: 1, cursor)),
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Assert.Single(result.OpResults).Result!.Value;
        Assert.Equal(1, payload.GetProperty("roots").GetArrayLength());
        Assert.Equal("First", payload.GetProperty("roots")[0].GetProperty("name").GetString());
        Assert.Equal(cursor, payload.GetProperty("window").GetProperty("cursor").GetString());
        Assert.Equal(BoundedWindowCursorCodec.Encode(2), payload.GetProperty("window").GetProperty("nextCursor").GetString());
        Assert.Equal(3, payload.GetProperty("window").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPackageSceneUsesLiveSource_ReturnsPackageScenePath ()
    {
        var scenePath = new UnityScenePath("Packages/com.example/Scenes/Main.unity");
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService
        {
            Result = CreateSceneTreeLiteReadResult(scenePath, SceneTreeLiteSource.Source),
        };
        var service = new QueryService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext)),
            new RecordingAssetSearchLookupAccessService(),
            sceneTreeLiteAccessService,
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QuerySceneTreeOperationRequest(
                    CommandName: "query.scene.tree",
                    OperationId: new IpcExecuteStepId("scene.tree"),
                    OperationName: UcliPrimitiveOperationNames.SceneTree,
                    ScenePath: scenePath,
                    Depth: 1,
                    WindowOptions: BoundedWindowOptions.CreateBounded(limit: 2, cursor: null)),
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Assert.Single(result.OpResults).Result!.Value;
        Assert.Equal(scenePath.Value, payload.GetProperty("path").GetString());
    }

    private static SceneTreeLiteReadResult CreateSceneTreeLiteReadResult (
        UnityScenePath? scenePath = null,
        SceneTreeLiteSource source = SceneTreeLiteSource.Index)
    {
        var resolvedScenePath = scenePath ?? new UnityScenePath("Assets/Scenes/Main.unity");
        return SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                ScenePath: resolvedScenePath,
                Roots:
                [
                    new SceneTreeLiteNode(
                        "Root",
                        new UnityGlobalObjectId("GlobalObjectId_V1-2-11111111111111111111111111111111-1-0"),
                        [
                            new SceneTreeLiteNode(
                                "First",
                                new UnityGlobalObjectId("GlobalObjectId_V1-2-11111111111111111111111111111111-2-0"),
                                [],
                                IndexSceneTreeLiteNodeChildrenState.Complete),
                            new SceneTreeLiteNode(
                                "Second",
                                new UnityGlobalObjectId("GlobalObjectId_V1-2-11111111111111111111111111111111-3-0"),
                                [],
                                IndexSceneTreeLiteNodeChildrenState.Complete),
                        ],
                        IndexSceneTreeLiteNodeChildrenState.Complete),
                ],
                SourceState: new SceneTreeSourceState(
                    source == SceneTreeLiteSource.Index
                        ? SceneTreeSourceStateKind.ReadIndex
                        : SceneTreeSourceStateKind.PersistedPreview,
                    isDirty: false),
                AccessInfo: new SceneTreeLiteAccessInfo(
                    Used: source == SceneTreeLiteSource.Index,
                    Hit: true,
                    Source: source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                    FallbackReason: null)),
            "Scene-tree-lite read completed.");
    }
}
