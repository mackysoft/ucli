using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Cli;

internal static class OpsCommandTestSupport
{
    public static OpsListServiceResult CreateListSuccess (params OpsOperationListItem[] operations)
    {
        return OpsListServiceResult.Success(
            new OpsListExecutionOutput(
                Operations: operations,
                ReadIndex: CreateProbableReadIndex()),
            "uCLI ops list completed.");
    }

    public static RecordingOpsService CreateService ()
    {
        return new RecordingOpsService(
            CreateListSuccess(),
            CreateDefaultDescribeSuccess());
    }

    public static OpsDescribeServiceResult CreateDefaultDescribeSuccess ()
    {
        return CreateDescribeSuccess(
            operationName: UcliPrimitiveOperationNames.GoDescribe,
            kind: "query",
            policy: "safe",
            description: "Returns a GameObject description including components and child hierarchy.",
            inputs: [],
            resultContract: UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description result."),
            assurance: OpsCliOutputContractTestSupport.CreateAssurance("query", "safe"),
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: """{"type":"object"}""");
    }

    public static OpsDescribeServiceResult CreateDescribeSuccess (
        string operationName,
        string kind,
        string policy,
        string description,
        IReadOnlyList<UcliOperationInputContract> inputs,
        UcliOperationResultContract resultContract,
        UcliOperationAssuranceContract assurance,
        string argsSchemaJson,
        string? resultSchemaJson)
    {
        return OpsDescribeServiceResult.Success(
            new OpsDescribeExecutionOutput(
                Operation: new OpsOperationDetail(
                    name: operationName,
                    kind: kind,
                    policy: policy,
                    playModeSupport: "disallowed",
                    description: description,
                    inputs: inputs,
                    resultContract: resultContract,
                    assurance: assurance,
                    codeContract: null,
                    argsSchema: ParseJsonElement(argsSchemaJson),
                    resultSchema: resultSchemaJson == null ? null : ParseJsonElement(resultSchemaJson)),
                ReadIndex: CreateProbableReadIndex()),
            $"uCLI ops describe completed for '{operationName}'.");
    }

    public static IReadOnlyList<UcliOperationInputContract> CreateVariantInputs ()
    {
        return
        [
            new UcliOperationInputContract(
                name: "target",
                valueType: "object",
                description: "Target GameObject reference.",
                constraints:
                [
                    new UcliOperationInputConstraintContract("referenceResolvable")
                    {
                        TargetKind = "gameObject",
                    },
                ],
                argsPath: "$.target",
                variants:
                [
                    new UcliOperationInputVariantContract(
                        name: "byGlobalObjectId",
                        description: "Use resolved Unity GlobalObjectId.",
                        fields:
                        [
                            new UcliOperationInputVariantFieldContract(
                                name: "globalObjectId",
                                argsPath: "$.target.globalObjectId",
                                description: "Resolved Unity GlobalObjectId.",
                                constraints:
                                [
                                    new UcliOperationInputConstraintContract("globalObjectId"),
                                ]),
                        ]),
                    new UcliOperationInputVariantContract(
                        name: "bySceneHierarchyPath",
                        description: "Use Scene asset path for a hierarchy selector and Unity hierarchy path inside the selected scene or prefab.",
                        fields:
                        [
                            new UcliOperationInputVariantFieldContract(
                                name: "scene",
                                argsPath: "$.target.scene",
                                description: "Scene asset path for a hierarchy selector.",
                                constraints:
                                [
                                    new UcliOperationInputConstraintContract("assetExists")
                                    {
                                        AssetKind = "scene",
                                    },
                                ]),
                            new UcliOperationInputVariantFieldContract(
                                name: "hierarchyPath",
                                argsPath: "$.target.hierarchyPath",
                                description: "Unity hierarchy path inside the selected scene or prefab.",
                                constraints:
                                [
                                    new UcliOperationInputConstraintContract("hierarchyPath"),
                                ]),
                        ]),
                ]),
            new UcliOperationInputContract(
                name: "depth",
                valueType: "integer",
                description: "Maximum child hierarchy depth to include; null means unbounded.",
                constraints:
                [
                    new UcliOperationInputConstraintContract("range")
                    {
                        Min = 0,
                    },
                ]),
        ];
    }

    private static ReadIndexInfo CreateProbableReadIndex ()
    {
        return new ReadIndexInfo(
            true,
            true,
            ReadIndexInfoSource.Index,
            IndexFreshness.Probable,
            DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
            null);
    }

    private static JsonElement ParseJsonElement (string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
