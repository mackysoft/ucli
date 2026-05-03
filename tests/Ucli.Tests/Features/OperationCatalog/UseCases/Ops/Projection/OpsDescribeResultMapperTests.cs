using MackySoft.Ucli.Contracts.Index;
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
                    CreateDescribedEntry(
                        name: UcliPrimitiveOperationNames.Resolve,
                        kind: "query",
                        policy: "safe",
                        argsSchemaJson: """{"type":"object"}""",
                        resultSchemaJson: """{"type":"object","properties":{"globalObjectId":{"type":"string"}}}"""),
                ],
                AccessInfo: new OpsCatalogAccessInfo(
                    true,
                    true,
                    OpsCatalogSource.Index,
                    MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            UcliPrimitiveOperationNames.Resolve);

        Assert.True(result.IsSuccess);
        Assert.Equal("object", result.Output!.Operation.ArgsSchema.GetProperty("type").GetString());
        Assert.Equal("Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.", result.Output.Operation.Description);
        Assert.Equal("IpcResolveOperationResult", result.Output.Operation.ResultContract.ResultType);
        Assert.True(result.Output.Operation.ResultContract.Emitted);
        Assert.Null(result.Output.Operation.GetType().GetProperty("Outputs"));
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
                    CreateDescribedEntry(
                        name: UcliPrimitiveOperationNames.Resolve,
                        kind: "query",
                        policy: "safe",
                        argsSchemaJson: "\"not-an-object\""),
                ],
                AccessInfo: new OpsCatalogAccessInfo(
                    true,
                    true,
                    OpsCatalogSource.Index,
                    MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            UcliPrimitiveOperationNames.Resolve);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }

    private static IndexOpEntryJsonContract CreateDescribedEntry (
        string name,
        string kind,
        string policy,
        string argsSchemaJson,
        string? resultSchemaJson = null)
    {
        var describe = UcliOperationDescribeCatalog.Get(name);
        return new IndexOpEntryJsonContract(
            name,
            kind,
            policy,
            argsSchemaJson,
            resultSchemaJson)
        {
            Description = describe.Description,
            Inputs = describe.Inputs,
            ResultContract = describe.ResultContract,
            Assurance = describe.Assurance,
        };
    }
}
