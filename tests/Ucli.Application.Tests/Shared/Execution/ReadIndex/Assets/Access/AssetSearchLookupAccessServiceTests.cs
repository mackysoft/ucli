using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Assets;

public sealed class AssetSearchLookupAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenAllowStaleIndexExists_ReturnsFilteredIndexEntries ()
    {
        var indexReader = new RecordingReadIndexArtifactReader
        {
            AssetSearchLookupResult = ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>.Success(
                new IndexAssetSearchLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "asset-search-hash",
                    Entries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Spawner.asset", "11111111111111111111111111111111", "Spawner", "Game.Spawner, Assembly-CSharp"),
                        CreateAssetSearchEntry("Assets/Data/Other.asset", "22222222222222222222222222222222", "Other", "Game.Other, Assembly-CSharp"),
                    ])),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var refreshService = new UnexpectedAssetLookupSourceRefreshService();
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);
        var project = ProjectContextTestFactory.CreateUnknownVersionUnityProject();

        var result = await service.SearchAsync(
            project,
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.AllowStale,
            query: new AssetSearchLookupQuery(
                TypeId: "UnityEngine.Object, UnityEngine.CoreModule",
                PathPrefix: "Assets/Data",
                NameContains: "spawn"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.Entries);
        Assert.Equal("Assets/Data/Spawner.asset", result.Output.Entries[0].AssetPath);
        Assert.Equal(AssetLookupSource.Index, result.Output.AccessInfo.Source);
        Assert.True(result.Output.AccessInfo.Used);
        ReadIndexFreshnessInvocationAssert.LookupFreshnessObservedOnce(
            freshnessEvaluator,
            project,
            IndexFreshnessTarget.AssetSearchLookup,
            "asset-search-hash");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var indexReader = new RecordingReadIndexArtifactReader
        {
            AssetSearchLookupResult = ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>.Success(
                new IndexAssetSearchLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "stale-hash",
                    Entries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Stale.asset", "11111111111111111111111111111111", "Stale", "Game.Stale, Assembly-CSharp"),
                    ])),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Stale),
        };
        var refreshService = new RecordingAssetLookupSourceRefreshService
        {
            Result = AssetLookupRefreshResult.Success(
                new IpcIndexAssetsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"),
                    AssetSearchEntries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Fresh.asset", "22222222222222222222222222222222", "Fresh", "Game.Fresh, Assembly-CSharp"),
                    ],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/Data/Fresh.asset"),
                    ]),
                "Existing asset-search index freshness is 'stale'."),
        };
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);

        var result = await service.SearchAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            query: new AssetSearchLookupQuery(TypeId: null, PathPrefix: "Assets/Data", NameContains: "Fresh"),
            failFast: true);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.Entries);
        Assert.Equal("Assets/Data/Fresh.asset", result.Output.Entries[0].AssetPath);
        Assert.Equal(AssetLookupSource.Source, result.Output.AccessInfo.Source);
        RequestReadIndexAccessInvocationAssert.AssetLookupRefreshRequestedOnce(
            refreshService,
            UcliCommandIds.Query,
            expectedFailFast: true);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenReadPostconditionRequiresNewerIndex_FallsBackToSourceEvenWhenAllowStale ()
    {
        var indexReader = new RecordingReadIndexArtifactReader
        {
            AssetSearchLookupResult = ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>.Success(
                new IndexAssetSearchLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    SourceInputsHash: "asset-search-hash",
                    Entries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Stale.asset", "11111111111111111111111111111111", "Stale", "Game.Stale, Assembly-CSharp"),
                    ])),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Success(
                OperationExecutionModelMapper.MapReadPostcondition(new IpcExecuteReadPostcondition(
                [
                    new IpcExecuteReadPostconditionRequirement(
                        Surface: IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
                        MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00")),
                ]))!),
        };
        var refreshService = new RecordingAssetLookupSourceRefreshService
        {
            Result = AssetLookupRefreshResult.Success(
                new IpcIndexAssetsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:10+00:00"),
                    AssetSearchEntries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Fresh.asset", "22222222222222222222222222222222", "Fresh", "Game.Fresh, Assembly-CSharp"),
                    ],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/Data/Fresh.asset"),
                    ]),
                "Existing asset-search index generatedAtUtc is older than mutation read postcondition."),
        };
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService);

        var result = await service.SearchAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.AllowStale,
            query: new AssetSearchLookupQuery(TypeId: null, PathPrefix: "Assets/Data", NameContains: "Fresh"));

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetLookupSource.Source, result.Output!.AccessInfo.Source);
        Assert.Single(result.Output.Entries);
        Assert.Equal("Assets/Data/Fresh.asset", result.Output.Entries[0].AssetPath);
        Assert.Contains("mutation read postcondition", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenQueryIsEmpty_ReturnsInvalidArgument ()
    {
        var service = new AssetSearchLookupAccessService(
            new RecordingReadIndexArtifactReader(),
            new RecordingReadIndexFreshnessEvaluator(),
            new TestMutationReadPostconditionStore(),
            new UnexpectedAssetLookupSourceRefreshService());

        var result = await service.SearchAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            query: new AssetSearchLookupQuery(null, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }

    private static IndexAssetSearchEntryJsonContract CreateAssetSearchEntry (
        string assetPath,
        string assetGuid,
        string name,
        string typeId)
    {
        return new IndexAssetSearchEntryJsonContract(
            AssetPath: assetPath,
            AssetGuid: assetGuid,
            Name: name,
            TypeId: typeId,
            SearchTypeIds:
            [
                typeId,
                "UnityEngine.Object, UnityEngine.CoreModule",
            ]);
    }

}
