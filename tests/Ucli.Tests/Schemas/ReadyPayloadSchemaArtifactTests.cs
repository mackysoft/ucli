using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class ReadyPayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void ReadyPayloadSchema_RequiresClaimValidity ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        var projectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");
        using var document = JsonDocument.Parse(
            $$"""
            {
              "verdict": "pass",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{projectFingerprint.ToString()}}",
                "unityVersion": "6000.1.4f1"
              },
              "verifiers": [
                {
                  "id": "ready.lifecycle",
                  "kind": "ready",
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
}
