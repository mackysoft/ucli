using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Indexing;
using MackySoft.Ucli.Tests.Helpers.Indexing.Assets;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;

namespace MackySoft.Ucli.Tests.Assets;

public sealed class AssetLookupSourceRefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_PersistsLookup_WhenSnapshotIsStable ()
    {
        var reader = new RecordingAssetLookupSnapshotReader();
        var response = CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/Stable.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(response));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var stableSnapshot = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var calculator = RecordingReadIndexInputFingerprintProvider.ForFullSnapshotsOnly();
        calculator.Enqueue(stableSnapshot);
        calculator.Enqueue(stableSnapshot);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            fallbackReason: "readIndex stale.",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        AssetLookupSnapshotReaderAssert.ReadRequested(reader, expectedFailFast: true);
        ReadIndexArtifactWriterAssert.AssetLookupWritten(
            store,
            response.GeneratedAtUtc,
            stableSnapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_RetriesAndPersists_WhenInputsChangeDuringFirstSnapshotRead ()
    {
        var reader = new RecordingAssetLookupSnapshotReader();
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset")));
        var stableResponse = CreateResponse("2026-03-08T00:01:00+00:00", "Assets/Data/Second.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(stableResponse));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var snapshot1 = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateSnapshot("asset-search-2", "guid-path-2", "combined-2");
        var calculator = RecordingReadIndexInputFingerprintProvider.ForFullSnapshotsOnly();
        calculator.Enqueue(snapshot1);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            fallbackReason: "readIndex stale.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(stableResponse, result.Response);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        ReadIndexArtifactWriterAssert.AssetLookupWrittenWithInputSnapshot(
            store,
            expectedAssetSearchHash: "asset-search-2",
            expectedAssetPath: "Assets/Data/Second.asset");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsFirstSuccessfulSnapshot_WhenRetryReadFails ()
    {
        var reader = new RecordingAssetLookupSnapshotReader();
        var firstResponse = CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(firstResponse));
        reader.Enqueue(AssetLookupSnapshotFetchResult.Failure("retry read timed out", UcliCoreErrorCodes.InternalError));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var snapshot1 = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateSnapshot("asset-search-2", "guid-path-2", "combined-2");
        var calculator = RecordingReadIndexInputFingerprintProvider.ForFullSnapshotsOnly();
        calculator.Enqueue(snapshot1);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            fallbackReason: "readIndex stale.",
            cancellationToken: CancellationToken.None);

        AssetLookupSourceRefreshAssert.FirstSnapshotReturnedAfterRetryFailureWithoutPersistence(
            result,
            store,
            firstResponse,
            expectedFallbackReason: "readIndex stale.",
            expectedRetryFailureMessage: "retry snapshot read failed. retry read timed out");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_SkipsPersist_WhenInputsRemainUnstableAcrossAttempts ()
    {
        var reader = new RecordingAssetLookupSnapshotReader();
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset")));
        var lastResponse = CreateResponse("2026-03-08T00:01:00+00:00", "Assets/Data/Second.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(lastResponse));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var snapshot1 = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateSnapshot("asset-search-2", "guid-path-2", "combined-2");
        var snapshot3 = CreateSnapshot("asset-search-3", "guid-path-3", "combined-3");
        var calculator = RecordingReadIndexInputFingerprintProvider.ForFullSnapshotsOnly();
        calculator.Enqueue(snapshot1);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot3);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            fallbackReason: "readIndex stale.",
            cancellationToken: CancellationToken.None);

        AssetLookupSourceRefreshAssert.LastSnapshotReturnedAfterUnstableInputsWithoutPersistence(
            result,
            store,
            lastResponse,
            expectedFallbackReason: "readIndex stale.");
    }

    private static IpcIndexAssetsReadResponse CreateResponse (
        string generatedAtUtc,
        string assetPath)
    {
        return new IpcIndexAssetsReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse(generatedAtUtc),
            AssetSearchEntries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: assetPath,
                    AssetGuid: "11111111111111111111111111111111",
                    Name: Path.GetFileNameWithoutExtension(assetPath),
                    TypeId: "Game.Asset, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "Game.Asset, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ],
            GuidPathEntries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "11111111111111111111111111111111",
                    AssetPath: assetPath),
            ]);
    }

    private static ReadIndexInputHashSnapshot CreateSnapshot (
        string assetSearchHash,
        string guidPathHash,
        string combinedHash)
    {
        return new ReadIndexInputHashSnapshot(
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asmdef-hash",
            AssetsContentHash: $"{guidPathHash}-assets",
            AssetSearchHash: assetSearchHash,
            GuidPathHash: guidPathHash,
            CombinedHash: combinedHash);
    }

}
