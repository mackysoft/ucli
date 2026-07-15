using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class ExecutePayloadSchemaArtifactTests
{
    private static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("9B0E6D1E-3F55-4A6B-8C66-5B9A3A7C9C62")]
    [InlineData("request-1")]
    [Trait("Size", "Medium")]
    public void ExecutePayloadSchema_RejectsNonCanonicalOrEmptyRequestId (string requestId)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var document = JsonDocument.Parse(
            $$"""
            {
              "requestId": "{{requestId}}"
            }
            """);

        var errors = schemaSet.Validate(
            "cli-output/payload/call.schema.json",
            document.RootElement);

        Assert.NotEmpty(errors);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void ExecutePayloadSchema_AcceptsContractViolations ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var document = JsonDocument.Parse(
            $$"""
            {
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{ProjectFingerprint.ToString()}}",
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
            $$"""
            {
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{ProjectFingerprint.ToString()}}",
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

    [Theory]
    [InlineData("""{ "kind": "unknown", "path": "Assets/Example.asset", "assetGuid": null }""")]
    [InlineData("""{ "kind": "asset", "assetGuid": null }""")]
    [InlineData("""{ "kind": "asset", "path": "Assets/../Example.asset", "assetGuid": null }""")]
    [InlineData("""{ "kind": "asset", "path": "Assets/Example.asset", "assetGuid": "00000000-0000-0000-0000-000000000000" }""")]
    [Trait("Size", "Medium")]
    public void ExecutePayloadSchema_RejectsInvalidTouchedResource (string touchedResourceJson)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var document = JsonDocument.Parse(
            $$"""
            {
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{ProjectFingerprint}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "step-1",
                  "op": "ucli.project.refresh",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [{{touchedResourceJson}}],
                  "diagnostics": []
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
    public void ExecutePayloadSchema_RejectsUnknownContractViolationApplicationState ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var document = JsonDocument.Parse(
            $$"""
            {
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{ProjectFingerprint.ToString()}}",
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
            $$"""
            {
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{ProjectFingerprint.ToString()}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "plan": {
                "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                "project": {
                  "projectPath": "/repo/UnityProject",
                  "projectFingerprint": "{{ProjectFingerprint.ToString()}}",
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
