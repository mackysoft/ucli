using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Ops.Mapping;

public sealed class OpsDescribeResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenResultContractIsPresent_ReturnsGeneratedContracts ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());
        var entry = CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe);

        var result = mapper.Map(CreateReadOutput(entry));

        var succeeded = Assert.IsType<OpsDescribeServiceResult.Succeeded>(result);
        var operation = succeeded.Output.Operation;
        var expectedArgsContract = Assert.IsType<UcliOperationJsonContract>(entry.ArgsContract);
        var expectedResultContract = Assert.IsType<UcliOperationJsonContract>(entry.ResultContract);
        var actualResultContract = Assert.IsType<UcliOperationJsonContract>(operation.ResultContract);
        Assert.Equal(expectedArgsContract.ContractDigest, operation.ArgsContract.ContractDigest);
        Assert.Equal(expectedResultContract.ContractDigest, actualResultContract.ContractDigest);
        Assert.Equal(UcliOperationPlayModeSupport.Disallowed, operation.PlayModeSupport);
        Assert.Equal(entry.DescriptorDigest, operation.DescriptorDigest);
        Assert.Equal(entry.VerdictContract, operation.VerdictContract);
        Assert.Equal("Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.", operation.Description);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenCodeContractIsPresent_ReturnsCodeContract ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());
        var entry = CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.CsEval,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Dangerous) with
        {
            CodeContract = CreateCodeContract(),
        };

        var result = mapper.Map(CreateReadOutput(entry));

        var succeeded = Assert.IsType<OpsDescribeServiceResult.Succeeded>(result);
        Assert.NotNull(succeeded.Output.Operation.CodeContract);
        Assert.Equal(UcliCodeLanguage.CSharp, succeeded.Output.Operation.CodeContract!.Language);
        Assert.Equal("public static object? | Task | Task<T> | ValueTask | ValueTask<T> Run(UcliCsEvalContext context)", succeeded.Output.Operation.CodeContract.EntryPoint!.Signature);
        Assert.Equal("Compiled source must contain exactly one public static Run(UcliCsEvalContext context) method returning object?, Task, Task<T>, ValueTask, or ValueTask<T>.", succeeded.Output.Operation.CodeContract.EntryPoint.MatchRule);
        Assert.Equal(
            new[] { UcliCodeSourceFormKind.CompilationUnit, UcliCodeSourceFormKind.Snippet },
            succeeded.Output.Operation.CodeContract.SourceForms!.Select(static form => form.Kind!.Value));
        Assert.Equal("MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext", Assert.Single(succeeded.Output.Operation.CodeContract.ApiTypes!).FullName);
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
        UcliOperationKind kind,
        OperationPolicy policy)
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            name,
            serializerOptions.GetTypeInfo(typeof(ResolveSelectorArgs)),
            serializerOptions.GetTypeInfo(typeof(IpcResolveOperationResult)));
        var assurance = CreateAssurance(kind, policy);
        var verdictContract = kind == UcliOperationKind.Query
            ? new UcliOperationVerdictContract(
                "The supplied reference resolves to one Unity object.")
            : null;
        var describe = verdictContract == null
            ? UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
                generationResult,
                "Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.",
                assurance,
                codeContract: null)
            : UcliOperationDescribeContractBuilder.CreateJudging(
                generationResult,
                "Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.",
                assurance,
                verdictContract,
                codeContract: null);
        var entry = new IndexOpEntryJsonContract(
            Name: name,
            Kind: kind,
            Policy: policy,
            ArgsContract: describe.ArgsContract,
            DescriptorDigest: null,
            ResultContract: describe.ResultContract,
            VerdictContract: describe.VerdictContract,
            Exposure: null,
            PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
        {
            Description = describe.Description,
            Assurance = describe.Assurance,
        };
        return entry with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(entry),
        };
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        UcliOperationKind kind,
        OperationPolicy policy)
    {
        var isMutation = kind == UcliOperationKind.Mutation;
        var isDangerousPolicy = policy == OperationPolicy.Dangerous;
        var isRiskyPolicy = policy != OperationPolicy.Safe;
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
