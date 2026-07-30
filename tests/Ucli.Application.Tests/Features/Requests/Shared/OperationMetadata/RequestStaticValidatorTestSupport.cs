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
        IReadOnlyList<ValidateRequestStep>? steps = null,
        bool allowPlayMode = false)
    {
        return new ValidateRequest(
            ProtocolVersion: protocolVersion,
            Steps: steps ??
            [
                CreateOpStep(0, UcliPrimitiveOperationNames.SceneOpen, new
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
            "operation-not-found" => CreateRequest(
                steps:
                [
                    CreateOpStep(0, "ucli.unknown"),
                ]),
            "operation-not-allowed" => CreateRequest(
                steps:
                [
                    CreateOpStep(0, UcliPrimitiveOperationNames.SceneSave, new
                    {
                        path = "Assets/Scenes/Main.unity",
                    }),
                ]),
            "edit-step-invalid" => CreateRequest(
                steps:
                [
                    CreateEditStep(
                        stepIndex: 0,
                        """
                        {
                          "kind": "edit",
                          "on": {
                            "kind": "scene",
                            "path": "Assets/Scenes/Main.unity"
                          },
                          "select": {
                            "kind": "gameObject",
                            "path": "Root/Spawner",
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
        int stepIndex,
        string operationName,
        object? args = null)
    {
        return new ValidateRequestStep(
            Kind: IpcExecuteStepKind.Op,
            StepIndex: stepIndex,
            Op: operationName,
            Args: JsonSerializer.SerializeToElement(args ?? new
            {
            }));
    }

    public static ValidateRequestStep CreateOpStep (
        int stepIndex,
        string operationName,
        string argsJson)
    {
        using var argsDocument = JsonDocument.Parse(argsJson);
        return new ValidateRequestStep(
            Kind: IpcExecuteStepKind.Op,
            StepIndex: stepIndex,
            Op: operationName,
            Args: argsDocument.RootElement.Clone());
    }

    public static ValidateRequestStep CreateEditStep (
        int stepIndex,
        string stepJson)
    {
        using var stepDocument = JsonDocument.Parse(stepJson);
        var requestElement = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            steps = new[]
            {
                stepDocument.RootElement,
            },
        });
        if (!IpcExecuteArgumentsContractReader.TryRead(
                requestElement,
                out var request,
                out var error))
        {
            throw new InvalidOperationException(error.Message);
        }

        var edit = Assert.Single(request.Steps!)!;
        return new ValidateRequestStep(
            Kind: IpcExecuteStepKind.Edit,
            StepIndex: stepIndex,
            Op: null,
            Args: default)
        {
            EditContract = edit.EditContract,
        };
    }

    public static ValidateRequestStep CreateSceneEnsureEditStep (int stepIndex)
    {
        return CreateEditStep(
            stepIndex,
            """
            {
              "kind": "edit",
              "on": {
                "kind": "scene",
                "path": "Assets/Scenes/Main.unity"
              },
              "select": {
                "kind": "gameObject",
                "path": "Root/Spawner",
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
            """);
    }

    public static ValidateRequestStep CreateAssetSetEditStep (
        int stepIndex,
        string contextKind)
    {
        var on = contextKind switch
        {
            "asset" => """
                "on": {
                  "kind": "asset",
                  "path": "Assets/Data/Config.asset"
                }
                """,
            "project" => """
                "on": {
                  "kind": "project"
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(contextKind), contextKind, "Unsupported edit context kind."),
        };
        var select = contextKind switch
        {
            "asset" => """
                "select": {
                  "kind": "self",
                  "cardinality": "one"
                }
                """,
            "project" => """
                "select": {
                  "kind": "projectAsset",
                  "path": "ProjectSettings/TagManager.asset",
                  "cardinality": "one"
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(contextKind), contextKind, "Unsupported edit context kind."),
        };

        return CreateEditStep(
            stepIndex,
            $$"""
            {
              "kind": "edit",
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
            DescriptorDigest: Sha256DigestTestFactory.Compute(operationName),
            VerdictContract: null,
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
