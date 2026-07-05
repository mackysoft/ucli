using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Assets;

public sealed class GuidPathLookupAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetGuid_WhenAllowStaleIndexExists_ReturnsIndexEntry ()
    {
        var indexReader = new RecordingReadIndexArtifactReader
        {
            GuidPathLookupResult = ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>.Success(
                new IndexGuidPathLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "guid-path-hash",
                    Entries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Spawner.asset"),
                    ])),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var refreshService = new UnexpectedAssetLookupSourceRefreshService();
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);
        var project = ProjectContextTestFactory.CreateUnknownVersionUnityProject();

        var result = await service.TryResolveAssetGuidAsync(
            project,
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.AllowStale,
            assetGuid: "11111111111111111111111111111111");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal("Assets/Data/Spawner.asset", result.Output!.Entry!.AssetPath);
        Assert.Equal(AssetLookupSource.Index, result.Output.AccessInfo.Source);
        ReadIndexFreshnessInvocationAssert.LookupFreshnessObservedOnce(
            freshnessEvaluator,
            project,
            IndexFreshnessTarget.GuidPathLookup,
            "guid-path-hash");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetPath_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var indexReader = new RecordingReadIndexArtifactReader
        {
            GuidPathLookupResult = ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>.Success(
                new IndexGuidPathLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "guid-path-hash",
                    Entries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Stale.asset"),
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
                        new IndexAssetSearchEntryJsonContract(
                            AssetPath: "Assets/Data/Fresh.asset",
                            AssetGuid: "22222222222222222222222222222222",
                            Name: "Fresh",
                            TypeId: "Game.Fresh, Assembly-CSharp",
                            SearchTypeIds:
                            [
                                "Game.Fresh, Assembly-CSharp",
                                "UnityEngine.Object, UnityEngine.CoreModule",
                            ]),
                    ],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/Data/Fresh.asset"),
                    ]),
                "Existing guid-path index freshness is 'stale'."),
        };
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);

        var result = await service.TryResolveAssetPathAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            assetPath: "Assets/Data/Fresh.asset");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal("22222222222222222222222222222222", result.Output!.Entry!.AssetGuid);
        Assert.Equal(AssetLookupSource.Source, result.Output.AccessInfo.Source);
        RequestReadIndexAccessInvocationAssert.AssetLookupRefreshRequestedOnce(
            refreshService,
            UcliCommandIds.Resolve,
            expectedFailFast: false);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetGuid_WhenReadPostconditionStoreFails_FallsBackToSource ()
    {
        var indexReader = new RecordingReadIndexArtifactReader
        {
            GuidPathLookupResult = ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>.Success(
                new IndexGuidPathLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    SourceInputsHash: "guid-path-hash",
                    Entries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Spawner.asset"),
                    ])),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Failure(
                ExecutionError.InvalidArgument("Mutation read postcondition is invalid: /repo/.ucli/local/fingerprints/project-fingerprint/mutation-read-postcondition.json.")),
        };
        var refreshService = new RecordingAssetLookupSourceRefreshService
        {
            Result = AssetLookupRefreshResult.Success(
                new IpcIndexAssetsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:01:00+00:00"),
                    AssetSearchEntries: [],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Spawner.asset"),
                    ]),
                "Mutation read postcondition is invalid."),
        };
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService);

        var result = await service.TryResolveAssetGuidAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            assetGuid: "11111111111111111111111111111111");

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetLookupSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal("Assets/Data/Spawner.asset", result.Output.Entry!.AssetPath);
        Assert.Contains("Mutation read postcondition", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetPath_WhenPathIsOutsideAssets_ReturnsInvalidArgument ()
    {
        var service = new GuidPathLookupAccessService(
            new RecordingReadIndexArtifactReader(),
            new RecordingReadIndexFreshnessEvaluator(),
            new TestMutationReadPostconditionStore(),
            new UnexpectedAssetLookupSourceRefreshService());

        var result = await service.TryResolveAssetPathAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            assetPath: "Packages/com.example/Test.asset");

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }

}
