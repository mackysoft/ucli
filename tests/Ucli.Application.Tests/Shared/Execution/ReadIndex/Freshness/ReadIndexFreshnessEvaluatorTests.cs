namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class ReadIndexFreshnessEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Observe_WhenPersistedHashIsMissing_ReturnsProbableWithoutComputingInputFingerprint ()
    {
        var inputProvider = new RecordingReadIndexInputFingerprintProvider();
        var evaluator = new ReadIndexFreshnessEvaluator(inputProvider, new RecordingReadIndexSceneSourceHashProvider());

        var result = await evaluator.ObserveAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: null,
            CancellationToken.None);

        ReadIndexFreshnessInvocationAssert.PersistedHashMissingReturnedProbableWithoutInputFingerprint(
            result,
            inputProvider);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Observe_ReturnsFresh_WhenCoreHashMatchesOpsCatalogHash ()
    {
        var inputProvider = new RecordingReadIndexInputFingerprintProvider
        {
            CoreSnapshot = CreateCoreSnapshot("combined-hash"),
        };
        var evaluator = new ReadIndexFreshnessEvaluator(inputProvider, new RecordingReadIndexSceneSourceHashProvider());
        var unityProject = ProjectContextTestFactory.CreateUnknownVersionUnityProject();

        var result = await evaluator.ObserveAsync(
            unityProject,
            IndexFreshnessTarget.OpsCatalog,
            "combined-hash",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        ReadIndexFreshnessInvocationAssert.CoreInputFingerprintComputedOnce(
            inputProvider,
            unityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Observe_ReturnsStale_WhenAssetLookupHashDiffers ()
    {
        var inputProvider = new RecordingReadIndexInputFingerprintProvider
        {
            Snapshot = CreateSnapshot(assetSearchHash: "asset-search-hash", guidPathHash: "guid-path-hash"),
        };
        var evaluator = new ReadIndexFreshnessEvaluator(inputProvider, new RecordingReadIndexSceneSourceHashProvider());
        var unityProject = ProjectContextTestFactory.CreateUnknownVersionUnityProject();

        var result = await evaluator.ObserveAsync(
            unityProject,
            IndexFreshnessTarget.AssetSearchLookup,
            "old-asset-search-hash",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        ReadIndexFreshnessInvocationAssert.FullInputFingerprintComputedOnce(
            inputProvider,
            unityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ObserveSceneTreeLite_ReturnsFresh_WhenSceneSourceHashMatches ()
    {
        var sceneHashProvider = new RecordingReadIndexSceneSourceHashProvider
        {
            SourceHash = "scene-hash",
        };
        var evaluator = new ReadIndexFreshnessEvaluator(new RecordingReadIndexInputFingerprintProvider(), sceneHashProvider);
        var unityProject = ProjectContextTestFactory.CreateUnknownVersionUnityProject();

        var result = await evaluator.ObserveSceneTreeLiteAsync(
            unityProject,
            "Assets/Scenes/Main.unity",
            "scene-hash",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        ReadIndexFreshnessInvocationAssert.SceneSourceHashComputedOnce(
            sceneHashProvider,
            unityProject,
            "Assets/Scenes/Main.unity");
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

}
