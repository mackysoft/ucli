using MackySoft.Ucli.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops.Projection;

namespace MackySoft.Ucli.Tests.Ops.Mapping;

public sealed class OpsListResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_SortsOperationsByName ()
    {
        var mapper = new OpsListResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            new OpsCatalogReadOutput(
                Operations:
                [
                    new MackySoft.Ucli.Contracts.Index.IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                        Kind: "mutation",
                        Policy: "advanced",
                        ArgsSchemaJson: """{"type":"object"}"""),
                    new MackySoft.Ucli.Contracts.Index.IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: """{"type":"object"}"""),
                ],
                AccessInfo: new OpsCatalogAccessInfo(
                    true,
                    true,
                    OpsCatalogSource.Index,
                    MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)));

        Assert.True(result.IsSuccess);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, result.Output!.Operations[0].Name);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, result.Output.Operations[1].Name);
        Assert.Equal("index", result.Output.ReadIndex.Source);
        Assert.Equal("fresh", result.Output.ReadIndex.Freshness);
    }
}