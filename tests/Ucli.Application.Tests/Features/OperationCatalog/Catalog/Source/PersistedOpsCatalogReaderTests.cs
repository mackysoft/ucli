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
            CreateArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Failure(error)),
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
            CreateArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Failure(error)),
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
            CreateArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Failure(error)),
            new RecordingReadIndexFreshnessEvaluator());

        var result = await reader.ReadAsync(ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.Malformed, result.ReadFailure!.Kind);
        Assert.Equal(error.Code, result.ReadFailure.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenLoadedOpsCatalogEntriesAreInvalid_ReturnsMalformedFailureWithoutObservingFreshness ()
    {
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator();
        var reader = new PersistedOpsCatalogReader(
            CreateArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(
                CreateCatalog(new IndexOpsCatalogEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    Description: "Returns a GameObject description.",
                    DescribeKey: new string('a', 64),
                    DescribeHash: string.Empty)))),
            freshnessEvaluator);

        var result = await reader.ReadAsync(ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(), CancellationToken.None);

        PersistedOpsCatalogReaderAssert.MalformedCatalogReturnedBeforeFreshnessObservation(
            result,
            freshnessEvaluator,
            "ops.catalog.json");
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
            CreateArtifactReader(ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog())),
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
                ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>.Success(CreateCatalog()),
                ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>.Success(CreateDescribe())),
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
            "source-hash");
    }

    private static RecordingReadIndexArtifactReader CreateArtifactReader (
        ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract> opsCatalogResult,
        ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>? opsDescribeResult = null)
    {
        return new RecordingReadIndexArtifactReader
        {
            OpsCatalogResult = opsCatalogResult,
            OpsDescribeResult = opsDescribeResult
                ?? ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>.Success(CreateDescribe()),
        };
    }

    private static IndexOpsCatalogJsonContract CreateCatalog ()
    {
        return CreateCatalog(new IndexOpsCatalogEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: "query",
            Policy: "safe",
            Description: "Returns a GameObject description.",
            DescribeKey: new string('a', 64),
            DescribeHash: new string('b', 64)));
    }

    private static IndexOpsCatalogJsonContract CreateCatalog (IndexOpsCatalogEntryJsonContract entry)
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

    private static IndexOpsDescribeJsonContract CreateDescribe ()
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: CreateGoDescribeEntry());
    }

}
