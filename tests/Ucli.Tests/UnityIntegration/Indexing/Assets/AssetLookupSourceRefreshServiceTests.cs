using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;

namespace MackySoft.Ucli.Tests.Assets;

public sealed class AssetLookupSourceRefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_PersistsLookup_WhenSnapshotIsStable ()
    {
        var reader = new StubAssetLookupSnapshotReader();
        var response = CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/Stable.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(response));
        var store = new StubAssetLookupStore();
        var stableSnapshot = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var calculator = new StubIndexInputFingerprintCalculator();
        calculator.Enqueue(stableSnapshot);
        calculator.Enqueue(stableSnapshot);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.Refresh(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            readIndexMode: ReadIndexMode.AllowStale,
            fallbackReason: "readIndex stale.",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        Assert.Equal(1, reader.CallCount);
        Assert.True(reader.LastFailFast);
        Assert.Equal(2, calculator.FullCallCount);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(response.GeneratedAtUtc, store.GeneratedAtUtc);
        Assert.Same(stableSnapshot, store.InputSnapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_RetriesAndPersists_WhenInputsChangeDuringFirstSnapshotRead ()
    {
        var reader = new StubAssetLookupSnapshotReader();
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset")));
        var stableResponse = CreateResponse("2026-03-08T00:01:00+00:00", "Assets/Data/Second.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(stableResponse));
        var store = new StubAssetLookupStore();
        var snapshot1 = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateSnapshot("asset-search-2", "guid-path-2", "combined-2");
        var calculator = new StubIndexInputFingerprintCalculator();
        calculator.Enqueue(snapshot1);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.Refresh(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            readIndexMode: ReadIndexMode.AllowStale,
            fallbackReason: "readIndex stale.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(stableResponse, result.Response);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(4, calculator.FullCallCount);
        Assert.Equal(1, store.CallCount);
        Assert.Equal("asset-search-2", store.InputSnapshot!.AssetSearchHash);
        Assert.Equal("Assets/Data/Second.asset", store.AssetSearchEntries![0].AssetPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsFirstSuccessfulSnapshot_WhenRetryReadFails ()
    {
        var reader = new StubAssetLookupSnapshotReader();
        var firstResponse = CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(firstResponse));
        reader.Enqueue(AssetLookupSnapshotFetchResult.Failure("retry read timed out", IpcErrorCodes.InternalError));
        var store = new StubAssetLookupStore();
        var snapshot1 = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateSnapshot("asset-search-2", "guid-path-2", "combined-2");
        var calculator = new StubIndexInputFingerprintCalculator();
        calculator.Enqueue(snapshot1);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.Refresh(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            readIndexMode: ReadIndexMode.AllowStale,
            fallbackReason: "readIndex stale.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(firstResponse, result.Response);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(3, calculator.FullCallCount);
        Assert.Equal(0, store.CallCount);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains("readIndex stale.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("retry snapshot read failed. retry read timed out", result.FallbackReason!, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_SkipsPersist_WhenInputsRemainUnstableAcrossAttempts ()
    {
        var reader = new StubAssetLookupSnapshotReader();
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(CreateResponse("2026-03-08T00:00:00+00:00", "Assets/Data/First.asset")));
        var lastResponse = CreateResponse("2026-03-08T00:01:00+00:00", "Assets/Data/Second.asset");
        reader.Enqueue(AssetLookupSnapshotFetchResult.Success(lastResponse));
        var store = new StubAssetLookupStore();
        var snapshot1 = CreateSnapshot("asset-search-1", "guid-path-1", "combined-1");
        var snapshot2 = CreateSnapshot("asset-search-2", "guid-path-2", "combined-2");
        var snapshot3 = CreateSnapshot("asset-search-3", "guid-path-3", "combined-3");
        var calculator = new StubIndexInputFingerprintCalculator();
        calculator.Enqueue(snapshot1);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot2);
        calculator.Enqueue(snapshot3);
        var service = new AssetLookupSourceRefreshService(reader, store, calculator);

        var result = await service.Refresh(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1000),
            readIndexMode: ReadIndexMode.AllowStale,
            fallbackReason: "readIndex stale.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(lastResponse, result.Response);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(4, calculator.FullCallCount);
        Assert.Equal(0, store.CallCount);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains("readIndex stale.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateProjectContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
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

    private static IndexInputHashSnapshot CreateSnapshot (
        string assetSearchHash,
        string guidPathHash,
        string combinedHash)
    {
        return new IndexInputHashSnapshot(
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asmdef-hash",
            AssetsContentHash: $"{guidPathHash}-assets",
            AssetSearchHash: assetSearchHash,
            GuidPathHash: guidPathHash,
            CombinedHash: combinedHash);
    }

    private sealed class StubAssetLookupSnapshotReader : IAssetLookupSnapshotReader
    {
        private readonly Queue<AssetLookupSnapshotFetchResult> results = new();

        public int CallCount { get; private set; }

        public bool LastFailFast { get; private set; }

        public void Enqueue (AssetLookupSnapshotFetchResult result)
        {
            results.Enqueue(result);
        }

        public ValueTask<AssetLookupSnapshotFetchResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastFailFast = failFast;
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("Asset lookup snapshot result is not configured.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubAssetLookupStore : IAssetLookupStore
    {
        public int CallCount { get; private set; }

        public DateTimeOffset? GeneratedAtUtc { get; private set; }

        public IReadOnlyList<IndexAssetSearchEntryJsonContract>? AssetSearchEntries { get; private set; }

        public IndexInputHashSnapshot? InputSnapshot { get; private set; }

        public ValueTask Write (
            string storageRoot,
            string projectFingerprint,
            DateTimeOffset generatedAtUtc,
            IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
            IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
            IndexInputHashSnapshot inputSnapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            GeneratedAtUtc = generatedAtUtc;
            AssetSearchEntries = assetSearchEntries;
            InputSnapshot = inputSnapshot;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
    {
        private readonly Queue<IndexInputHashSnapshot?> snapshots = new();

        public int FullCallCount { get; private set; }

        public void Enqueue (IndexInputHashSnapshot? snapshot)
        {
            snapshots.Enqueue(snapshot);
        }

        public ValueTask<IndexCoreInputHashSnapshot?> TryComputeCore (
            string projectRootPath,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Core snapshot should not be computed in asset lookup refresh tests.");
        }

        public ValueTask<IndexInputHashSnapshot?> TryCompute (
            string projectRootPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FullCallCount++;
            if (!snapshots.TryDequeue(out var snapshot))
            {
                throw new InvalidOperationException("Input fingerprint snapshot is not configured.");
            }

            return ValueTask.FromResult(snapshot);
        }
    }
}
