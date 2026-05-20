using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using static MackySoft.Ucli.Tests.Features.OperationCatalog.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Tests.Features.OperationCatalog.Catalog.Source;

public sealed class OpsCatalogSourceRefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_PersistsOpsCatalog_WhenCoreAndFullSnapshotsAreAvailable ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var reader = new StubOpsCatalogReader
        {
            Result = CreateFetchResult(generatedAtUtc, [CreateGoDescribeEntry()]),
        };
        var fingerprintProvider = new StubReadIndexInputFingerprintProvider
        {
            CoreSnapshot = CreateCoreSnapshot("combined"),
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined"),
        };
        var artifactWriter = new StubReadIndexArtifactWriter();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        Assert.Equal(1, reader.CallCount);
        Assert.True(reader.LastRequireReadinessGate);
        Assert.False(reader.LastIncludeEditLoweringOnly);
        Assert.Equal(2, fingerprintProvider.CoreCallCount);
        Assert.Equal(1, fingerprintProvider.FullCallCount);
        Assert.Equal(1, artifactWriter.OpsCatalogCallCount);
        Assert.Equal("combined", artifactWriter.LastSourceInputsHash);
        Assert.NotNull(artifactWriter.LastManifestInputSnapshot);
        Assert.Equal("asset-search", artifactWriter.LastManifestInputSnapshot!.AssetSearchHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReusesPersistedManifestAssetHashesWithoutFullFingerprint ()
    {
        var reader = new StubOpsCatalogReader
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
        var fingerprintProvider = new StubReadIndexInputFingerprintProvider
        {
            CoreSnapshot = CreateCoreSnapshot("new-combined"),
            ThrowOnTryCompute = true,
        };
        var artifactWriter = new StubReadIndexArtifactWriter();
        var service = new OpsCatalogSourceRefreshService(reader, persistedArtifactsReader, fingerprintProvider, artifactWriter);

        var result = await service.RefreshAsync(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fingerprintProvider.CoreCallCount);
        Assert.Equal(0, fingerprintProvider.FullCallCount);
        Assert.Equal("new-combined", artifactWriter.LastSourceInputsHash);
        Assert.NotNull(artifactWriter.LastManifestInputSnapshot);
        Assert.Equal("existing-assets", artifactWriter.LastManifestInputSnapshot!.AssetsContentHash);
        Assert.Equal("existing-asset-search", artifactWriter.LastManifestInputSnapshot.AssetSearchHash);
        Assert.Equal("existing-guid-path", artifactWriter.LastManifestInputSnapshot.GuidPathHash);
        Assert.Equal("new-combined", artifactWriter.LastManifestInputSnapshot.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsSourceResultWithPersistenceFailureReason ()
    {
        var reader = new StubOpsCatalogReader
        {
            Result = CreateFetchResult(
                DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                [CreateGoDescribeEntry()]),
        };
        var artifactWriter = new StubReadIndexArtifactWriter
        {
            WriteException = new InvalidOperationException("disk full"),
        };
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            new StubReadIndexInputFingerprintProvider
            {
                CoreSnapshot = CreateCoreSnapshot("combined"),
                Snapshot = CreateSnapshot("asset-search", "guid-path", "combined"),
            },
            artifactWriter);

        var result = await service.RefreshAsync(
            CreateProjectContext(),
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
        var reader = new StubOpsCatalogReader
        {
            Result = CreateFetchResult(
                DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                [CreateGoDescribeEntry()]),
        };
        var fingerprintProvider = new StubReadIndexInputFingerprintProvider
        {
            CoreSnapshot = null,
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined"),
        };
        var artifactWriter = new StubReadIndexArtifactWriter();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("readIndex stale.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("input fingerprint could not be computed", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(1, fingerprintProvider.CoreCallCount);
        Assert.Equal(0, fingerprintProvider.FullCallCount);
        Assert.Equal(0, artifactWriter.OpsCatalogCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsFirstSourceResultWithRetryFailureReason_WhenRetryCatalogReadFails ()
    {
        var reader = new StubOpsCatalogReader();
        reader.Enqueue(CreateFetchResult(
            DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
            [CreateGoDescribeEntry()]));
        reader.Enqueue(OpsCatalogFetchResult.Failure("Unity source unavailable.", UcliCoreErrorCodes.InternalError));
        var fingerprintProvider = new StubReadIndexInputFingerprintProvider
        {
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined-2"),
        };
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-1"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        var artifactWriter = new StubReadIndexArtifactWriter();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Snapshot!.Operations);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, result.Snapshot.Operations[0].Name);
        Assert.Contains("readIndex stale.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the catalog was being read", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("retry catalog read failed. Unity source unavailable.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(3, fingerprintProvider.CoreCallCount);
        Assert.Equal(0, artifactWriter.OpsCatalogCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_RetriesAndPersists_WhenCoreInputsChangeDuringFirstCatalogRead ()
    {
        var reader = new StubOpsCatalogReader();
        reader.Enqueue(CreateFetchResult(
            DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
            [CreateGoDescribeEntry()]));
        reader.Enqueue(CreateFetchResult(
            DateTimeOffset.Parse("2026-03-07T00:01:00+00:00"),
            [CreateSceneSaveEntry()]));
        var fingerprintProvider = new StubReadIndexInputFingerprintProvider
        {
            Snapshot = CreateSnapshot("asset-search", "guid-path", "combined-2"),
        };
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-1"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        fingerprintProvider.EnqueueCore(CreateCoreSnapshot("combined-2"));
        var artifactWriter = new StubReadIndexArtifactWriter();
        var service = new OpsCatalogSourceRefreshService(
            reader,
            new StubPersistedOpsCatalogPersistenceArtifactsReader(),
            fingerprintProvider,
            artifactWriter);

        var result = await service.RefreshAsync(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            "readIndex stale.",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Snapshot!.Operations);
        Assert.Equal(UcliPrimitiveOperationNames.SceneSave, result.Snapshot.Operations[0].Name);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(4, fingerprintProvider.CoreCallCount);
        Assert.Equal(1, artifactWriter.OpsCatalogCallCount);
        Assert.Equal("combined-2", artifactWriter.LastSourceInputsHash);
    }

    private static ResolvedUnityProjectContext CreateProjectContext ()
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

    private sealed class StubOpsCatalogReader : IOpsCatalogReader
    {
        private readonly Queue<OpsCatalogFetchResult> results = new();

        public int CallCount { get; private set; }

        public bool LastRequireReadinessGate { get; private set; }

        public bool LastIncludeEditLoweringOnly { get; private set; }

        public OpsCatalogFetchResult Result { get; set; }
            = OpsCatalogFetchResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public void Enqueue (OpsCatalogFetchResult result)
        {
            results.Enqueue(result);
        }

        public ValueTask<OpsCatalogFetchResult> ReadAsync (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UnityExecutionMode mode,
            TimeSpan timeout,
            bool failFast,
            bool requireReadinessGate,
            bool includeEditLoweringOnly = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequireReadinessGate = requireReadinessGate;
            LastIncludeEditLoweringOnly = includeEditLoweringOnly;
            if (results.TryDequeue(out var result))
            {
                return ValueTask.FromResult(result);
            }

            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubPersistedOpsCatalogPersistenceArtifactsReader : IPersistedOpsCatalogPersistenceArtifactsReader
    {
        public PersistedOpsCatalogPersistenceArtifacts Result { get; set; }
            = new(InputsManifest: null, HasPersistedAssetLookupArtifacts: false);

        public ValueTask<PersistedOpsCatalogPersistenceArtifacts> ReadAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubReadIndexInputFingerprintProvider : IReadIndexInputFingerprintProvider
    {
        private readonly Queue<ReadIndexCoreInputHashSnapshot?> coreSnapshots = new();

        public int CoreCallCount { get; private set; }

        public int FullCallCount { get; private set; }

        public ReadIndexCoreInputHashSnapshot? CoreSnapshot { get; set; }

        public ReadIndexInputHashSnapshot? Snapshot { get; set; }

        public bool ThrowOnTryCompute { get; set; }

        public void EnqueueCore (ReadIndexCoreInputHashSnapshot? snapshot)
        {
            coreSnapshots.Enqueue(snapshot);
        }

        public ValueTask<ReadIndexCoreInputHashSnapshot?> TryComputeCoreAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CoreCallCount++;
            if (coreSnapshots.TryDequeue(out var snapshot))
            {
                return ValueTask.FromResult(snapshot);
            }

            return ValueTask.FromResult(CoreSnapshot);
        }

        public ValueTask<ReadIndexInputHashSnapshot?> TryComputeAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FullCallCount++;
            if (ThrowOnTryCompute)
            {
                throw new InvalidOperationException("full snapshot should not be computed");
            }

            return ValueTask.FromResult(Snapshot);
        }
    }

    private sealed class StubReadIndexArtifactWriter : IReadIndexArtifactWriter
    {
        public int OpsCatalogCallCount { get; private set; }

        public string? LastSourceInputsHash { get; private set; }

        public ReadIndexInputHashSnapshot? LastManifestInputSnapshot { get; private set; }

        public Exception? WriteException { get; set; }

        public ValueTask WriteOpsCatalogAsync (
            string storageRoot,
            string projectFingerprint,
            DateTimeOffset generatedAtUtc,
            IReadOnlyList<IndexOpEntryJsonContract> operations,
            string sourceInputsHash,
            ReadIndexInputHashSnapshot? manifestInputSnapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpsCatalogCallCount++;
            LastSourceInputsHash = sourceInputsHash;
            LastManifestInputSnapshot = manifestInputSnapshot;
            if (WriteException != null)
            {
                throw WriteException;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask WriteAssetLookupsAsync (
            string storageRoot,
            string projectFingerprint,
            DateTimeOffset generatedAtUtc,
            IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
            IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
            ReadIndexInputHashSnapshot inputSnapshot,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask WriteSceneTreeLiteAsync (
            string storageRoot,
            string projectFingerprint,
            DateTimeOffset generatedAtUtc,
            string scenePath,
            IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
            string sourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
