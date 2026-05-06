using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Tests;

public sealed class PersistedOpsCatalogSnapshotLoaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WhenOpsCatalogReadFails_ReturnsFailure ()
    {
        var error = new IndexServiceError(
            IpcErrorCodes.ReadIndexBootstrapFailed,
            "Index contract file was not found: ops.catalog.json.");
        var loader = new PersistedOpsCatalogSnapshotLoader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Failure(error)),
            new StubIndexFreshnessEvaluator(IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh)));

        var result = await loader.Load(CreateUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WhenFreshnessEvaluationFails_ReturnsFailure ()
    {
        var error = new IndexServiceError(
            IpcErrorCodes.ReadIndexFreshRequired,
            "readIndexMode=requireFresh requires index freshness 'fresh'.");
        var freshnessEvaluator = new StubIndexFreshnessEvaluator(
            IndexFreshnessEvaluationResult.Failure(IndexFreshness.Stale, error));
        var loader = new PersistedOpsCatalogSnapshotLoader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog())),
            freshnessEvaluator);
        var unityProject = CreateUnityProject();

        var result = await loader.Load(unityProject, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WhenDependenciesSucceed_ReturnsSnapshotAndObservesFreshness ()
    {
        var freshnessEvaluator = new StubIndexFreshnessEvaluator(
            IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable));
        var loader = new PersistedOpsCatalogSnapshotLoader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog())),
            freshnessEvaluator);
        var unityProject = CreateUnityProject();

        var result = await loader.Load(unityProject, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(IndexFreshness.Probable, result.Snapshot!.Freshness);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"), result.Snapshot.GeneratedAtUtc);
        Assert.Single(result.Snapshot.Entries);
        Assert.Same(unityProject, freshnessEvaluator.LastUnityProject);
        Assert.Equal(IndexFreshnessTarget.OpsCatalog, freshnessEvaluator.LastTarget);
        Assert.Equal("source-hash", freshnessEvaluator.LastPersistedSourceInputsHash);
        Assert.Equal(1, freshnessEvaluator.ObserveCallCount);
        Assert.Equal(0, freshnessEvaluator.EvaluateCallCount);
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static IndexOpsCatalogJsonContract CreateCatalog ()
    {
        return new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}"""),
            ]);
    }

    private sealed class StubReadIndexArtifactReader : IReadIndexArtifactReader
    {
        private readonly ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract> opsCatalogResult;

        public StubReadIndexArtifactReader (ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract> opsCatalogResult)
        {
            this.opsCatalogResult = opsCatalogResult ?? throw new ArgumentNullException(nameof(opsCatalogResult));
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(opsCatalogResult);
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubIndexFreshnessEvaluator : IReadIndexFreshnessEvaluator
    {
        private readonly IndexFreshnessEvaluationResult result;

        public StubIndexFreshnessEvaluator (IndexFreshnessEvaluationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public IndexFreshnessTarget LastTarget { get; private set; }

        public string? LastPersistedSourceInputsHash { get; private set; }

        public int ObserveCallCount { get; private set; }

        public int EvaluateCallCount { get; private set; }

        public ValueTask<IndexFreshnessEvaluationResult> Evaluate (
            ResolvedUnityProjectContext unityProject,
            IndexFreshnessTarget target,
            string? persistedSourceInputsHash,
            ReadIndexMode mode,
            CancellationToken cancellationToken = default)
        {
            EvaluateCallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public ValueTask<IndexFreshnessEvaluationResult> Observe (
            ResolvedUnityProjectContext unityProject,
            IndexFreshnessTarget target,
            string? persistedSourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            LastUnityProject = unityProject;
            LastTarget = target;
            LastPersistedSourceInputsHash = persistedSourceInputsHash;
            ObserveCallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }

        public ValueTask<IndexFreshnessEvaluationResult> EvaluateSceneTreeLite (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            string? persistedSourceInputsHash,
            ReadIndexMode mode,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLite (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            string? persistedSourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
