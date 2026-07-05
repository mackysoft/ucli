using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class ExecutePayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void ExecutePayloadSchema_AcceptsContractViolations ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
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
    [Trait("Size", "Medium")]
    public void ExecutePayloadSchema_AcceptsPostReadSource ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
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
    [Trait("Size", "Medium")]
    public void ExecutePayloadSchema_RejectsUnknownContractViolationApplicationState ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
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
    [Trait("Size", "Medium")]
    public void CallPayloadSchema_RejectsUnknownNestedPlanContractViolationApplicationState ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
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
}
