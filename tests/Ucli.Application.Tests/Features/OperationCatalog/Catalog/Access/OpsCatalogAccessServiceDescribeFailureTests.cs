using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Ops.Access.OpsCatalogAccessServiceTestSupport;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceDescribeFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenPersistedReadReturnsInvalidArgument_ReturnsFailureWithoutSourceFallback ()
    {
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.InvalidArgument,
                    UcliCoreErrorCodes.InvalidArgument,
                    "invalid project fingerprint")),
        };
        var sourceRefreshService = new UnexpectedOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.AllowStale),
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenOperationMissingFromDescriptor_ReturnsInvalidArgumentWithoutSourceFallback ()
    {
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry()]),
        };
        var sourceRefreshService = new UnexpectedOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.RequireFresh),
            UcliPrimitiveOperationNames.SceneSave,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }
}
