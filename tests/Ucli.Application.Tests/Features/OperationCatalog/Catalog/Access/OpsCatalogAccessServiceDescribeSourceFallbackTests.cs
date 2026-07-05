using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Ops.Access.OpsCatalogAccessServiceTestSupport;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceDescribeSourceFallbackTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenDetailArtifactIsBroken_FallsBackToSource ()
    {
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry()]),
            DescribeResult = PersistedOpsDescribeReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Malformed,
                    ReadIndexErrorCodes.ReadIndexFormatInvalid,
                    "Index contract file 'catalogs/ops.describe/<opKey>.json' is malformed.")),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "detail broken"),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.RequireFresh);

        var result = await service.ReadDescribeAsync(
            context,
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflightWithReasonContaining(
            sourceRefreshService,
            context,
            "ops.describe",
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenReadIndexDisabled_UsesSourceRefreshWithDisabledFallbackReason ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "readIndex disabled by mode."),
        };
        var service = new OpsCatalogAccessService(new UnexpectedPersistedOpsCatalogReader(), sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.Disabled);

        var result = await service.ReadDescribeAsync(
            context,
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflight(
            sourceRefreshService,
            context,
            "readIndex disabled by mode.",
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Stale,
                [CreateGoDescribeEntry()]),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "Existing ops index freshness is 'stale'."),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.RequireFresh);

        var result = await service.ReadDescribeAsync(
            context,
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflight(
            sourceRefreshService,
            context,
            "Existing ops index freshness is 'stale'.",
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenSourceRefreshFails_ReturnsSourceFailure ()
    {
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Unavailable,
                    ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                    "Index contract file was not found: ops.catalog.json.")),
        };
        var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
        {
            Result = OpsCatalogSourceRefreshResult.Failure("source refresh failed", UcliCoreErrorCodes.InternalError),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.RequireFresh);

        var result = await service.ReadDescribeAsync(
            context,
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflight(
            sourceRefreshService,
            context,
            "Index contract file was not found: ops.catalog.json.",
            CancellationToken.None);
    }
}
