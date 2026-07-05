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
            CreateInput(
                new QuerySceneTreeOperationRequest(
                    CommandName: "query.scene.tree",
                    OperationId: "scene.tree",
                    OperationName: UcliPrimitiveOperationNames.SceneTree,
                    ScenePath: "Assets/Scenes/Main.unity",
                    Depth: 1,
                    WindowOptions: new BoundedWindowOptions(
                        All: false,
                        Limit: 2,
                        Cursor: null,
                        Offset: 0)),
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
            CreateInput(
                new QuerySceneTreeOperationRequest(
                    CommandName: "query.scene.tree",
                    OperationId: "scene.tree",
                    OperationName: UcliPrimitiveOperationNames.SceneTree,
                    ScenePath: "Assets/Scenes/Main.unity",
                    Depth: 1,
                    WindowOptions: new BoundedWindowOptions(
                        All: false,
                        Limit: 1,
                        Cursor: cursor,
                        Offset: 1)),
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

    private static SceneTreeLiteReadResult CreateSceneTreeLiteReadResult ()
    {
        return SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                ScenePath: "Assets/Scenes/Main.unity",
                Roots:
                [
                    new IndexSceneTreeLiteNodeJsonContract(
                        "Root",
                        "GlobalObjectId_V1-1-2-3-4-5-6",
                        [
                            new IndexSceneTreeLiteNodeJsonContract("First", "GlobalObjectId_V1-1-2-3-4-5-7", [], IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                            new IndexSceneTreeLiteNodeJsonContract("Second", "GlobalObjectId_V1-1-2-3-4-5-8", [], IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                        ],
                        IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                ],
                SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
                AccessInfo: new SceneTreeLiteAccessInfo(
                    Used: true,
                    Hit: true,
                    Source: SceneTreeLiteSource.Index,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                    FallbackReason: null)),
            "Scene-tree-lite read completed.");
    }
}
