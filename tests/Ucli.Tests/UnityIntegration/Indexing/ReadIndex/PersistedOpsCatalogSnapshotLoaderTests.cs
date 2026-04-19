using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.UnityIntegration.Project;

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
            new StubIndexCatalogReader(IndexAccessResult<IndexOpsCatalogJsonContract>.Failure(error)),
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
            new StubIndexCatalogReader(IndexAccessResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog())),
            freshnessEvaluator);

        var result = await loader.Load(CreateUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WhenDependenciesSucceed_ReturnsSnapshotAndUsesAllowStale ()
    {
        var freshnessEvaluator = new StubIndexFreshnessEvaluator(
            IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable));
        var loader = new PersistedOpsCatalogSnapshotLoader(
            new StubIndexCatalogReader(IndexAccessResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog())),
            freshnessEvaluator);

        var result = await loader.Load(CreateUnityProject(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(IndexFreshness.Probable, result.Snapshot!.Freshness);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"), result.Snapshot.GeneratedAtUtc);
        Assert.Single(result.Snapshot.Entries);
        Assert.Equal(ReadIndexMode.AllowStale, freshnessEvaluator.LastMode);
        Assert.Equal("/repo/UnityProject", freshnessEvaluator.LastProjectRoot);
        Assert.Equal(IndexFreshnessTarget.OpsCatalog, freshnessEvaluator.LastTarget);
        Assert.Equal("source-hash", freshnessEvaluator.LastPersistedSourceInputsHash);
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

    private sealed class StubIndexCatalogReader : IIndexCatalogReader
    {
        private readonly IndexAccessResult<IndexOpsCatalogJsonContract> opsCatalogResult;

        public StubIndexCatalogReader (IndexAccessResult<IndexOpsCatalogJsonContract> opsCatalogResult)
        {
            this.opsCatalogResult = opsCatalogResult ?? throw new ArgumentNullException(nameof(opsCatalogResult));
        }

        public ValueTask<IndexAccessResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(opsCatalogResult);
        }

        public ValueTask<IndexAccessResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexAccessResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexAccessResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexAccessResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexAccessResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
            string storageRoot,
            string projectFingerprint,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexAccessResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubIndexFreshnessEvaluator : IIndexFreshnessEvaluator
    {
        private readonly IndexFreshnessEvaluationResult result;

        public StubIndexFreshnessEvaluator (IndexFreshnessEvaluationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public string? LastProjectRoot { get; private set; }

        public IndexFreshnessTarget LastTarget { get; private set; }

        public string? LastPersistedSourceInputsHash { get; private set; }

        public ReadIndexMode LastMode { get; private set; }

        public ValueTask<IndexFreshnessEvaluationResult> Evaluate (
            string projectRoot,
            IndexFreshnessTarget target,
            string? persistedSourceInputsHash,
            ReadIndexMode mode,
            CancellationToken cancellationToken = default)
        {
            LastProjectRoot = projectRoot;
            LastTarget = target;
            LastPersistedSourceInputsHash = persistedSourceInputsHash;
            LastMode = mode;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}