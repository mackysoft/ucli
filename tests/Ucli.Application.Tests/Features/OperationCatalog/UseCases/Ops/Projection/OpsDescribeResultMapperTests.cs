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
        Assert.Equal("disallowed", result.Output.Operation.PlayModeSupport);
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
        Assert.Equal(UcliCodeLanguage.CSharp, result.Output.Operation.CodeContract!.Language);
        Assert.Equal("public static object? | Task | Task<T> | ValueTask | ValueTask<T> Run(UcliCsEvalContext context)", result.Output.Operation.CodeContract.EntryPoint!.Signature);
        Assert.Equal("Compiled source must contain exactly one public static Run(UcliCsEvalContext context) method returning object?, Task, Task<T>, ValueTask, or ValueTask<T>.", result.Output.Operation.CodeContract.EntryPoint.MatchRule);
        Assert.Equal(
            new[] { UcliCodeSourceFormKind.CompilationUnit, UcliCodeSourceFormKind.Snippet },
            result.Output.Operation.CodeContract.SourceForms!.Select(static form => form.Kind!.Value));
        Assert.Equal("MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext", Assert.Single(result.Output.Operation.CodeContract.ApiTypes!).FullName);
    }

    private static OpsDescribeReadOutput CreateReadOutput (IndexOpEntryJsonContract entry)
    {
        return new OpsDescribeReadOutput(
            Operation: OperationCatalogTestFixtures.CreateValidatedOperation(entry),
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
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()));
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
            Assurance = CreateAssurance(kind, policy),
        };
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        string kind,
        string policy)
    {
        var isMutation = string.Equals(kind, "mutation", StringComparison.Ordinal);
        var isDangerousPolicy = string.Equals(policy, "dangerous", StringComparison.Ordinal);
        var isRiskyPolicy = !string.Equals(policy, "safe", StringComparison.Ordinal);
        return new UcliOperationAssuranceContract(
            sideEffects: isDangerousPolicy
                ? [UcliOperationSideEffect.AssetSave, UcliOperationSideEffect.ArbitrarySourceExecution]
                : isMutation ? [UcliOperationSideEffect.AssetSave] : [UcliOperationSideEffect.ObservesUnityState],
            touchedKinds: isMutation ? [UcliTouchedResourceKind.Asset] : Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: isMutation ? "Execute the mutation against live Unity state." : "Read Unity state without applying mutation.",
            touchedContract: isMutation ? "Reports the resource touched by the mutation." : "Returns no touched resources.",
            readPostconditionContract: isMutation ? "Touched resource read surfaces may be stale after a successful call." : "Does not stale read surfaces by itself.",
            failureSemantics: isMutation ? "Failure may leave partial or indeterminate Unity state changes." : "Failure means the observation was not fully produced.",
            dangerousNotes: isRiskyPolicy ? ["Fixture operation has policy-specific risk metadata for contract validation."] : Array.Empty<string>());
    }

    private static UcliOperationCodeContract CreateCodeContract ()
    {
        return new UcliOperationCodeContract(
            UcliCodeLanguage.CSharp,
            new UcliCodeEntryPointContract(
                "public static object? | Task | Task<T> | ValueTask | ValueTask<T> Run(UcliCsEvalContext context)",
                "Compiled source must contain exactly one public static Run(UcliCsEvalContext context) method returning object?, Task, Task<T>, ValueTask, or ValueTask<T>.",
                requiredStatic: true,
                new[] { "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext" },
                "JSON-serializable value or awaited task-like result."),
            new[]
            {
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.CompilationUnit, "Complete C# compilation unit."),
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.Snippet, "Run method body snippet."),
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
                            UcliCodeApiMemberKind.Method,
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
