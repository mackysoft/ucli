using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Ops.Mapping;

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

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenArgsSchemaIsMissing_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            CreateOutput(
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.Resolve,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: null,
                    resultSchemaJson: """{"type":"object"}""")),
            UcliPrimitiveOperationNames.Resolve);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenResultSchemaIsInvalid_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            CreateOutput(
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.Resolve,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: "\"not-an-object\"")),
            UcliPrimitiveOperationNames.Resolve);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenEmittedResultMissesResultSchema_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            CreateOutput(
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.Resolve,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: null)),
            UcliPrimitiveOperationNames.Resolve);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenNonEmittedResultHasResultSchema_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());
        var entry = CreateDescribedEntry(
                name: UcliPrimitiveOperationNames.Resolve,
                kind: "query",
                policy: "safe",
                argsSchemaJson: """{"type":"object"}""",
                resultSchemaJson: """{"type":"object"}""")
            with
        {
            ResultContract = UcliOperationResultContract.NoResult("No result."),
        };

        var result = mapper.Map(CreateOutput(entry), UcliPrimitiveOperationNames.Resolve);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("description")]
    [InlineData("inputs")]
    [InlineData("resultContract")]
    [InlineData("assurance")]
    public void Map_WhenDescribeContractIsIncomplete_ReturnsInternalError (string missingField)
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());
        var entry = CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: "query",
            policy: "safe",
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: """{"type":"object"}""");

        entry = missingField switch
        {
            "description" => entry with { Description = null },
            "inputs" => entry with { Inputs = null },
            "resultContract" => entry with { ResultContract = null },
            "assurance" => entry with { Assurance = null },
            _ => throw new ArgumentOutOfRangeException(nameof(missingField), missingField, "Unsupported field."),
        };

        var result = mapper.Map(CreateOutput(entry), UcliPrimitiveOperationNames.Resolve);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }

    private static OpsCatalogReadOutput CreateOutput (IndexOpEntryJsonContract entry)
    {
        return new OpsCatalogReadOutput(
            Operations:
            [
                entry,
            ],
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
    }

    private static IndexOpEntryJsonContract CreateDescribedEntry (
        string name,
        string kind,
        string policy,
        string? argsSchemaJson,
        string? resultSchemaJson = null)
    {
        var describe = UcliOperationDescribeContractBuilder.Create<ResolveSelectorArgs, IpcResolveOperationResult>(
            "Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));
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
