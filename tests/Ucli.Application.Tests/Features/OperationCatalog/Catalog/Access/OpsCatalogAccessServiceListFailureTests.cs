using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.Ops.Access.OpsCatalogAccessServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceListFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenPersistedReadReturnsInvalidArgument_ReturnsFailureWithoutSourceFallback ()
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

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.AllowStale), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }
}
