using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using static MackySoft.Ucli.Application.Tests.Helpers.OperationCatalog.OperationCatalogTestFixtures;

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

        Assert.True(result.IsSuccess);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, result.Output!.Operations[0].Name);
        Assert.Equal("Returns a GameObject description including components and child hierarchy.", result.Output.Operations[0].Description);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, result.Output.Operations[1].Name);
        Assert.Equal("Saves a Unity scene asset.", result.Output.Operations[1].Description);
        Assert.Equal(ReadIndexInfoSource.Index, result.Output.ReadIndex.Source);
        Assert.Equal(MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh, result.Output.ReadIndex.Freshness);
    }
}
