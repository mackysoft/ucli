using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class ReadIndexFreshnessEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Observe_Throws_WhenPersistedHashIsNull ()
    {
        var inputProvider = new RecordingReadIndexInputFingerprintProvider();
        var evaluator = new ReadIndexFreshnessEvaluator(inputProvider, new RecordingReadIndexSceneSourceHashProvider());

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await evaluator.ObserveAsync(
            ProjectContextTestFactory.CreateUnknownVersionUnityProject(),
            IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: null!,
            CancellationToken.None));

        Assert.Empty(inputProvider.Invocations);
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
            Sha256DigestTestFactory.Compute("combined-hash"),
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
            Sha256DigestTestFactory.Compute("old-asset-search-hash"),
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
            SourceHash = Sha256DigestTestFactory.Compute("scene-hash"),
        };
        var evaluator = new ReadIndexFreshnessEvaluator(new RecordingReadIndexInputFingerprintProvider(), sceneHashProvider);
        var unityProject = ProjectContextTestFactory.CreateUnknownVersionUnityProject();
        var scenePath = new SceneAssetPath("Assets/Scenes/Main.unity");

        var result = await evaluator.ObserveSceneTreeLiteAsync(
            unityProject,
            scenePath,
            Sha256DigestTestFactory.Compute("scene-hash"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        ReadIndexFreshnessInvocationAssert.SceneSourceHashComputedOnce(
            sceneHashProvider,
            unityProject,
            scenePath);
    }

    private static ReadIndexCoreInputHashSnapshot CreateCoreSnapshot (string combinedHash)
    {
        return new ReadIndexCoreInputHashSnapshot(
            Sha256DigestTestFactory.Compute("script-hash"),
            Sha256DigestTestFactory.Compute("manifest-hash"),
            Sha256DigestTestFactory.Compute("lock-hash"),
            Sha256DigestTestFactory.Compute("asmdef-hash"),
            Sha256DigestTestFactory.Compute(combinedHash));
    }

    private static ReadIndexInputHashSnapshot CreateSnapshot (
        string assetSearchHash,
        string guidPathHash)
    {
        return new ReadIndexInputHashSnapshot(
            Sha256DigestTestFactory.Compute("script-hash"),
            Sha256DigestTestFactory.Compute("manifest-hash"),
            Sha256DigestTestFactory.Compute("lock-hash"),
            Sha256DigestTestFactory.Compute("asmdef-hash"),
            Sha256DigestTestFactory.Compute("assets-hash"),
            Sha256DigestTestFactory.Compute(assetSearchHash),
            Sha256DigestTestFactory.Compute(guidPathHash),
            Sha256DigestTestFactory.Compute("combined-hash"));
    }

}
