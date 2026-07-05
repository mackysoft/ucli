using System.Text.Json;
using static MackySoft.Ucli.Tests.Schemas.PlayPayloadSchemaTestSupport;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class PlayPayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void PlayPayloadSchemas_AcceptLifecycleAndTransitionContracts ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var statusDocument = JsonDocument.Parse(CreatePlayStatusPayloadJson());
        using var enterDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "entered",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}}
            }
            """));
        using var exitDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "exited",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreateReadyStoppedPlayLifecycleSnapshotJson()}}
            }
            """));
        using var alreadyExitedDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "alreadyExited",
              "before": {{CreateCompilingStoppedPlayLifecycleSnapshotJson()}},
              "after": {{CreateCompilingStoppedPlayLifecycleSnapshotJson()}}
            }
            """,
            lifecycleState: "compiling",
            blockingReasonJson: "\"compile\"",
            canAcceptExecutionRequests: false));

        Assert.Empty(schemaSet.Validate("cli-output/payload/play.status.schema.json", statusDocument.RootElement));
        Assert.Empty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", enterDocument.RootElement));
        Assert.Empty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", exitDocument.RootElement));
        Assert.Empty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", alreadyExitedDocument.RootElement));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PlayTransitionPayloadSchemas_RejectMismatchedLeafContracts ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var enterWithExitTransitionDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "exited",
              "before": {{CreatePlayLifecycleSnapshotJson()}}
            }
            """));
        using var enterWithUnknownTransitionFieldDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "entered",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "target": "entered"
            }
            """));

        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", enterWithExitTransitionDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", enterWithUnknownTransitionFieldDocument.RootElement));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PlayLifecycleSnapshotSchemas_ValidatePrimaryDiagnosticContract ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var validDiagnosticDocument = JsonDocument.Parse(CreatePlayStatusPayloadJson(primaryDiagnosticJson: """
            {
              "kind": "compiler.error",
              "code": "CS1002",
              "file": "Assets/Scripts/Example.cs",
              "line": 12,
              "column": 8,
              "message": "Expected semicolon."
            }
            """));
        using var invalidDiagnosticTypeDocument = JsonDocument.Parse(CreatePlayStatusPayloadJson(primaryDiagnosticJson: """
            {
              "kind": "compiler.error",
              "line": "12"
            }
            """));
        using var invalidDiagnosticPropertyDocument = JsonDocument.Parse(CreatePlayStatusPayloadJson(primaryDiagnosticJson: """
            {
              "kind": "compiler.error",
              "unexpected": true
            }
            """));

        Assert.Empty(schemaSet.Validate("cli-output/payload/play.status.schema.json", validDiagnosticDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.status.schema.json", invalidDiagnosticTypeDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.status.schema.json", invalidDiagnosticPropertyDocument.RootElement));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PlayStatusPayloadSchema_RejectsMissingRequiredFlatFields ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var missingProjectDocument = JsonDocument.Parse(
            """
            {
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "ready",
              "blockingReason": null,
              "compileState": "idle",
              "compileGeneration": "12",
              "domainReloadGeneration": "7",
              "canAcceptExecutionRequests": true,
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "stopped",
                "transition": "none",
                "isPlaying": false,
                "isPlayingOrWillChangePlaymode": false,
                "generation": "42"
              },
              "timeoutMilliseconds": 1000
            }
            """);
        using var legacyNestedSnapshotDocument = JsonDocument.Parse(
            $$"""
            {
              "snapshot": {{CreatePlayLifecycleSnapshotJson()}},
              "timeoutMilliseconds": 1000
            }
            """);

        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.status.schema.json", missingProjectDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.status.schema.json", legacyNestedSnapshotDocument.RootElement));
    }
}
