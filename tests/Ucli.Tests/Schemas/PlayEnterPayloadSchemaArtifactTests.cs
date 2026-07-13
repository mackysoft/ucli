using System.Text.Json;
using static MackySoft.Ucli.Tests.Schemas.PlayPayloadSchemaTestSupport;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class PlayEnterPayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void PlayEnterPayloadSchema_ValidatesSuccessAndFailureTransitionShapes ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var successDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "alreadyEntered",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}}
            }
            """));
        using var timeoutDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "timeout",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(playModeState: "entering", playModeTransition: "entering", isPlayingOrWillChangePlaymode: true)}},
              "applicationState": "indeterminate"
            }
            """));
        using var blockedDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "blocked",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(lifecycleState: "compiling", blockingReasonJson: "\"compile\"", canAcceptExecutionRequests: false)}},
              "applicationState": "notApplied"
            }
            """,
            lifecycleState: "compiling",
            blockingReasonJson: "\"compile\"",
            canAcceptExecutionRequests: false,
            playModeState: "stopped",
            playModeTransition: "none",
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false));
        using var successWithoutAfterDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "entered",
              "before": {{CreatePlayLifecycleSnapshotJson()}}
            }
            """));
        using var timeoutWithoutObservedDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "timeout",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "applicationState": "indeterminate"
            }
            """));
        using var successWithErrorFieldsDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "entered",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson()}},
              "applicationState": "notApplied"
            }
            """));
        using var timeoutWithAfterDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "timeout",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(playModeState: "entering", playModeTransition: "entering", isPlayingOrWillChangePlaymode: true)}},
              "applicationState": "indeterminate"
            }
            """));
        using var timeoutWithNotAppliedApplicationStateDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "timeout",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(playModeState: "entering", playModeTransition: "entering", isPlayingOrWillChangePlaymode: true)}},
              "applicationState": "notApplied"
            }
            """));
        using var successWithStoppedAfterDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "entered",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayLifecycleSnapshotJson()}}
            }
            """));
        using var alreadyEnteredWithStoppedBeforeDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "alreadyEntered",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}}
            }
            """));
        using var successWithStoppedTopLevelDocument = JsonDocument.Parse(CreatePlayEnterPayloadJson(
            $$"""
            {
              "transition": "enter",
              "result": "entered",
              "before": {{CreatePlayLifecycleSnapshotJson()}},
              "after": {{CreatePlayingPlayLifecycleSnapshotJson()}}
            }
            """,
            lifecycleState: "ready",
            blockingReasonJson: "null",
            canAcceptExecutionRequests: true,
            playModeState: "stopped",
            playModeTransition: "none",
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false));
        using var enterWithOpResultsDocument = JsonDocument.Parse(
            $$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{SampleProjectFingerprint.ToString()}}",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "playmode",
              "blockingReason": "playMode",
              "compileState": "ready",
              "generations": {
                "compileGeneration": 12,
                "domainReloadGeneration": 7,
                "assetRefreshGeneration": 5,
                "playModeGeneration": 43
              },
              "canAcceptExecutionRequests": false,
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "playing",
                "transition": "none",
                "isPlaying": true,
                "isPlayingOrWillChangePlaymode": true
              },
              "transition": {
                "transition": "enter",
                "result": "entered",
                "before": {{CreatePlayLifecycleSnapshotJson()}},
                "after": {{CreatePlayingPlayLifecycleSnapshotJson()}}
              },
              "timeoutMilliseconds": 1000,
              "opResults": []
            }
            """);

        Assert.Empty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", successDocument.RootElement));
        Assert.Empty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", timeoutDocument.RootElement));
        Assert.Empty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", blockedDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", successWithoutAfterDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", timeoutWithoutObservedDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", successWithErrorFieldsDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", timeoutWithAfterDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", timeoutWithNotAppliedApplicationStateDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", successWithStoppedAfterDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", alreadyEnteredWithStoppedBeforeDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", successWithStoppedTopLevelDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/play.enter.schema.json", enterWithOpResultsDocument.RootElement));
    }
}
