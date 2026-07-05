using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Ops.Access.OpsCatalogAccessServiceTestSupport;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceDescribeIndexTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenIndexHit_ReadsOnlyRequestedDetail ()
    {
        var sceneSave = CreateSceneSaveEntry();
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry(), sceneSave]),
            DescribeResult = PersistedOpsDescribeReadResult.Success(sceneSave),
        };
        var sourceRefreshService = new UnexpectedOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.RequireFresh);

        var result = await service.ReadDescribeAsync(
            context,
            UcliPrimitiveOperationNames.SceneSave,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(sceneSave, result.Output!.Operation);
        OpsCatalogAccessInvocationAssert.PersistedDescribeReadFor(
            persistedReader,
            context,
            UcliPrimitiveOperationNames.SceneSave,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenIndexDetailHasDangerousOperation_ReturnsDetailAsProvided ()
    {
        var operation = CreateCsEvalEntry(UcliPrimitiveOperationNames.GoDescribe);
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry()]),
            DescribeResult = PersistedOpsDescribeReadResult.Success(operation),
        };
        var sourceRefreshService = new UnexpectedOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.RequireFresh),
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(operation, result.Output!.Operation);
    }
}
