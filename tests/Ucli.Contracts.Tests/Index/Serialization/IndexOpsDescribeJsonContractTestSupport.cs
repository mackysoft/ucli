using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Index;

internal static class IndexOpsDescribeJsonContractTestSupport
{
    public static IndexOpsDescribeJsonContract CreateGoDescribeIndexContract ()
    {
        var describe = IndexOpsDescribeContractTestData.CreateGoDescribeContract();
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: WithDescriptorDigest(
                new IndexOpEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: UcliOperationKind.Query,
                    Policy: OperationPolicy.Safe,
                    ArgsContract: describe.ArgsContract,
                    DescriptorDigest: null,
                    ResultContract: describe.ResultContract,
                    VerdictContract: describe.VerdictContract,
                    Exposure: null,
                    PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
                {
                    Description = describe.Description,
                    Assurance = describe.Assurance,
                }));
    }

    public static IndexOpsDescribeJsonContract CreateCsEvalIndexContract ()
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            UcliPrimitiveOperationNames.CsEval,
            serializerOptions.GetTypeInfo(typeof(CsEvalArgs)),
            serializerOptions.GetTypeInfo(typeof(CsEvalResult)));
        var describe = UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
            generationResult,
            "Compiles and executes C# source in the Unity Editor process.",
            new UcliOperationAssuranceContract(
                sideEffects: [UcliOperationSideEffect.ArbitrarySourceExecution],
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate the supplied source without executing it.",
                callSemantics: "Compile and execute caller-provided source in the Unity Editor process.",
                touchedContract: "Reports touched resources declared by the executed source.",
                readPostconditionContract: "Executed source may stale any read surface.",
                failureSemantics: "Execution failure may leave indeterminate process or project state.",
                dangerousNotes: ["Executes caller-provided source code."]),
            CreateCodeContract());
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Operation: WithDescriptorDigest(
                new IndexOpEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.CsEval,
                    Kind: UcliOperationKind.Mutation,
                    Policy: OperationPolicy.Dangerous,
                    ArgsContract: describe.ArgsContract,
                    DescriptorDigest: null,
                    VerdictContract: null,
                    ResultContract: describe.ResultContract,
                    Exposure: null,
                    PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
                {
                    Description = describe.Description,
                    Assurance = describe.Assurance,
                    CodeContract = describe.CodeContract,
                }));
    }

    public static IndexOpEntryJsonContract WithDescriptorDigest (IndexOpEntryJsonContract operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var descriptorWithoutDigest = operation with { DescriptorDigest = null };
        return descriptorWithoutDigest with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(descriptorWithoutDigest),
        };
    }

    private static UcliOperationCodeContract CreateCodeContract ()
    {
        return new UcliOperationCodeContract(
            UcliCodeLanguage.CSharp,
            new UcliCodeEntryPointContract(
                "public static object? | Task | Task<T> | ValueTask | ValueTask<T> Run(UcliCsEvalContext context)",
                "Compiled source must contain exactly one public static Run(UcliCsEvalContext context) method returning object?, Task, Task<T>, ValueTask, or ValueTask<T>.",
                requiredStatic: true,
                ["MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext"],
                "JSON-serializable value or awaited task-like result."),
            [
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.CompilationUnit, "Complete C# compilation unit."),
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.Snippet, "Run method body snippet."),
            ],
            [
                new UcliCodeApiTypeContract(
                    "UcliCsEvalContext",
                    "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext",
                    "Execution context.",
                    [
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
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKind.Property,
                            "ProjectPath",
                            "Gets the Unity project path.",
                            type: "System.String",
                            returnType: null,
                            parameters: Array.Empty<UcliCodeApiParameterContract>()),
                    ]),
            ]);
    }
}
