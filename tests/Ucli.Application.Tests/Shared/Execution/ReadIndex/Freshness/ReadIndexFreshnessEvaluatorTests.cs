namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class ReadIndexFreshnessEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Observe_ReturnsProbable_WhenPersistedHashIsMissing ()
    {
        var inputProvider = new StubReadIndexInputFingerprintProvider();
        var evaluator = new ReadIndexFreshnessEvaluator(inputProvider, new StubReadIndexSceneSourceHashProvider());

        var result = await evaluator.Observe(
            CreateProject(),
            IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.Equal(0, inputProvider.CoreCallCount);
        Assert.Equal(0, inputProvider.FullCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Observe_ReturnsFresh_WhenCoreHashMatchesOpsCatalogHash ()
    {
        var inputProvider = new StubReadIndexInputFingerprintProvider
        {
            CoreSnapshot = CreateCoreSnapshot("combined-hash"),
        };
        var evaluator = new ReadIndexFreshnessEvaluator(inputProvider, new StubReadIndexSceneSourceHashProvider());

        var result = await evaluator.Observe(
            CreateProject(),
            IndexFreshnessTarget.OpsCatalog,
            "combined-hash",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        Assert.Equal(1, inputProvider.CoreCallCount);
        Assert.Equal(0, inputProvider.FullCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Observe_ReturnsStale_WhenAssetLookupHashDiffers ()
    {
        var inputProvider = new StubReadIndexInputFingerprintProvider
        {
            Snapshot = CreateSnapshot(assetSearchHash: "asset-search-hash", guidPathHash: "guid-path-hash"),
        };
        var evaluator = new ReadIndexFreshnessEvaluator(inputProvider, new StubReadIndexSceneSourceHashProvider());

        var result = await evaluator.Observe(
            CreateProject(),
            IndexFreshnessTarget.AssetSearchLookup,
            "old-asset-search-hash",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.Equal(0, inputProvider.CoreCallCount);
        Assert.Equal(1, inputProvider.FullCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ObserveSceneTreeLite_ReturnsFresh_WhenSceneSourceHashMatches ()
    {
        var sceneHashProvider = new StubReadIndexSceneSourceHashProvider
        {
            SourceHash = "scene-hash",
        };
        var evaluator = new ReadIndexFreshnessEvaluator(new StubReadIndexInputFingerprintProvider(), sceneHashProvider);

        var result = await evaluator.ObserveSceneTreeLite(
            CreateProject(),
            "Assets/Scenes/Main.unity",
            "scene-hash",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        Assert.Equal(1, sceneHashProvider.CallCount);
    }

    private static ResolvedUnityProjectContext CreateProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static ReadIndexCoreInputHashSnapshot CreateCoreSnapshot (string combinedHash)
    {
        return new ReadIndexCoreInputHashSnapshot(
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asmdef-hash",
            CombinedHash: combinedHash);
    }

    private static ReadIndexInputHashSnapshot CreateSnapshot (
        string assetSearchHash,
        string guidPathHash)
    {
        return new ReadIndexInputHashSnapshot(
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asmdef-hash",
            AssetsContentHash: "assets-hash",
            AssetSearchHash: assetSearchHash,
            GuidPathHash: guidPathHash,
            CombinedHash: "combined-hash");
    }

    private sealed class StubReadIndexInputFingerprintProvider : IReadIndexInputFingerprintProvider
    {
        public int CoreCallCount { get; private set; }

        public int FullCallCount { get; private set; }

        public ReadIndexCoreInputHashSnapshot? CoreSnapshot { get; set; }

        public ReadIndexInputHashSnapshot? Snapshot { get; set; }

        public ValueTask<ReadIndexCoreInputHashSnapshot?> TryComputeCore (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CoreCallCount++;
            return ValueTask.FromResult(CoreSnapshot);
        }

        public ValueTask<ReadIndexInputHashSnapshot?> TryCompute (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FullCallCount++;
            return ValueTask.FromResult(Snapshot);
        }
    }

    private sealed class StubReadIndexSceneSourceHashProvider : IReadIndexSceneSourceHashProvider
    {
        public int CallCount { get; private set; }

        public string? SourceHash { get; set; }

        public ValueTask<string?> TryCompute (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(SourceHash);
        }
    }
}
