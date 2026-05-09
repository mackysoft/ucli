using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Helpers.OperationCatalog.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Source;

public sealed class PersistedOpsCatalogReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenOpsCatalogReadFails_ReturnsFailure ()
    {
        var error = new IndexServiceError(
            ReadIndexErrorCodes.ReadIndexBootstrapFailed,
            "Index contract file was not found: ops.catalog.json.");
        var reader = new PersistedOpsCatalogReader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Failure(error)),
            new StubIndexFreshnessEvaluator(IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh)));

        var result = await reader.ReadAsync(CreateUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.Unavailable, result.ReadFailure!.Kind);
        Assert.Equal(error.Code, result.ReadFailure.ErrorCode);
        Assert.Equal(error.Message, result.ReadFailure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenOpsCatalogPathInputIsInvalid_ReturnsInvalidArgumentFailure ()
    {
        var error = new IndexServiceError(
            UcliCoreErrorCodes.InvalidArgument,
            "Project fingerprint must not be empty.");
        var reader = new PersistedOpsCatalogReader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Failure(error)),
            new StubIndexFreshnessEvaluator(IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh)));

        var result = await reader.ReadAsync(CreateUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.InvalidArgument, result.ReadFailure!.Kind);
        Assert.Equal(error.Code, result.ReadFailure.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenOpsCatalogContractIsMalformed_ReturnsMalformedFailure ()
    {
        var error = new IndexServiceError(
            ReadIndexErrorCodes.ReadIndexFormatInvalid,
            "Index contract file 'ops.catalog.json' is malformed.");
        var reader = new PersistedOpsCatalogReader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Failure(error)),
            new StubIndexFreshnessEvaluator(IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh)));

        var result = await reader.ReadAsync(CreateUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.Malformed, result.ReadFailure!.Kind);
        Assert.Equal(error.Code, result.ReadFailure.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenLoadedOpsCatalogEntriesAreInvalid_ReturnsMalformedFailureWithoutObservingFreshness ()
    {
        var freshnessEvaluator = new StubIndexFreshnessEvaluator(
            IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh));
        var reader = new PersistedOpsCatalogReader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(
                CreateCatalog(new IndexOpEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: "\"not-an-object\"")))),
            freshnessEvaluator);

        var result = await reader.ReadAsync(CreateUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.Malformed, result.ReadFailure!.Kind);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.ReadFailure.ErrorCode);
        Assert.Contains("ops.catalog.json", result.ReadFailure.Message, StringComparison.Ordinal);
        Assert.Equal(0, freshnessEvaluator.ObserveCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFreshnessObservationFails_ReturnsFailure ()
    {
        var error = new IndexServiceError(
            ReadIndexErrorCodes.ReadIndexFreshRequired,
            "readIndexMode=requireFresh requires index freshness 'fresh'.");
        var freshnessEvaluator = new StubIndexFreshnessEvaluator(
            IndexFreshnessEvaluationResult.Failure(IndexFreshness.Stale, error));
        var reader = new PersistedOpsCatalogReader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog())),
            freshnessEvaluator);

        var result = await reader.ReadAsync(CreateUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.FreshnessUnavailable, result.ReadFailure!.Kind);
        Assert.Equal(error.Code, result.ReadFailure.ErrorCode);
        Assert.Equal(error.Message, result.ReadFailure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDependenciesSucceed_ReturnsCatalogAndObservesFreshness ()
    {
        var freshnessEvaluator = new StubIndexFreshnessEvaluator(
            IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable));
        var reader = new PersistedOpsCatalogReader(
            new StubReadIndexArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog())),
            freshnessEvaluator);
        var unityProject = CreateUnityProject();

        var result = await reader.ReadAsync(unityProject, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"), result.Snapshot!.GeneratedAtUtc);
        Assert.Single(result.Snapshot.Operations);
        Assert.Same(unityProject, freshnessEvaluator.LastUnityProject);
        Assert.Equal(IndexFreshnessTarget.OpsCatalog, freshnessEvaluator.LastTarget);
        Assert.Equal("source-hash", freshnessEvaluator.LastPersistedSourceInputsHash);
        Assert.Equal(1, freshnessEvaluator.ObserveCallCount);
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
        return CreateCatalog(CreateGoDescribeEntry());
    }

    private static IndexOpsCatalogJsonContract CreateCatalog (IndexOpEntryJsonContract entry)
    {
        return new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                entry,
            ]);
    }

    private sealed class StubReadIndexArtifactReader : IReadIndexArtifactReader
    {
        private readonly ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract> opsCatalogResult;

        public StubReadIndexArtifactReader (ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract> opsCatalogResult)
        {
            this.opsCatalogResult = opsCatalogResult ?? throw new ArgumentNullException(nameof(opsCatalogResult));
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(opsCatalogResult);
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookupAsync (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifestAsync (
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

        public ValueTask<IndexFreshnessEvaluationResult> ObserveAsync (
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

        public ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLiteAsync (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            string? persistedSourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
