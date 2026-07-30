using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Mapping;

public sealed class OpsListResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_SortsOperationsByName ()
    {
        var mapper = new OpsListResultMapper(new OpsReadIndexInfoMapper());
        var output = new OpsListReadOutput(
            Snapshot: OpsCatalogListSnapshotFactory.FromCatalog(CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    CreateSceneSaveEntry(),
                    CreateGoDescribeEntry(),
                ])),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));

        var result = mapper.Map(
            output,
            output.Snapshot.Operations);

        var succeeded = Assert.IsType<OpsListServiceResult.Succeeded>(result);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, succeeded.Output.Operations[0].Name);
        Assert.Equal("Returns a GameObject description including components and child hierarchy.", succeeded.Output.Operations[0].Description);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, succeeded.Output.Operations[1].Name);
        Assert.Equal("Saves a Unity scene asset.", succeeded.Output.Operations[1].Description);
        Assert.Equal(ReadIndexInfoSource.Index, succeeded.Output.ReadIndex.Source);
        Assert.Equal(MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh, succeeded.Output.ReadIndex.Freshness);
    }
}
