using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
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
        var lookupSnapshot = AssetLookupSnapshotTestFactory.Create("2026-03-08T00:00:00+00:00", "Assets/Data/Stable.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(lookupSnapshot));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var stableInputSnapshot = CreateInputSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var calculator = RecordingReadIndexInputFingerprintProvider.ForFullSnapshotsOnly();
        calculator.Enqueue(stableInputSnapshot);
        calculator.Enqueue(stableInputSnapshot);
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
        Assert.Same(lookupSnapshot, result.Snapshot);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        AssetLookupSnapshotReaderAssert.ReadRequested(reader, expectedFailFast: true);
        ReadIndexArtifactWriterAssert.AssetLookupWritten(
            store,
            lookupSnapshot.GeneratedAtUtc,
            stableInputSnapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_RetriesAndPersists_WhenInputsChangeDuringFirstSnapshotRead ()
    {
        var reader = new RecordingAssetLookupSnapshotReader();
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(AssetLookupSnapshotTestFactory.Create("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset")));
        var stableLookupSnapshot = AssetLookupSnapshotTestFactory.Create("2026-03-08T00:01:00+00:00", "Assets/Data/Second.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(stableLookupSnapshot));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var snapshot1 = CreateInputSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateInputSnapshot("asset-search-2", "guid-path-2", "combined-2");
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
        Assert.Same(stableLookupSnapshot, result.Snapshot);
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
        var firstLookupSnapshot = AssetLookupSnapshotTestFactory.Create("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(firstLookupSnapshot));
        reader.Enqueue(AssetLookupSnapshotFetchResult.Failure("retry read timed out", UcliCoreErrorCodes.InternalError));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var snapshot1 = CreateInputSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateInputSnapshot("asset-search-2", "guid-path-2", "combined-2");
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
            firstLookupSnapshot,
            expectedFallbackReason: "readIndex stale.",
            expectedRetryFailureMessage: "retry snapshot read failed. retry read timed out");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_SkipsPersist_WhenInputsRemainUnstableAcrossAttempts ()
    {
        var reader = new RecordingAssetLookupSnapshotReader();
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(AssetLookupSnapshotTestFactory.Create("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset")));
        var lastLookupSnapshot = AssetLookupSnapshotTestFactory.Create("2026-03-08T00:01:00+00:00", "Assets/Data/Second.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(lastLookupSnapshot));
        var store = RecordingReadIndexArtifactWriter.ForAssetLookups();
        var snapshot1 = CreateInputSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateInputSnapshot("asset-search-2", "guid-path-2", "combined-2");
        var snapshot3 = CreateInputSnapshot("asset-search-3", "guid-path-3", "combined-3");
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
            lastLookupSnapshot,
            expectedFallbackReason: "readIndex stale.");
    }

    private static ReadIndexInputHashSnapshot CreateInputSnapshot (
        string assetSearchHash,
        string guidPathHash,
        string combinedHash)
    {
        return new ReadIndexInputHashSnapshot(
            Sha256DigestTestFactory.Compute("script-hash"),
            Sha256DigestTestFactory.Compute("manifest-hash"),
            Sha256DigestTestFactory.Compute("lock-hash"),
            Sha256DigestTestFactory.Compute("asmdef-hash"),
            Sha256DigestTestFactory.Compute($"{guidPathHash}-assets"),
            Sha256DigestTestFactory.Compute(assetSearchHash),
            Sha256DigestTestFactory.Compute(guidPathHash),
            Sha256DigestTestFactory.Compute(combinedHash));
    }

}
