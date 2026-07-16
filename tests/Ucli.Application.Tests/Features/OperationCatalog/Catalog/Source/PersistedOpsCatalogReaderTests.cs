using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

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
            CreateArtifactReader(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Failure(error)),
            new RecordingReadIndexFreshnessEvaluator());

        var result = await reader.ReadAsync(ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(), CancellationToken.None);

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
            CreateArtifactReader(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Failure(error)),
            new RecordingReadIndexFreshnessEvaluator());

        var result = await reader.ReadAsync(ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(), CancellationToken.None);

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
            CreateArtifactReader(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Failure(error)),
            new RecordingReadIndexFreshnessEvaluator());

        var result = await reader.ReadAsync(ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.Malformed, result.ReadFailure!.Kind);
        Assert.Equal(error.Code, result.ReadFailure.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFreshnessObservationFails_ReturnsFailure ()
    {
        var error = new IndexServiceError(
            ReadIndexErrorCodes.ReadIndexFreshRequired,
            "readIndexMode=requireFresh requires index freshness 'fresh'.");
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Failure(IndexFreshness.Stale, error),
        };
        var reader = new PersistedOpsCatalogReader(
            CreateArtifactReader(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(CreateCatalogSnapshot(
                "source-hash",
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                "describe-generation"))),
            freshnessEvaluator);

        var result = await reader.ReadAsync(ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.FreshnessUnavailable, result.ReadFailure!.Kind);
        Assert.Equal(error.Code, result.ReadFailure.ErrorCode);
        Assert.Equal(error.Message, result.ReadFailure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDependenciesSucceed_ReturnsCatalogAndObservesFreshness ()
    {
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var reader = new PersistedOpsCatalogReader(
            CreateArtifactReader(
                ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(CreateCatalogSnapshot(
                    "source-hash",
                    DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    "describe-generation")),
                ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Success(CreateDescribeSnapshot("source-hash"))),
            freshnessEvaluator);
        var unityProject = ProjectContextTestFactory.CreateRepositoryFixtureUnityProject();

        var result = await reader.ReadAsync(unityProject, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"), result.Snapshot!.GeneratedAtUtc);
        Assert.Single(result.Snapshot.Operations);
        ReadIndexFreshnessInvocationAssert.FreshnessObservedOnce(
            freshnessEvaluator,
            unityProject,
            IndexFreshnessTarget.OpsCatalog,
            Sha256DigestTestFactory.Compute("source-hash"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDescribeDisappearsDuringCatalogPublication_RetriesChangedCatalogOnce ()
    {
        var oldSourceHash = Sha256DigestTestFactory.Compute("old-source-hash");
        var newSourceHash = Sha256DigestTestFactory.Compute("new-source-hash");
        var artifactReader = new RecordingReadIndexArtifactReader();
        artifactReader.OpsCatalogResults.Enqueue(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(
            CreateCatalogSnapshot(
                "old-source-hash",
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                "old-describe-generation")));
        artifactReader.OpsCatalogResults.Enqueue(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(
            CreateCatalogSnapshot(
                "new-source-hash",
                DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                "new-describe-generation")));
        artifactReader.OpsDescribeResults.Enqueue(ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
            new IndexServiceError(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                "The describe artifact from the previous catalog generation no longer exists.")));
        artifactReader.OpsDescribeResults.Enqueue(ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Success(
            CreateDescribeSnapshot("new-source-hash")));
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator();
        var reader = new PersistedOpsCatalogReader(artifactReader, freshnessEvaluator);

        var result = await reader.ReadAsync(
            ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"), result.Snapshot!.GeneratedAtUtc);
        Assert.Equal(
            [
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsCatalog,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsDescribe,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsCatalog,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsDescribe,
            ],
            artifactReader.ReadInvocations.Select(static invocation => invocation.Kind));
        Assert.Collection(
            freshnessEvaluator.ObserveInvocations,
            invocation => Assert.Equal(oldSourceHash, invocation.PersistedSourceInputsHash),
            invocation => Assert.Equal(newSourceHash, invocation.PersistedSourceInputsHash));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenSameInputsReferenceNewDescribeGeneration_RetriesOnce ()
    {
        var sourceHash = Sha256DigestTestFactory.Compute("source-hash");
        var artifactReader = new RecordingReadIndexArtifactReader();
        artifactReader.OpsCatalogResults.Enqueue(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(
            CreateCatalogSnapshot(
                "source-hash",
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                "old-describe-generation")));
        artifactReader.OpsCatalogResults.Enqueue(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(
            CreateCatalogSnapshot(
                "source-hash",
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                "new-describe-generation")));
        artifactReader.OpsDescribeResults.Enqueue(ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
            new IndexServiceError(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                "The describe artifact from the previous catalog generation no longer exists.")));
        artifactReader.OpsDescribeResults.Enqueue(ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Success(
            CreateDescribeSnapshot("source-hash")));
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator();
        var reader = new PersistedOpsCatalogReader(artifactReader, freshnessEvaluator);

        var result = await reader.ReadAsync(
            ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"), result.Snapshot!.GeneratedAtUtc);
        Assert.Equal(
            [
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsCatalog,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsDescribe,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsCatalog,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsDescribe,
            ],
            artifactReader.ReadInvocations.Select(static invocation => invocation.Kind));
        Assert.Collection(
            freshnessEvaluator.ObserveInvocations,
            invocation => Assert.Equal(sourceHash, invocation.PersistedSourceInputsHash),
            invocation => Assert.Equal(sourceHash, invocation.PersistedSourceInputsHash));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDescribeFailsAndCatalogGenerationIsUnchanged_ReturnsOriginalFailureWithoutRetryingDescribe ()
    {
        var sourceHash = Sha256DigestTestFactory.Compute("source-hash");
        var describeError = new IndexServiceError(
            ReadIndexErrorCodes.ReadIndexBootstrapFailed,
            "The describe artifact is unavailable.");
        var artifactReader = new RecordingReadIndexArtifactReader();
        artifactReader.OpsCatalogResults.Enqueue(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(
            CreateCatalogSnapshot(
                "source-hash",
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                "describe-generation")));
        artifactReader.OpsCatalogResults.Enqueue(ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Success(
            CreateCatalogSnapshot(
                "source-hash",
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                "describe-generation")));
        artifactReader.OpsDescribeResults.Enqueue(ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(describeError));
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator();
        var reader = new PersistedOpsCatalogReader(artifactReader, freshnessEvaluator);

        var result = await reader.ReadAsync(
            ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(describeError.Code, result.ReadFailure!.ErrorCode);
        Assert.Equal(describeError.Message, result.ReadFailure.Message);
        Assert.Equal(
            [
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsCatalog,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsDescribe,
                RecordingReadIndexArtifactReader.ReadIndexArtifactKind.OpsCatalog,
            ],
            artifactReader.ReadInvocations.Select(static invocation => invocation.Kind));
        Assert.Collection(
            freshnessEvaluator.ObserveInvocations,
            invocation => Assert.Equal(sourceHash, invocation.PersistedSourceInputsHash),
            invocation => Assert.Equal(sourceHash, invocation.PersistedSourceInputsHash));
    }

    private static RecordingReadIndexArtifactReader CreateArtifactReader (
        ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot> opsCatalogResult,
        ReadIndexArtifactReadResult<OpsDescribeSnapshot>? opsDescribeResult = null)
    {
        return new RecordingReadIndexArtifactReader
        {
            OpsCatalogResult = opsCatalogResult,
            OpsDescribeResult = opsDescribeResult
                ?? ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Success(CreateDescribeSnapshot("source-hash")),
        };
    }

    private static IndexOpsCatalogJsonContract CreateCatalog (
        string sourceHashSeed,
        DateTimeOffset generatedAtUtc,
        string describeGenerationSeed)
    {
        var describeIdentity = Sha256DigestTestFactory.Compute(describeGenerationSeed).ToString();
        return new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: Sha256DigestTestFactory.Compute(sourceHashSeed).ToString(),
            Entries:
            [
                new IndexOpsCatalogEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    Description: "Returns a GameObject description.",
                    DescribeKey: describeIdentity,
                    DescribeHash: describeIdentity),
            ]);
    }

    private static OpsCatalogDescriptorSnapshot CreateCatalogSnapshot (
        string sourceHashSeed,
        DateTimeOffset generatedAtUtc,
        string describeGenerationSeed)
    {
        if (!OpsCatalogDescriptorSnapshot.TryCreate(
                CreateCatalog(sourceHashSeed, generatedAtUtc, describeGenerationSeed),
                out var snapshot))
        {
            throw new InvalidOperationException("Persisted ops-catalog fixture is invalid.");
        }

        return snapshot;
    }

    private static IndexOpsDescribeJsonContract CreateDescribe (string sourceHashSeed)
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute(sourceHashSeed).ToString(),
            Operation: CreateGoDescribeEntry());
    }

    private static OpsDescribeSnapshot CreateDescribeSnapshot (string sourceHashSeed)
    {
        if (!OpsDescribeSnapshot.TryCreate(CreateDescribe(sourceHashSeed), out var snapshot))
        {
            throw new InvalidOperationException("Persisted ops-describe fixture is invalid.");
        }

        return snapshot;
    }

}
