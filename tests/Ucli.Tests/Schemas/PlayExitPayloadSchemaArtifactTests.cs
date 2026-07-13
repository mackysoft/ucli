using System.Text.Json;
using static MackySoft.Ucli.Tests.Schemas.PlayPayloadSchemaTestSupport;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class PlayExitPayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void PlayExitPayloadSchema_ValidatesSuccessAndFailureTransitionShapes ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var successDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "exited",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreateReadyStoppedPlayLifecycleSnapshotJson()}}
            }
            """));
        using var timeoutDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "timeout",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(playModeState: "exiting", playModeTransition: "exiting", lifecycleState: "playmode", blockingReasonJson: "\"playMode\"", canAcceptExecutionRequests: false, isPlaying: true, isPlayingOrWillChangePlaymode: true)}},
              "applicationState": "indeterminate"
            }
            """,
            lifecycleState: "playmode",
            blockingReasonJson: "\"playMode\"",
            canAcceptExecutionRequests: false,
            playModeState: "exiting",
            playModeTransition: "exiting",
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            playModeGeneration: 42));
        using var blockedDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "blocked",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(lifecycleState: "safeMode", blockingReasonJson: "\"safeMode\"", canAcceptExecutionRequests: false, playModeGeneration: 44)}},
              "applicationState": "applied"
            }
            """,
            lifecycleState: "safeMode",
            blockingReasonJson: "\"safeMode\"",
            canAcceptExecutionRequests: false));
        using var successWithoutAfterDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "exited",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}}
            }
            """));
        using var timeoutWithoutObservedDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "timeout",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "applicationState": "indeterminate"
            }
            """,
            lifecycleState: "playmode",
            blockingReasonJson: "\"playMode\"",
            canAcceptExecutionRequests: false,
            playModeState: "exiting",
            playModeTransition: "exiting",
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            playModeGeneration: 42));
        using var successWithErrorFieldsDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "exited",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreateReadyStoppedPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "applicationState": "notApplied"
            }
            """));
        using var timeoutWithAfterDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "timeout",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreateReadyStoppedPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(playModeState: "exiting", playModeTransition: "exiting", lifecycleState: "playmode", blockingReasonJson: "\"playMode\"", canAcceptExecutionRequests: false, isPlaying: true, isPlayingOrWillChangePlaymode: true)}},
              "applicationState": "indeterminate"
            }
            """,
            lifecycleState: "playmode",
            blockingReasonJson: "\"playMode\"",
            canAcceptExecutionRequests: false,
            playModeState: "exiting",
            playModeTransition: "exiting",
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            playModeGeneration: 42));
        using var timeoutWithNotAppliedApplicationStateDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "timeout",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(playModeState: "exiting", playModeTransition: "exiting", lifecycleState: "playmode", blockingReasonJson: "\"playMode\"", canAcceptExecutionRequests: false, isPlaying: true, isPlayingOrWillChangePlaymode: true)}},
              "applicationState": "notApplied"
            }
            """,
            lifecycleState: "playmode",
            blockingReasonJson: "\"playMode\"",
            canAcceptExecutionRequests: false,
            playModeState: "exiting",
            playModeTransition: "exiting",
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            playModeGeneration: 42));
        using var successWithPlayingAfterDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "exited",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}}
            }
            """));
        using var alreadyExitedWithPlayingBeforeDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "alreadyExited",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreateReadyStoppedPlayLifecycleSnapshotJson()}}
            }
            """));
        using var successWithPlayingTopLevelDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "exited",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreateReadyStoppedPlayLifecycleSnapshotJson()}}
            }
            """,
            lifecycleState: "playmode",
            blockingReasonJson: "\"playMode\"",
            canAcceptExecutionRequests: false,
            playModeState: "playing",
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            playModeGeneration: 43));
        using var exitWithOpResultsDocument = JsonDocument.Parse(
            $$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "ready",
              "blockingReason": null,
              "compileState": "ready",
              "generations": {
                "compileGeneration": 12,
                "domainReloadGeneration": 7,
                "assetRefreshGeneration": 5,
                "playModeGeneration": 44
              },
              "canAcceptExecutionRequests": true,
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "stopped",
                "transition": "none",
                "isPlaying": false,
                "isPlayingOrWillChangePlaymode": false
              },
              "transition": {
                "transition": "exit",
                "result": "exited",
                "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
                "after": {{CreateReadyStoppedPlayLifecycleSnapshotJson()}}
              },
              "timeoutMilliseconds": 1000,
              "opResults": []
            }
            """);

        Assert.Empty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", successDocument.RootElement));
        Assert.Empty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", timeoutDocument.RootElement));
        Assert.Empty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", blockedDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", successWithoutAfterDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", timeoutWithoutObservedDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", successWithErrorFieldsDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", timeoutWithAfterDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", timeoutWithNotAppliedApplicationStateDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", successWithPlayingAfterDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", alreadyExitedWithPlayingBeforeDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", successWithPlayingTopLevelDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.exit.schema.json", exitWithOpResultsDocument.RootElement));
    }
}
