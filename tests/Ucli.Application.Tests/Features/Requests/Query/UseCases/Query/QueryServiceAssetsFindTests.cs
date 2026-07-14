using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static QueryServiceTestSupport;

public sealed class QueryServiceAssetsFindTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAssetSearchIndexEntryHasNoGuid_ReturnsNullAssetGuid ()
    {
        var assetSearchLookupAccessService = new RecordingAssetSearchLookupAccessService
        {
            Result = AssetSearchLookupReadResult.Success(
                new AssetSearchLookupReadOutput(
                    Entries:
                    [
                        CreateMaterialAssetEntry("Assets/Planned.mat", string.Empty, "Planned"),
                    ],
                    AccessInfo: CreateAssetLookupAccessInfo()),
                "Asset-search lookup read completed."),
        };
        var service = new QueryService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext)),
            assetSearchLookupAccessService,
            new RecordingSceneTreeLiteAccessService(),
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QueryAssetsFindOperationRequest(
                    CommandName: "query.assets.find",
                    OperationId: "assets.find",
                    OperationName: UcliPrimitiveOperationNames.AssetsFind,
                    Query: new AssetSearchLookupQuery(
                        new UnityTypeId("UnityEngine.Material, UnityEngine.CoreModule"),
                        null,
                        null),
                    WindowOptions: new BoundedWindowOptions(
                        All: true,
                        Limit: 100,
                        Cursor: null,
                        Offset: 0)),
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(Assert.Single(result.OpResults).Result!.Value.GetProperty("matches").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, match.GetProperty("assetGuid").ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAssetsFindLookupSucceeds_ForwardsFailFastAndReturnsWindowedPlanResultWithoutUnityExecution ()
    {
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext));
        var assetSearchLookupAccessService = new RecordingAssetSearchLookupAccessService
        {
            Result = AssetSearchLookupReadResult.Success(
                new AssetSearchLookupReadOutput(
                    Entries:
                    [
                        CreateMaterialAssetEntry("Assets/A.mat", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "A"),
                        CreateMaterialAssetEntry("Assets/B.mat", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "B"),
                    ],
                    AccessInfo: CreateAssetLookupAccessInfo()),
                "Asset-search lookup read completed."),
        };
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService();
        var unityRequestExecutor = new UnexpectedUnityRequestExecutor();
        var service = new QueryService(projectContextResolver, assetSearchLookupAccessService, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QueryAssetsFindOperationRequest(
                    CommandName: "query.assets.find",
                    OperationId: "assets.find",
                    OperationName: UcliPrimitiveOperationNames.AssetsFind,
                    Query: new AssetSearchLookupQuery(
                        new UnityTypeId("UnityEngine.Material, UnityEngine.CoreModule"),
                        null,
                        null),
                    WindowOptions: new BoundedWindowOptions(
                        All: false,
                        Limit: 1,
                        Cursor: null,
                        Offset: 0)),
                failFast: true),
            CancellationToken.None);

        RequestReadIndexAccessInvocationAssert.AssetsFindServedByAssetLookupOnly(
            result,
            assetSearchLookupAccessService,
            sceneTreeLiteAccessService,
            "query.assets.find",
            expectedTypeId: "UnityEngine.Material, UnityEngine.CoreModule",
            expectedFailFast: true);

        var opResult = Assert.Single(result.OpResults);
        Assert.Equal("assets.find", opResult.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.AssetsFind, opResult.Op);
        Assert.True(opResult.Result.HasValue);
        var payload = opResult.Result!.Value;
        Assert.Equal(1, payload.GetProperty("matches").GetArrayLength());
        Assert.Equal(2, payload.GetProperty("window").GetProperty("totalCount").GetInt32());
        Assert.False(payload.GetProperty("window").GetProperty("isComplete").GetBoolean());
        Assert.True(payload.GetProperty("window").TryGetProperty("nextCursor", out var nextCursor));
        Assert.False(string.IsNullOrWhiteSpace(nextCursor.GetString()));
        Assert.False(payload.GetProperty("window").TryGetProperty("after", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAssetsFindWindowHasCursor_ReturnsRequestedCursorWindow ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(1);
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext));
        var assetSearchLookupAccessService = new RecordingAssetSearchLookupAccessService
        {
            Result = AssetSearchLookupReadResult.Success(
                new AssetSearchLookupReadOutput(
                    Entries:
                    [
                        CreateMaterialAssetEntry("Assets/A.mat", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "A"),
                        CreateMaterialAssetEntry("Assets/B.mat", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "B"),
                        CreateMaterialAssetEntry("Assets/C.mat", "cccccccccccccccccccccccccccccccc", "C"),
                    ],
                    AccessInfo: CreateAssetLookupAccessInfo()),
                "Asset-search lookup read completed."),
        };
        var service = new QueryService(
            projectContextResolver,
            assetSearchLookupAccessService,
            new RecordingSceneTreeLiteAccessService(),
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QueryAssetsFindOperationRequest(
                    CommandName: "query.assets.find",
                    OperationId: "assets.find",
                    OperationName: UcliPrimitiveOperationNames.AssetsFind,
                    Query: new AssetSearchLookupQuery(
                        new UnityTypeId("UnityEngine.Material, UnityEngine.CoreModule"),
                        null,
                        null),
                    WindowOptions: new BoundedWindowOptions(
                        All: false,
                        Limit: 1,
                        Cursor: cursor,
                        Offset: 1)),
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Assert.Single(result.OpResults).Result!.Value;
        Assert.Equal("Assets/B.mat", payload.GetProperty("matches")[0].GetProperty("assetPath").GetString());
        Assert.Equal(cursor, payload.GetProperty("window").GetProperty("cursor").GetString());
        Assert.Equal(BoundedWindowCursorCodec.Encode(2), payload.GetProperty("window").GetProperty("nextCursor").GetString());
        Assert.Equal(3, payload.GetProperty("window").GetProperty("totalCount").GetInt32());
    }

    private static IndexAssetSearchEntryJsonContract CreateMaterialAssetEntry (
        string assetPath,
        string assetGuid,
        string name)
    {
        return new IndexAssetSearchEntryJsonContract(
            AssetPath: assetPath,
            AssetGuid: assetGuid,
            Name: name,
            TypeId: "UnityEngine.Material, UnityEngine.CoreModule",
            SearchTypeIds: ["UnityEngine.Material, UnityEngine.CoreModule"]);
    }

    private static AssetLookupAccessInfo CreateAssetLookupAccessInfo ()
    {
        return new AssetLookupAccessInfo(
            Used: true,
            Hit: true,
            Source: AssetLookupSource.Index,
            Freshness: IndexFreshness.Fresh,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
            FallbackReason: null);
    }
}
