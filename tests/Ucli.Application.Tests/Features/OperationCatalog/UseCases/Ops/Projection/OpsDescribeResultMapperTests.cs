using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Ops.Mapping;

public sealed class OpsDescribeResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenResultSchemaIsPresent_ReturnsArgsAndResultSchemas ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(CreateReadOutput(CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: "query",
            policy: "safe",
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: """{"type":"object","properties":{"globalObjectId":{"type":"string"}}}""")));

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
    public void Map_WhenCodeContractIsPresent_ReturnsCodeContract ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());
        var entry = CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.CsEval,
            kind: "mutation",
            policy: "dangerous",
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: """{"type":"object"}""") with
        {
            CodeContract = CreateCodeContract(),
        };

        var result = mapper.Map(CreateReadOutput(entry));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output!.Operation.CodeContract);
        Assert.Equal("csharp", result.Output.Operation.CodeContract!.Language);
        Assert.Equal("public static object? Run(UcliCsEvalContext context)", result.Output.Operation.CodeContract.EntryPoint!.Signature);
        Assert.Equal("Compiled source must contain exactly one matching Run method.", result.Output.Operation.CodeContract.EntryPoint.MatchRule);
        Assert.Equal(new[] { CsEvalSourceKindValues.CompilationUnit, CsEvalSourceKindValues.Snippet }, result.Output.Operation.CodeContract.SourceForms!.Select(static form => form.Kind));
        Assert.Equal("MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext", Assert.Single(result.Output.Operation.CodeContract.ApiTypes!).FullName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenArgsSchemaIsInvalid_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(CreateReadOutput(CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: "query",
            policy: "safe",
            argsSchemaJson: "\"not-an-object\"")));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenArgsSchemaIsMissing_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(CreateReadOutput(CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: "query",
            policy: "safe",
            argsSchemaJson: null,
            resultSchemaJson: """{"type":"object"}""")));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenResultSchemaIsInvalid_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(CreateReadOutput(CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: "query",
            policy: "safe",
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: "\"not-an-object\"")));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenEmittedResultMissesResultSchema_ReturnsInternalError ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(CreateReadOutput(CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: "query",
            policy: "safe",
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: null)));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
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

        var result = mapper.Map(CreateReadOutput(entry));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
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

        var result = mapper.Map(CreateReadOutput(entry));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
    }

    private static OpsDescribeReadOutput CreateReadOutput (IndexOpEntryJsonContract entry)
    {
        return new OpsDescribeReadOutput(
            Operation: entry,
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

    private static UcliOperationCodeContract CreateCodeContract ()
    {
        return new UcliOperationCodeContract(
            "csharp",
            new UcliCodeEntryPointContract(
                "public static object? Run(UcliCsEvalContext context)",
                "Compiled source must contain exactly one matching Run method.",
                requiredStatic: true,
                new[] { "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext" },
                "JSON-serializable value."),
            new[]
            {
                new UcliCodeSourceFormContract(CsEvalSourceKindValues.CompilationUnit, "Complete C# compilation unit."),
                new UcliCodeSourceFormContract(CsEvalSourceKindValues.Snippet, "Run method body snippet."),
            },
            new[]
            {
                new UcliCodeApiTypeContract(
                    "UcliCsEvalContext",
                    "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext",
                    "Execution context.",
                    new[]
                    {
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKindValues.Method,
                            "Log",
                            "Records an informational eval log entry.",
                            type: null,
                            returnType: "void",
                            parameters:
                            [
                                new UcliCodeApiParameterContract("message", "System.String", "Log message text."),
                            ]),
                    }),
            });
    }
}
