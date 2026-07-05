using MackySoft.Ucli.Contracts.Index;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexOpsDescribeJsonFullMetadataTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Writer_EmitsOptionalOperationMetadataAndSchemaObjectsWithStableFields ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateWriteAssetIndexContract();

        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "sourceInputsHash": "hash",
                  "operation": {
                    "name": "write.asset",
                    "kind": "mutation",
                    "policy": "safe",
                    "description": "Writes one asset.",
                    "inputs": [
                      {
                        "name": "target",
                        "description": "Target input.",
                        "valueType": "object",
                        "constraints": [
                          {
                            "kind": "assetExists",
                            "assetKind": "scene"
                          },
                          {
                            "kind": "referenceResolvable",
                            "targetKind": "gameObject"
                          },
                          {
                            "kind": "typeAssignableTo",
                            "typeKind": "component"
                          },
                          {
                            "kind": "serializedProperty",
                            "access": "write"
                          },
                          {
                            "kind": "range",
                            "min": 1.5,
                            "max": 3.5
                          }
                        ],
                        "argsPath": "$.target",
                        "variants": [
                          {
                            "name": "byPath",
                            "description": "Path selector.",
                            "fields": [
                              {
                                "name": "path",
                                "argsPath": "$.target.path",
                                "description": "Serialized path.",
                                "constraints": [
                                  {
                                    "kind": "nonEmpty"
                                  }
                                ]
                              }
                            ]
                          }
                        ]
                      }
                    ],
                    "resultContract": {
                      "emitted": true,
                      "resultType": "WriteResult",
                      "description": "Written result."
                    },
                    "assurance": {
                      "sideEffects": [
                        "assetContentMutation",
                        "assetSave",
                        "arbitrarySourceExecution"
                      ],
                      "mayDirty": true,
                      "mayPersist": true,
                      "touchedKinds": [
                        "asset"
                      ],
                      "planMode": "mayCreatePreviewState",
                      "planSemantics": "Validate asset write inputs and compute preview state without persisting project data.",
                      "callSemantics": "Write the requested asset data to Unity project state.",
                      "touchedContract": "Reports the asset resource affected by the write.",
                      "readPostconditionContract": "Asset read surfaces may be stale after a successful call.",
                      "failureSemantics": "Write failure may leave partial or indeterminate asset state.",
                      "dangerousNotes": []
                    },
                    "codeContract": {
                      "language": "csharp",
                      "entryPoint": {
                        "signature": "public static object? Run(UcliCsEvalContext context)",
                        "matchRule": "Compiled source must contain exactly one matching Run method.",
                        "requiredStatic": true,
                        "parameterTypes": [
                          "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext"
                        ],
                        "returnValue": "JSON-serializable value."
                      },
                      "sourceForms": [
                        {
                          "kind": "compilationUnit",
                          "description": "Complete C# compilation unit."
                        },
                        {
                          "kind": "snippet",
                          "description": "Run method body snippet."
                        }
                      ],
                      "apiTypes": [
                        {
                          "name": "UcliCsEvalContext",
                          "fullName": "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext",
                          "description": "Execution context.",
                          "members": [
                            {
                              "kind": "method",
                              "name": "Log",
                              "description": "Records an informational eval log entry.",
                              "type": null,
                              "returnType": "void",
                              "parameters": [
                                {
                                  "name": "message",
                                  "type": "System.String",
                                  "description": "Log message text."
                                }
                              ]
                            },
                            {
                              "kind": "property",
                              "name": "ProjectPath",
                              "description": "Gets the Unity project path.",
                              "type": "System.String",
                              "returnType": null,
                              "parameters": []
                            }
                          ]
                        }
                      ]
                    },
                    "argsSchema": {
                      "type": "object"
                    },
                    "resultSchema": {
                      "$ref": "#/definitions/WriteResult"
                    }
                  }
                }
                """),
            json);
    }
}
