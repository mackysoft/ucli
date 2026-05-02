using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops.Projection;

namespace MackySoft.Ucli.Tests.Ops.Mapping;

public sealed class OpsDescribeResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenResultSchemaIsPresent_ReturnsArgsAndResultSchemas ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            new OpsCatalogReadOutput(
                Operations:
                [
                    new MackySoft.Ucli.Contracts.Index.IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: """{"type":"object"}""",
                        ResultSchemaJson: """{"type":"object","properties":{"globalObjectId":{"type":"string"}}}"""),
                ],
                AccessInfo: new OpsCatalogAccessInfo(
                    true,
                    true,
                    OpsCatalogSource.Index,
                    MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);

        Assert.True(result.IsSuccess);
        Assert.Equal("object", result.Output!.Operation.ArgsSchema.GetProperty("type").GetString());
        Assert.Equal("object", result.Output.Operation.ResultSchema!.Value.GetProperty("type").GetString());
        Assert.True(result.Output.Operation.ResultSchema.Value.GetProperty("properties").TryGetProperty("globalObjectId", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenOperationNameIsEmpty_ReturnsInvalidArgument ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            new OpsCatalogReadOutput(
                Operations: Array.Empty<MackySoft.Ucli.Contracts.Index.IndexOpEntryJsonContract>(),
                AccessInfo: new OpsCatalogAccessInfo(
                    true,
                    true,
                    OpsCatalogSource.Index,
                    MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenArgsSchemaIsInvalid_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            new OpsCatalogReadOutput(
                Operations:
                [
                    new MackySoft.Ucli.Contracts.Index.IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: "\"not-an-object\""),
                ],
                AccessInfo: new OpsCatalogAccessInfo(
                    true,
                    true,
                    OpsCatalogSource.Index,
                    MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }
}
