namespace MackySoft.Ucli.Application.Tests;

using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

internal static class RequestStaticValidatorTestSupport
{
    public static readonly ResolvedUnityProjectContext ValidationUnityProject =
        ProjectContextTestFactory.CreateTemporaryFixtureUnityProject();

    public static readonly InvalidRequestCase[] InvalidRequestCases =
    [
        new("protocol-version-mismatch", IpcProtocolErrorCodes.ProtocolVersionMismatch),
        new("request-id-invalid", ValidationErrorCodes.RequestIdInvalid),
        new("request-id-not-canonical-d", ValidationErrorCodes.RequestIdInvalid),
        new("steps-required", ValidationErrorCodes.StepsRequired),
        new("step-id-duplicated", ValidationErrorCodes.StepIdDuplicated),
        new("operation-not-found", ValidationErrorCodes.OperationNotFound),
        new("operation-not-allowed", OperationAuthorizationErrorCodes.OperationNotAllowed),
        new("edit-step-invalid", ValidationErrorCodes.EditStepInvalid),
    ];

    public static readonly string[] EditLoweringOnlyPrimitiveNames =
    [
        UcliPrimitiveOperationNames.AssetCreate,
        UcliPrimitiveOperationNames.AssetSave,
        UcliPrimitiveOperationNames.AssetSet,
        UcliPrimitiveOperationNames.CompEnsure,
        UcliPrimitiveOperationNames.CompSet,
        UcliPrimitiveOperationNames.GoCreate,
        UcliPrimitiveOperationNames.PrefabApplyOverrides,
        UcliPrimitiveOperationNames.PrefabCreate,
        UcliPrimitiveOperationNames.PrefabRevertOverrides,
    ];

    public static IRequestStaticValidator CreateValidator ()
    {
        var authorizationService = new OperationAuthorizationService();
        return new RequestStaticValidator(authorizationService);
    }

    public static ValidateRequest CreateRequest (
        int protocolVersion = IpcProtocol.CurrentVersion,
        string? requestId = null,
        IReadOnlyList<ValidateRequestStep?>? steps = null,
        bool allowPlayMode = false)
    {
        return new ValidateRequest(
            ProtocolVersion: protocolVersion,
            RequestId: requestId ?? Guid.NewGuid().ToString(),
            Steps: steps ??
            [
                CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneOpen, new
                {
                    path = "Assets/Scenes/Main.unity",
                }),
            ],
            AllowPlayMode: allowPlayMode);
    }

    public static ValidateRequest CreateInvalidRequest (string scenario)
    {
        return scenario switch
        {
            "protocol-version-mismatch" => CreateRequest(protocolVersion: IpcProtocol.CurrentVersion + 1),
            "request-id-invalid" => CreateRequest(requestId: "invalid-request-id"),
            "request-id-not-canonical-d" => CreateRequest(requestId: Guid.NewGuid().ToString("B")),
            "steps-required" => new ValidateRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: Guid.NewGuid().ToString(),
                Steps: null),
            "step-id-duplicated" => CreateRequest(
                steps:
                [
                    CreateOpStep("dup", UcliPrimitiveOperationNames.SceneOpen, new
                    {
                        path = "Assets/Scenes/Main.unity",
                    }),
                    CreateOpStep("dup", UcliPrimitiveOperationNames.SceneTree, new
                    {
                        path = "Assets/Scenes/Main.unity",
                    }),
                ]),
            "operation-not-found" => CreateRequest(
                steps:
                [
                    CreateOpStep("step-1", "ucli.unknown"),
                ]),
            "operation-not-allowed" => CreateRequest(
                steps:
                [
                    CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneSave, new
                    {
                        path = "Assets/Scenes/Main.unity",
                    }),
                ]),
            "edit-step-invalid" => CreateRequest(
                steps:
                [
                    CreateEditStep(
                        stepId: "edit-1",
                        """
                        {
                          "kind": "edit",
                          "id": "edit-1",
                          "on": {
                            "scene": "Assets/Scenes/Main.unity"
                          },
                          "select": {
                            "gameObject": "Root/Spawner",
                            "cardinality": "one"
                          },
                          "actions": [
                            {
                              "kind": "set",
                              "target": "$missing",
                              "values": {
                                "spawnInterval": 3.0
                              }
                            }
                          ],
                          "commit": "context"
                        }
                        """),
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported invalid request scenario."),
        };
    }

    public static ValidateRequestStep CreateOpStep (
        string stepId,
        string operationName,
        object? args = null)
    {
        var stepElement = JsonSerializer.SerializeToElement(new
        {
            kind = "op",
            id = stepId,
            op = operationName,
            args = args ?? new
            {
            },
        });

        return new ValidateRequestStep(
            Kind: IpcRequestStepKind.Op,
            StepId: stepId,
            Op: operationName,
            Element: stepElement);
    }

    public static ValidateRequestStep CreateOpStep (
        string stepId,
        string operationName,
        string argsJson)
    {
        using var argsDocument = JsonDocument.Parse(argsJson);
        var stepElement = JsonSerializer.SerializeToElement(new
        {
            kind = "op",
            id = stepId,
            op = operationName,
            args = argsDocument.RootElement.Clone(),
        });

        return new ValidateRequestStep(
            Kind: IpcRequestStepKind.Op,
            StepId: stepId,
            Op: operationName,
            Element: stepElement);
    }

    public static ValidateRequestStep CreateEditStep (
        string stepId,
        string stepJson)
    {
        using var document = JsonDocument.Parse(stepJson);
        return new ValidateRequestStep(
            Kind: IpcRequestStepKind.Edit,
            StepId: stepId,
            Op: null,
            Element: document.RootElement.Clone());
    }

    public static ValidateRequestStep CreateSceneEnsureEditStep (string stepId)
    {
        return CreateEditStep(
            stepId: stepId,
            """
            {
              "kind": "edit",
              "id": "__STEP_ID__",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "gameObject": "Root/Spawner",
                "cardinality": "one"
              },
              "actions": [
                {
                  "kind": "ensureComponent",
                  "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule"
                }
              ],
              "commit": "none"
            }
            """.Replace("__STEP_ID__", stepId, StringComparison.Ordinal));
    }

    public static ValidateRequestStep CreateAssetSetEditStep (
        string stepId,
        string contextKind)
    {
        var on = contextKind switch
        {
            "asset" => """
                "on": {
                  "asset": "Assets/Data/Config.asset"
                }
                """,
            "project" => """
                "on": {
                  "project": true
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(contextKind), contextKind, "Unsupported edit context kind."),
        };
        var select = contextKind switch
        {
            "asset" => """
                "select": {
                  "self": true,
                  "cardinality": "one"
                }
                """,
            "project" => """
                "select": {
                  "projectAsset": {
                    "path": "ProjectSettings/TagManager.asset"
                  },
                  "cardinality": "one"
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(contextKind), contextKind, "Unsupported edit context kind."),
        };

        return CreateEditStep(
            stepId: stepId,
            $$"""
            {
              "kind": "edit",
              "id": "{{stepId}}",
              {{on}},
              {{select}},
              "actions": [
                {
                  "kind": "set",
                  "values": {
                    "m_Name": "Updated"
                  }
                }
              ],
              "commit": "context"
            }
            """);
    }

    public static UcliOperationDescriptor CreateDescriptor (
        string operationName,
        UcliOperationKind kind = UcliOperationKind.Mutation,
        OperationPolicy policy = OperationPolicy.Safe,
        UcliOperationExposure exposure = UcliOperationExposure.Public)
    {
        return new UcliOperationDescriptor(
            Name: operationName,
            Kind: kind,
            Policy: policy,
            ArgsSchemaJson: """{"type":"object"}""",
            ResultSchemaJson: null,
            Exposure: exposure);
    }

    public static UcliConfig CreateConfig (
        OperationPolicy operationPolicy,
        params string[] allowlistPatterns)
    {
        return new UcliConfig(
            SchemaVersion: 1,
            OperationPolicy: operationPolicy,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist: allowlistPatterns);
    }

    public static void AssertContainsError (ValidationResult result, UcliCode errorCode)
    {
        Assert.Contains(
            result.Errors,
            error => error.Code == errorCode);
    }

    public static void AssertContainsEditLoweringOnlyError (
        ValidationResult result,
        string operationName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == UcliCoreErrorCodes.InvalidArgument
                     && error.Message.Contains(operationName, StringComparison.Ordinal)
                     && error.Message.Contains("available only through edit lowering", StringComparison.Ordinal));
    }

    public sealed record InvalidRequestCase (
        string Scenario,
        UcliCode ExpectedErrorCode);
}
