using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Indexing;
using MackySoft.Ucli.Tests.Helpers.OperationCatalog;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Tests.Features.OperationCatalog.Catalog.Source;

public sealed class OpsCatalogSourceRefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_PersistsOpsCatalog_WhenCoreAndFullSnapshotsAreAvailable ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var reader = new RecordingOpsCatalogReader
        {
            Result = CreateFetchResult(generatedAtUtc, [CreateGoDescribeEntry()]),
        };
        var fingerprintProvider = new RecordingReadIndexInputFingerprintProvider
        {
            CoreSnapshot = CreateCoreSnapshot("combined"),
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined"),
        };
        var artifactWriter = RecordingReadIndexArtifactWriter.ForOpsCatalog();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        OpsCatalogReaderAssert.ReadRequiresReadinessGate(reader);
        OpsCatalogSourceRefreshAssert.PersistedWithFullInputSnapshot(
            result,
            fingerprintProvider,
            artifactWriter,
            expectedFallbackReason: "readIndex stale.",
            expectedSourceInputsHash: "combined",
            expectedAssetSearchHash: "asset-search");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReusesPersistedManifestAssetHashesWithoutFullFingerprint ()
    {
        var reader = new RecordingOpsCatalogReader
        {
            Result = CreateFetchResult(
                DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                [CreateGoDescribeEntry()]),
        };
        var persistedArtifactsReader = new StubPersistedOpsCatalogPersistenceArtifactsReader
        {
            Result = new PersistedOpsCatalogPersistenceArtifacts(
                InputsManifest: new IndexInputsManifestJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    ScriptAssembliesHash: "old-script",
                    PackagesManifestHash: "old-manifest",
                    PackagesLockHash: "old-lock",
                    AssemblyDefinitionHash: "old-asmdef",
                    AssetsContentHash: "existing-assets",
                    AssetSearchHash: "existing-asset-search",
                    GuidPathHash: "existing-guid-path",
                    CombinedHash: "old-combined"),
                HasPersistedAssetLookupArtifacts: true),
        };
        var fingerprintProvider = new RecordingReadIndexInputFingerprintProvider
        {
            CoreSnapshot = CreateCoreSnapshot("new-combined"),
            ThrowOnTryCompute = true,
        };
        var artifactWriter = RecordingReadIndexArtifactWriter.ForOpsCatalog();
        var service = new OpsCatalogSourceRefreshService(reader, persistedArtifactsReader, fingerprintProvider, artifactWriter);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        OpsCatalogSourceRefreshAssert.PersistedWithReusedManifestAssetHashes(
            result,
            fingerprintProvider,
            artifactWriter,
            expectedSourceInputsHash: "new-combined",
            expectedAssetsContentHash: "existing-assets",
            expectedAssetSearchHash: "existing-asset-search",
            expectedGuidPathHash: "existing-guid-path",
            expectedCombinedHash: "new-combined");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsSourceResultWithPersistenceFailureReason ()
    {
        var reader = new RecordingOpsCatalogReader
        {
            Result = CreateFetchResult(
                DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                [CreateGoDescribeEntry()]),
        };
        var artifactWriter = RecordingReadIndexArtifactWriter.ForOpsCatalog();
        artifactWriter.WriteException = new InvalidOperationException("disk full");
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            new RecordingReadIndexInputFingerprintProvider
            {
                CoreSnapshot = CreateCoreSnapshot("combined"),
                Snapshot = CreateSnapshot("asset-search", "guid-path", "combined"),
            },
            artifactWriter);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex disabled by mode.",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains("readIndex disabled by mode.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("Failed to persist refreshed ops readIndex. disk full", result.FallbackReason!, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsSourceResultWithFingerprintFailureReason_WhenCoreSnapshotBeforeReadIsMissing ()
    {
        var reader = new RecordingOpsCatalogReader
        {
            Result = CreateFetchResult(
                DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                [CreateGoDescribeEntry()]),
        };
        var fingerprintProvider = new RecordingReadIndexInputFingerprintProvider
        {
            CoreSnapshot = null,
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined"),
        };
        var artifactWriter = RecordingReadIndexArtifactWriter.ForOpsCatalog();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        OpsCatalogSourceRefreshAssert.SourceResultReturnedWithFingerprintFailureBeforePersistence(
            result,
            reader,
            fingerprintProvider,
            artifactWriter,
            expectedFallbackReason: "readIndex stale.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsFirstSourceResultWithRetryFailureReason_WhenRetryCatalogReadFails ()
    {
        var reader = new RecordingOpsCatalogReader();
        reader.Enqueue(CreateFetchResult(
            DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
            [CreateGoDescribeEntry()]));
        reader.Enqueue(OpsCatalogFetchResult.Failure("Unity source unavailable.", UcliCoreErrorCodes.InternalError));
        var fingerprintProvider = new RecordingReadIndexInputFingerprintProvider
        {
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined-2"),
        };
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-1"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        var artifactWriter = RecordingReadIndexArtifactWriter.ForOpsCatalog();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        OpsCatalogSourceRefreshAssert.FirstSourceResultReturnedAfterRetryFailureWithoutPersistence(
            result,
            artifactWriter,
            expectedFirstOperationName: UcliPrimitiveOperationNames.GoDescribe,
            expectedFallbackReason: "readIndex stale.",
            expectedRetryFailureMessage: "retry catalog read failed. Unity source unavailable.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_RetriesAndPersists_WhenCoreInputsChangeDuringFirstCatalogRead ()
    {
        var reader = new RecordingOpsCatalogReader();
        reader.Enqueue(CreateFetchResult(
            DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
            [CreateGoDescribeEntry()]));
        reader.Enqueue(CreateFetchResult(
            DateTimeOffset.Parse("2026-03-07T00:01:00+00:00"),
            [CreateSceneSaveEntry()]));
        var fingerprintProvider = new RecordingReadIndexInputFingerprintProvider
        {
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined-2"),
        };
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-1"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        var artifactWriter = RecordingReadIndexArtifactWriter.ForOpsCatalog();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Snapshot!.Operations);
        Assert.Equal(UcliPrimitiveOperationNames.SceneSave, result.Snapshot.Operations[0].Name);
        ReadIndexArtifactWriterAssert.OpsCatalogWritten(artifactWriter, expectedSourceInputsHash: "combined-2");
    }

    private static ReadIndexCoreInputHashSnapshot CreateCoreSnapshot (string combinedHash)
    {
        return new ReadIndexCoreInputHashSnapshot(
            ScriptAssembliesHash: "script",
            PackagesManifestHash: "manifest",
            PackagesLockHash: "lock",
            AssemblyDefinitionHash: "asmdef",
            CombinedHash: combinedHash);
    }

    private static ReadIndexInputHashSnapshot CreateSnapshot (
        string assetSearchHash,
        string guidPathHash,
        string combinedHash)
    {
        return new ReadIndexInputHashSnapshot(
            ScriptAssembliesHash: "script",
            PackagesManifestHash: "manifest",
            PackagesLockHash: "lock",
            AssemblyDefinitionHash: "asmdef",
            AssetsContentHash: "assets",
            AssetSearchHash: assetSearchHash,
            GuidPathHash: guidPathHash,
            CombinedHash: combinedHash);
    }

}
