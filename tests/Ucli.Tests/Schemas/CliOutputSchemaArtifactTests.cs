using System.Text.Json;
using System.Text.RegularExpressions;
using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class CliOutputSchemaArtifactTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    [Trait("Size", "Small")]
    public void SchemaManifest_IndexesGeneratedV1Schemas ()
    {
        var schemaRoot = Path.Combine(RepositoryRoot, "schemas", "v1");
        var manifestPath = Path.Combine(schemaRoot, "schema-manifest.json");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        Assert.Equal("ucli", root.GetProperty("schemaSet").GetString());
        Assert.Equal("v1", root.GetProperty("schemaSetVersion").GetString());
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("0.0.0", root.GetProperty("packageVersion").GetString());
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("jsonSchemaDialect").GetString());

        var commandEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var schemaEntry in root.GetProperty("schemas").EnumerateArray())
        {
            var path = schemaEntry.GetProperty("path").GetString();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(
                File.Exists(Path.Combine(schemaRoot, path!)),
                $"Schema manifest references missing schema path: {path}");
            Assert.DoesNotContain("v1", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);

            if (schemaEntry.TryGetProperty("command", out var commandElement))
            {
                commandEntries.Add(commandElement.GetString()!, path!);
            }
        }

        Assert.Contains("status", commandEntries.Keys);
        Assert.Contains("ready", commandEntries.Keys);
        Assert.Contains("compile", commandEntries.Keys);
        Assert.Contains("verify", commandEntries.Keys);
        Assert.Contains("plan", commandEntries.Keys);
        Assert.Contains("ops.describe", commandEntries.Keys);
        Assert.Contains("codes.describe", commandEntries.Keys);
        Assert.Contains("test.run", commandEntries.Keys);
    }

    [Theory]
    [MemberData(nameof(GetCliOutputGoldenFiles))]
    [Trait("Size", "Small")]
    public void CliOutputGoldenFile_MatchesEnvelopeAndCommandPayloadSchemas (string repositoryRelativeGoldenPath)
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativeGoldenPath)));
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("schemaVersion", out _));
        AssertSchemaValid(schemaSet.Validate("cli-output/envelope.schema.json", root), repositoryRelativeGoldenPath);

        var command = root.GetProperty("command").GetString();
        Assert.False(string.IsNullOrWhiteSpace(command));
        var payloadSchemaPath = schemaSet.FindPayloadSchemaPath(command!);
        Assert.False(
            string.IsNullOrWhiteSpace(payloadSchemaPath),
            $"No payload schema is registered for command '{command}' in {repositoryRelativeGoldenPath}.");

        AssertSchemaValid(
            schemaSet.Validate(payloadSchemaPath!, root.GetProperty("payload"), "$.payload"),
            repositoryRelativeGoldenPath);
    }

    [Theory]
    [MemberData(nameof(GetReportRefContractCases))]
    [Trait("Size", "Small")]
    public void ReportRefSchema_RequiresKindAndExactlyOneLocation (
        string reportJson,
        bool expectedValid)
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(reportJson);

        var errors = schemaSet.Validate("cli-output/defs/report-ref.schema.json", document.RootElement);

        if (expectedValid)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.NotEmpty(errors);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyPayloadSchema_RequiresClaimValidity ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(
            """
            {
              "verdict": "pass",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "verifiers": [
                {
                  "id": "ready.lifecycle",
                  "kind": "ready.lifecycle",
                  "deterministic": false,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "effects": []
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready.lifecycle",
                  "statement": "Unity is ready for execution.",
                  "subject": {},
                  "evidence": [],
                  "residualRisks": []
                }
              ],
              "reports": {},
              "residualRisks": [],
              "target": "execution",
              "requestedMode": "auto",
              "resolvedMode": "oneshot",
              "sessionKind": "transientProbe",
              "timeoutMilliseconds": 10000,
              "lifecycle": null,
              "readIndex": null
            }
            """);

        var errors = schemaSet.Validate(
            "cli-output/payload/ready.schema.json",
            document.RootElement);

        Assert.NotEmpty(errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutePayloadSchema_AcceptsContractViolations ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(
            """
            {
              "requestId": "req-1",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "opId": "step-1",
                  "operation": "ucli.project.refresh",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "indeterminate"
                }
              ]
            }
            """);

        var errors = schemaSet.Validate(
            "cli-output/payload/call.schema.json",
            document.RootElement);

        Assert.Empty(errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutePayloadSchema_AcceptsPostReadSource ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(
            """
            {
              "requestId": "req-1",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "edit-1",
                  "op": "edit",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [],
                  "diagnostics": []
                }
              ],
              "postReadSource": {
                "schemaVersion": 1,
                "steps": [
                  {
                    "opId": "edit-1",
                    "sourceKind": "edit",
                    "playModeMutation": false,
                    "commit": "context",
                    "persistenceExpected": true,
                    "expectedPostState": "deterministic"
                  }
                ]
              }
            }
            """);

        var errors = schemaSet.Validate(
            "cli-output/payload/call.schema.json",
            document.RootElement);

        Assert.Empty(errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutePayloadSchema_RejectsUnknownContractViolationApplicationState ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(
            """
            {
              "requestId": "req-1",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "opId": "step-1",
                  "operation": "ucli.project.refresh",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "maybeApplied"
                }
              ]
            }
            """);

        var errors = schemaSet.Validate(
            "cli-output/payload/call.schema.json",
            document.RootElement);

        Assert.NotEmpty(errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CallPayloadSchema_RejectsUnknownNestedPlanContractViolationApplicationState ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(
            """
            {
              "requestId": "req-1",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "plan": {
                "requestId": "req-1",
                "project": {
                  "projectPath": "/repo/UnityProject",
                  "projectFingerprint": "project-fingerprint",
                  "unityVersion": "6000.1.4f1"
                },
                "opResults": [],
                "contractViolations": [
                  {
                    "opId": "step-1",
                    "operation": "ucli.project.refresh",
                    "expectedFact": "assurance.mayDirty=false",
                    "observedResult": "opResults[].changed=true",
                    "applicationState": "maybeApplied"
                  }
                ]
              }
            }
            """);

        var errors = schemaSet.Validate(
            "cli-output/payload/call.schema.json",
            document.RootElement);

        Assert.NotEmpty(errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OpsDescribePayloadSchema_RestrictsPublicPlanModeEnum ()
    {
        var schemaPath = Path.Combine(
            RepositoryRoot,
            "schemas",
            "v1",
            "cli-output",
            "payload",
            "ops.describe.schema.json");

        var schemaText = File.ReadAllText(schemaPath);
        Assert.DoesNotContain("mayCreatePreviewState", schemaText, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(schemaText);
        var planModeEnum = document.RootElement
            .GetProperty("properties")
            .GetProperty("operation")
            .GetProperty("properties")
            .GetProperty("assurance")
            .GetProperty("properties")
            .GetProperty("planMode")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(static value => value.GetString())
            .ToArray();

        Assert.Contains("validationOnly", planModeEnum);
        Assert.Contains("observesLiveUnity", planModeEnum);
        Assert.DoesNotContain("mayCreatePreviewState", planModeEnum);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OpsDescribePayloadSchema_AcceptsVariantInputsAndClosedConstraintParameters ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(CreateOpsDescribePayload());

        var errors = schemaSet.Validate(
            "cli-output/payload/ops.describe.schema.json",
            document.RootElement);

        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(GetInvalidOpsDescribePayloadCases))]
    [Trait("Size", "Small")]
    public void OpsDescribePayloadSchema_RejectsNonPublicFreezeFields (
        string caseName,
        string payloadJson)
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(payloadJson);

        var errors = schemaSet.Validate(
            "cli-output/payload/ops.describe.schema.json",
            document.RootElement);

        Assert.True(errors.Count > 0, caseName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OpsDescribePayloadSchema_AcceptsDocumentedFullExamples ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        var examples = ReadOpsDescribeDocumentationPayloadExamples();

        Assert.NotEmpty(examples);
        foreach (var example in examples)
        {
            using var document = JsonDocument.Parse(example);

            var errors = schemaSet.Validate(
                "cli-output/payload/ops.describe.schema.json",
                document.RootElement);

            Assert.Empty(errors);
        }
    }

    public static IEnumerable<object[]> GetCliOutputGoldenFiles ()
    {
        var goldenRoot = Path.Combine(RepositoryRoot, "tests", "Ucli.Tests", "GoldenFiles", "Json", "CliOutput");
        return Directory
            .EnumerateFiles(goldenRoot, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new object[]
            {
                Path.GetRelativePath(RepositoryRoot, path),
            });
    }

    public static IEnumerable<object[]> GetReportRefContractCases ()
    {
        yield return new object[]
        {
            """
            {
              "kind": "log",
              "path": "artifacts/ready.log"
            }
            """,
            true,
        };
        yield return new object[]
        {
            """
            {
              "kind": "report",
              "uri": "https://example.test/report"
            }
            """,
            true,
        };
        yield return new object[]
        {
            """
            {
              "kind": "report",
              "digest": "sha256:abc"
            }
            """,
            false,
        };
        yield return new object[]
        {
            """
            {
              "kind": "report",
              "path": "artifacts/ready.log",
              "uri": "https://example.test/report"
            }
            """,
            false,
        };
    }

    public static IEnumerable<object[]> GetInvalidOpsDescribePayloadCases ()
    {
        yield return new object[]
        {
            "public planMode must not allow preview state",
            CreateOpsDescribePayload(planMode: "mayCreatePreviewState"),
        };
        yield return new object[]
        {
            "policyDerivation must not be public",
            CreateOpsDescribePayload(operationExtra: ",\"policyDerivation\":{}"),
        };
        yield return new object[]
        {
            "policyRestriction must not be public",
            CreateOpsDescribePayload(operationExtra: ",\"policyRestriction\":\"advancedByOverride\""),
        };
        yield return new object[]
        {
            "exposure must not be public",
            CreateOpsDescribePayload(operationExtra: ",\"exposure\":\"public\""),
        };
        yield return new object[]
        {
            "policyReason must not be public",
            CreateOpsDescribePayload(operationExtra: ",\"policyReason\":\"exposureNotPublic\""),
        };
        yield return new object[]
        {
            "unknown constraint parameter must not be public",
            CreateOpsDescribePayload(constraintExtra: ",\"unknownParameter\":true"),
        };
    }

    private static void AssertSchemaValid (
        IReadOnlyList<string> errors,
        string repositoryRelativeGoldenPath)
    {
        Assert.True(
            errors.Count == 0,
            $"Schema validation failed for {repositoryRelativeGoldenPath}:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    private static string CreateOpsDescribePayload (
        string planMode = "observesLiveUnity",
        string operationExtra = "",
        string constraintExtra = "")
    {
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
                      {
                        "kind": "referenceResolvable",
                        "targetKind": "gameObject"{{constraintExtra}}
                      }
                    ],
                    "argsPath": "$.target",
                    "variants": [
                      {
                        "name": "byGlobalObjectId",
                        "description": "Use resolved Unity GlobalObjectId.",
                        "fields": [
                          {
                            "name": "globalObjectId",
                            "argsPath": "$.target.globalObjectId",
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

    private static IReadOnlyList<string> ReadOpsDescribeDocumentationPayloadExamples ()
    {
        var propertyReferencePath = Path.Combine(RepositoryRoot, "docs", "uCLI-property-reference.md");
        var text = File.ReadAllText(propertyReferencePath);
        var examples = new List<string>();
        foreach (Match match in Regex.Matches(text, "```json\\s*(?<json>.*?)\\s*```", RegexOptions.Singleline))
        {
            var json = match.Groups["json"].Value.Trim();
            if (!json.Contains("\"operation\"", StringComparison.Ordinal)
                || !json.Contains("\"readIndex\"", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("operation", out _)
                && root.TryGetProperty("readIndex", out _))
            {
                examples.Add(json);
            }
        }

        return examples;
    }

    private static string FindRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ucli.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test base directory.");
    }
}
