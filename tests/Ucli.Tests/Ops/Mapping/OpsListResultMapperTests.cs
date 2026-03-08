using MackySoft.Ucli.Ops.Access;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.Ops.Mapping;

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
                        Name: "ucli.scene.save",
                        Kind: "mutation",
                        Policy: "advanced",
                        ArgsSchemaJson: """{"type":"object"}"""),
                    new MackySoft.Ucli.Contracts.Index.IndexOpEntryJsonContract(
                        Name: "ucli.go.describe",
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
        Assert.Equal("ucli.go.describe", result.Output!.Operations[0].Name);
        Assert.Equal("ucli.scene.save", result.Output.Operations[1].Name);
        Assert.Equal("index", result.Output.ReadIndex.Source);
        Assert.Equal("fresh", result.Output.ReadIndex.Freshness);
    }
}
