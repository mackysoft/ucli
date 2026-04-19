using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog;
using MackySoft.Ucli.Features.OperationCatalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Mapping;

namespace MackySoft.Ucli.Tests.Ops.Mapping;

public sealed class OpsDescribeResultMapperTests
{
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