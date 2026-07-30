using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Ops.Access.OpsCatalogAccessServiceTestSupport;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceListIndexTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAllowStaleIndexExists_ReturnsPersistedCatalog ()
    {
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Probable,
                [CreateGoDescribeEntry()]),
        };
        var sourceRefreshService = new UnexpectedOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.AllowStale), CancellationToken.None);

        var succeeded = Assert.IsType<OpsListReadResult.Succeeded>(result);
        Assert.Equal(OpsCatalogSource.Index, succeeded.Output.AccessInfo.Source);
        Assert.True(succeeded.Output.AccessInfo.Used);
        Assert.True(succeeded.Output.AccessInfo.Hit);
        Assert.Equal(IndexFreshness.Probable, succeeded.Output.AccessInfo.Freshness);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAllowStaleIndexContainsEditLoweringOnlyPrimitive_FiltersPublicList ()
    {
        var hiddenPrimitiveEntry = CreateSceneSaveEntry() with
        {
            Name = UcliPrimitiveOperationNames.CompEnsure,
        };
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Stale,
                [CreateGoDescribeEntry(), hiddenPrimitiveEntry]),
        };
        var sourceRefreshService = new UnexpectedOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.AllowStale), CancellationToken.None);

        var succeeded = Assert.IsType<OpsListReadResult.Succeeded>(result);
        Assert.Equal(
            [UcliPrimitiveOperationNames.GoDescribe],
            succeeded.Output.Snapshot.Operations.Select(static operation => operation.Name));
        Assert.Equal(OpsCatalogSource.Index, succeeded.Output.AccessInfo.Source);
    }
}
