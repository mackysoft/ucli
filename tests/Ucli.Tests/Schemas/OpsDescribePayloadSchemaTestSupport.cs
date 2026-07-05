using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Schemas;

internal static class OpsDescribePayloadSchemaTestSupport
{
    public static InvalidPayloadCase[] GetInvalidPayloadCases ()
    {
        return
        [
            new(
                "public planMode must not allow preview state",
                CreatePayload(planMode: "mayCreatePreviewState")),
            new(
                "policyDerivation must not be public",
                CreatePayload(operationExtra: ",\"policyDerivation\":{}")),
            new(
                "policyRestriction must not be public",
                CreatePayload(operationExtra: ",\"policyRestriction\":\"advancedByOverride\"")),
            new(
                "exposure must not be public",
                CreatePayload(operationExtra: ",\"exposure\":\"public\"")),
            new(
                "policyReason must not be public",
                CreatePayload(operationExtra: ",\"policyReason\":\"exposureNotPublic\"")),
            new(
                "unknown constraint parameter must not be public",
                CreatePayload(constraintExtra: ",\"unknownParameter\":true")),
            new(
                "asset constraint must require assetKind",
                CreatePayload(targetConstraintJson: """{"kind":"assetExists"}""")),
            new(
                "nonEmpty constraint must not allow range parameter",
                CreatePayload(targetConstraintJson: """{"kind":"nonEmpty","min":1}""")),
            new(
                "range constraint must require a bound",
                CreatePayload(targetConstraintJson: """{"kind":"range"}""")),
            new(
                "cursor constraint must not allow serialized property access",
                CreatePayload(targetConstraintJson: """{"kind":"cursor","access":"write"}""")),
            new(
                "input argsPath must not expose request-local alias root branch",
                CreatePayload(inputArgsPath: $"$.{UcliOperationContractPropertyNames.Alias}")),
            new(
                "input argsPath must not expose request-local alias nested branch",
                CreatePayload(inputArgsPath: $"$.target.{UcliOperationContractPropertyNames.Alias}")),
            new(
                "variant field argsPath must not expose request-local alias branch",
                CreatePayload(fieldArgsPath: $"$.target.{UcliOperationContractPropertyNames.Alias}")),
            new(
                "variant field argsPath must use uCLI args path syntax",
                CreatePayload(fieldArgsPath: "$.target[0].globalObjectId")),
        ];
    }

    public static string CreatePayload (
        string planMode = "observesLiveUnity",
        string operationExtra = "",
        string constraintExtra = "",
        string? targetConstraintJson = null,
        string inputArgsPath = "$.target",
        string fieldArgsPath = "$.target.globalObjectId")
    {
        targetConstraintJson ??= $$"""{"kind":"referenceResolvable","targetKind":"gameObject"{{constraintExtra}}}""";

        return $$"""
            {
              "operation": {
                "name": "ucli.go.describe",
                "kind": "query",
                "policy": "safe",
                "description": "Returns a GameObject description including components and child hierarchy.",
                "inputs": [
                  {
                    "name": "target",
                    "description": "Target GameObject reference.",
                    "valueType": "object",
                    "constraints": [
                      {{targetConstraintJson}}
                    ],
                    "argsPath": "{{inputArgsPath}}",
                    "variants": [
                      {
                        "name": "byGlobalObjectId",
                        "description": "Use resolved Unity GlobalObjectId.",
                        "fields": [
                          {
                            "name": "globalObjectId",
                            "argsPath": "{{fieldArgsPath}}",
                            "description": "Resolved Unity GlobalObjectId.",
                            "constraints": [
                              {
                                "kind": "globalObjectId"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "name": "bySceneHierarchyPath",
                        "description": "Use Scene asset path for a hierarchy selector and Unity hierarchy path inside the selected scene or prefab.",
                        "fields": [
                          {
                            "name": "scene",
                            "argsPath": "$.target.scene",
                            "description": "Scene asset path for a hierarchy selector.",
                            "constraints": [
                              {
                                "kind": "assetExists",
                                "assetKind": "scene"
                              }
                            ]
                          },
                          {
                            "name": "hierarchyPath",
                            "argsPath": "$.target.hierarchyPath",
                            "description": "Unity hierarchy path inside the selected scene or prefab.",
                            "constraints": [
                              {
                                "kind": "hierarchyPath"
                              }
                            ]
                          }
                        ]
                      }
                    ]
                  },
                  {
                    "name": "depth",
                    "description": "Maximum child hierarchy depth to include; null means unbounded.",
                    "valueType": "integer",
                    "constraints": [
                      {
                        "kind": "range",
                        "min": 0
                      }
                    ]
                  }
                ],
                "resultContract": {
                  "emitted": true,
                  "resultType": "GameObjectDescriptionResult",
                  "description": "GameObject describe operation result."
                },
                "assurance": {
                  "sideEffects": [
                    "observesUnityState"
                  ],
                  "mayDirty": false,
                  "mayPersist": false,
                  "touchedKinds": [],
                  "planMode": "{{planMode}}",
                  "planSemantics": "Validate arguments and observe Unity state without applying mutation.",
                  "callSemantics": "Read Unity state without applying mutation.",
                  "touchedContract": "Returns no touched resources.",
                  "readPostconditionContract": "Does not stale read surfaces by itself.",
                  "failureSemantics": "Failure means the observation was not fully produced.",
                  "dangerousNotes": []
                },
                "argsSchema": {
                  "type": "object"
                },
                "resultSchema": {
                  "type": "object"
                }{{operationExtra}}
              },
              "readIndex": {
                "used": true,
                "hit": true,
                "source": "index",
                "freshness": "fresh",
                "generatedAtUtc": "2026-05-03T00:00:00Z",
                "fallbackReason": null
              }
            }
            """;
    }

    public static void AssertSchemasUseSupportedSubset (JsonElement operation)
    {
        Assert.True(
            IndexJsonSchemaSubsetValidator.IsValidPublicRawOpArgsSchema(operation.GetProperty("argsSchema").GetRawText()),
            "Documented ops describe argsSchema must use the uCLI-supported public raw op schema subset.");

        var resultSchema = operation.GetProperty("resultSchema");
        if (resultSchema.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        Assert.True(
            IndexJsonSchemaSubsetValidator.IsValidObjectSchema(resultSchema.GetRawText()),
            "Documented ops describe resultSchema must use the uCLI-supported schema subset.");
    }

    public readonly record struct InvalidPayloadCase (
        string Name,
        string PayloadJson);
}
