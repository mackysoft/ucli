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
            Operation: new IndexOpEntryJsonContract(
                Name: UcliPrimitiveOperationNames.GoDescribe,
                Kind: "query",
                Policy: "safe",
                ArgsSchemaJson: """{"type":"object"}""",
                ResultSchemaJson: """{"type":"object"}""")
            {
                Description = describe.Description,
                Inputs = describe.Inputs,
                ResultContract = describe.ResultContract,
                Assurance = describe.Assurance,
            });
    }

    public static IndexOpsDescribeJsonContract CreateWriteAssetIndexContract ()
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Operation: new IndexOpEntryJsonContract(
                Name: "write.asset",
                Kind: "mutation",
                Policy: "safe",
                ArgsSchemaJson: """{"type":"object"}""",
                ResultSchemaJson: """{"$ref":"#/definitions/WriteResult"}""")
            {
                Description = "Writes one asset.",
                Inputs =
                [
                    new UcliOperationInputContract(
                        name: "target",
                        valueType: "object",
                        description: "Target input.",
                        constraints:
                        [
                            new UcliOperationInputConstraintContract("assetExists")
                            {
                                AssetKind = "scene",
                            },
                            new UcliOperationInputConstraintContract("referenceResolvable")
                            {
                                TargetKind = "gameObject",
                            },
                            new UcliOperationInputConstraintContract("typeAssignableTo")
                            {
                                TypeKind = "component",
                            },
                            new UcliOperationInputConstraintContract("serializedProperty")
                            {
                                Access = "write",
                            },
                            new UcliOperationInputConstraintContract("range")
                            {
                                Min = 1.5,
                                Max = 3.5,
                            },
                        ],
                        argsPath: "$.target",
                        variants:
                        [
                            new UcliOperationInputVariantContract(
                                name: "byPath",
                                description: "Path selector.",
                                fields:
                                [
                                    new UcliOperationInputVariantFieldContract(
                                        name: "path",
                                        argsPath: "$.target.path",
                                        description: "Serialized path.",
                                        constraints:
                                        [
                                            new UcliOperationInputConstraintContract("nonEmpty"),
                                        ]),
                                ]),
                        ]),
                ],
                ResultContract = new UcliOperationResultContract(
                    emitted: true,
                    resultType: "WriteResult",
                    description: "Written result."),
                Assurance = new UcliOperationAssuranceContract(
                    sideEffects:
                    [
                        UcliOperationSideEffect.AssetContentMutation,
                        UcliOperationSideEffect.AssetSave,
                        UcliOperationSideEffect.ArbitrarySourceExecution,
                    ],
                    touchedKinds:
                    [
                        UcliTouchedResourceKind.Asset,
                    ],
                    planMode: UcliOperationPlanMode.MayCreatePreviewState,
                    planSemantics: "Validate asset write inputs and compute preview state without persisting project data.",
                    callSemantics: "Write the requested asset data to Unity project state.",
                    touchedContract: "Reports the asset resource affected by the write.",
                    readPostconditionContract: "Asset read surfaces may be stale after a successful call.",
                    failureSemantics: "Write failure may leave partial or indeterminate asset state.",
                    dangerousNotes: Array.Empty<string>()),
                CodeContract = new UcliOperationCodeContract(
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
                                new UcliCodeApiMemberContract(
                                    UcliCodeApiMemberKind.Property,
                                    "ProjectPath",
                                    "Gets the Unity project path.",
                                    type: "System.String",
                                    returnType: null,
                                    parameters: Array.Empty<UcliCodeApiParameterContract>()),
                            }),
                    }),
            });
    }
}
