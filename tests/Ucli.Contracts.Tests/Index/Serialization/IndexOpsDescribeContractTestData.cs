using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Tests.Index;

internal static class IndexOpsDescribeContractTestData
{
    public const string GoDescribeDescriptorDigest = "0000000000000000000000000000000000000000000000000000000000000000";
    public const string GoDescribeCalculatedDescriptorDigest = "3a1de5897c2e60da7c9afb13be779fe9c876f010ffdbe9a35bf7d148df212439";
    public const string CodeOperationDescriptorDigest = "3333333333333333333333333333333333333333333333333333333333333333";
    public const string ArgsContractDigest = "1111111111111111111111111111111111111111111111111111111111111111";
    public const string ResultContractDigest = "2222222222222222222222222222222222222222222222222222222222222222";
    public const string GoDescribeDescription = "Returns a GameObject description including components and child hierarchy.";
    public const string GoDescribeVerdictDescription = "The requested GameObject exists and its description is complete.";
    public const string CodeOperationName = "test.code.execute";
    public const string CodeOperationDescription = "Executes caller-provided source in the test operation fixture.";

    public static IndexOpEntryJsonContract CreateGoDescribeOperation ()
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: UcliOperationKind.Query,
            Policy: OperationPolicy.Safe,
            ArgsContract: CreateArgsContract(),
            DescriptorDigest: Sha256Digest.Parse(GoDescribeDescriptorDigest),
            VerdictContract: new UcliOperationVerdictContract(GoDescribeVerdictDescription),
            ResultContract: CreateResultContract(),
            Exposure: null,
            PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
        {
            Description = GoDescribeDescription,
            Assurance = CreateReadOnlyAssurance(),
        };
    }

    public static IndexOpEntryJsonContract CreateCodeOperation ()
    {
        return new IndexOpEntryJsonContract(
            Name: CodeOperationName,
            Kind: UcliOperationKind.Mutation,
            Policy: OperationPolicy.Dangerous,
            ArgsContract: CreateArgsContract(),
            DescriptorDigest: Sha256Digest.Parse(CodeOperationDescriptorDigest),
            VerdictContract: null,
            ResultContract: CreateResultContract(),
            Exposure: null,
            PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
        {
            Description = CodeOperationDescription,
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: [UcliOperationSideEffect.ArbitrarySourceExecution],
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate the supplied source without executing it.",
                callSemantics: "Compile and execute caller-provided source in the Unity Editor process.",
                touchedContract: "Reports touched resources declared by the executed source.",
                readPostconditionContract: "Executed source may stale any read surface.",
                failureSemantics: "Execution failure may leave indeterminate process or project state.",
                dangerousNotes: ["Executes caller-provided source code."]),
            CodeContract = CreateCodeContract(),
        };
    }

    public static UcliOperationJsonContract CreateArgsContract ()
    {
        return CreateJsonContract(ArgsContractDigest, "args");
    }

    public static UcliOperationJsonContract CreateResultContract ()
    {
        return CreateJsonContract(ResultContractDigest, "result");
    }

    private static UcliOperationAssuranceContract CreateReadOnlyAssurance ()
    {
        return new UcliOperationAssuranceContract(
            sideEffects: Array.Empty<UcliOperationSideEffect>(),
            touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: "Read Unity state without applying mutation.",
            touchedContract: "Returns no touched resources.",
            readPostconditionContract: "Does not stale read surfaces by itself.",
            failureSemantics: "Failure means the observation was not fully produced.",
            dangerousNotes: Array.Empty<string>());
    }

    private static UcliOperationJsonContract CreateJsonContract (
        string contractDigest,
        string title)
    {
        using var typeMetadata = JsonDocument.Parse(
            "{\"contractDigest\":\"" + contractDigest + "\",\"title\":\"" + title + "\"}");
        using var schema = JsonDocument.Parse(
            "{\"x-contract-digest\":\"" + contractDigest + "\",\"type\":\"object\",\"properties\":{}}");
        return new UcliOperationJsonContract(
            Sha256Digest.Parse(contractDigest),
            new UcliJsonObject(typeMetadata.RootElement),
            new UcliJsonObject(schema.RootElement));
    }

    private static UcliOperationCodeContract CreateCodeContract ()
    {
        return new UcliOperationCodeContract(
            UcliCodeLanguage.CSharp,
            new UcliCodeEntryPointContract(
                "public static object? Run(ExampleContext context)",
                "Compiled source must contain exactly one public static Run(ExampleContext context) method.",
                requiredStatic: true,
                ["Example.Context"],
                "JSON-serializable value."),
            [
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.CompilationUnit, "Complete C# compilation unit."),
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.Snippet, "Run method body snippet."),
            ],
            [
                new UcliCodeApiTypeContract(
                    "ExampleContext",
                    "Example.Context",
                    "Execution context.",
                    [
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKind.Method,
                            "Log",
                            "Records an informational fixture log entry.",
                            type: null,
                            returnType: "void",
                            parameters:
                            [
                                new UcliCodeApiParameterContract("message", "System.String", "Log message text."),
                            ]),
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKind.Property,
                            "WorkspacePath",
                            "Gets the fixture workspace path.",
                            type: "System.String",
                            returnType: null,
                            parameters: Array.Empty<UcliCodeApiParameterContract>()),
                    ]),
            ]);
    }
}
