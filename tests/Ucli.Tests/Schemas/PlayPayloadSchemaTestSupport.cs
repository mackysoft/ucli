using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

internal static class PlayPayloadSchemaTestSupport
{
    public static readonly ProjectFingerprint SampleProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static string CreatePlayLifecycleSnapshotJson (
        string playModeState = "stopped",
        string playModeTransition = "none",
        string primaryDiagnosticJson = "null",
        string lifecycleState = "ready",
        string blockingReasonJson = "null",
        bool canAcceptExecutionRequests = true,
        bool isPlaying = false,
        bool isPlayingOrWillChangePlaymode = false,
        long playModeGeneration = 42)
    {
        return $$"""
            {
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "unityVersion": "6000.1.4f1",
              "projectFingerprint": "{{SampleProjectFingerprint.ToString()}}",
              "lifecycleState": "{{lifecycleState}}",
              "blockingReason": {{blockingReasonJson}},
              "compileState": "ready",
              "generations": {{CreateUnityGenerationSnapshotJson(playModeGeneration)}},
              "canAcceptExecutionRequests": {{JsonSerializer.Serialize(canAcceptExecutionRequests)}},
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": {{primaryDiagnosticJson}},
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": {{JsonSerializer.Serialize(isPlaying)}},
                "isPlayingOrWillChangePlaymode": {{JsonSerializer.Serialize(isPlayingOrWillChangePlaymode)}}
              }
            }
            """;
    }

    public static string CreatePlayingPlayLifecycleSnapshotJson ()
    {
        return CreatePlayLifecycleSnapshotJson(
            playModeState: "playing",
            playModeTransition: "none",
            lifecycleState: "playmode",
            blockingReasonJson: "\"playMode\"",
            canAcceptExecutionRequests: false,
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            playModeGeneration: 43);
    }

    public static string CreateReadyStoppedPlayLifecycleSnapshotJson ()
    {
        return CreatePlayLifecycleSnapshotJson(playModeGeneration: 44);
    }

    public static string CreateCompilingStoppedPlayLifecycleSnapshotJson ()
    {
        return CreatePlayLifecycleSnapshotJson(
            lifecycleState: "compiling",
            blockingReasonJson: "\"compile\"",
            canAcceptExecutionRequests: false,
            playModeGeneration: 44);
    }

    public static string CreatePlayEnterPayloadJson (
        string transitionJson,
        string lifecycleState = "playmode",
        string blockingReasonJson = "\"playMode\"",
        bool canAcceptExecutionRequests = false,
        string playModeState = "playing",
        string playModeTransition = "none",
        bool isPlaying = true,
        bool isPlayingOrWillChangePlaymode = true)
    {
        return $$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{SampleProjectFingerprint.ToString()}}",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "{{lifecycleState}}",
              "blockingReason": {{blockingReasonJson}},
              "compileState": "ready",
              "generations": {{CreateUnityGenerationSnapshotJson(playModeGeneration: 43)}},
              "canAcceptExecutionRequests": {{JsonSerializer.Serialize(canAcceptExecutionRequests)}},
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": {{JsonSerializer.Serialize(isPlaying)}},
                "isPlayingOrWillChangePlaymode": {{JsonSerializer.Serialize(isPlayingOrWillChangePlaymode)}}
              },
              "transition": {{transitionJson}},
              "timeoutMilliseconds": 1000
            }
            """;
    }

    public static string CreatePlayExitPayloadJson (
        string transitionJson,
        string lifecycleState = "ready",
        string blockingReasonJson = "null",
        bool canAcceptExecutionRequests = true,
        string playModeState = "stopped",
        string playModeTransition = "none",
        bool isPlaying = false,
        bool isPlayingOrWillChangePlaymode = false,
        long playModeGeneration = 44)
    {
        return $$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{SampleProjectFingerprint.ToString()}}",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "{{lifecycleState}}",
              "blockingReason": {{blockingReasonJson}},
              "compileState": "ready",
              "generations": {{CreateUnityGenerationSnapshotJson(playModeGeneration)}},
              "canAcceptExecutionRequests": {{JsonSerializer.Serialize(canAcceptExecutionRequests)}},
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": {{JsonSerializer.Serialize(isPlaying)}},
                "isPlayingOrWillChangePlaymode": {{JsonSerializer.Serialize(isPlayingOrWillChangePlaymode)}}
              },
              "transition": {{transitionJson}},
              "timeoutMilliseconds": 1000
            }
            """;
    }

    public static string CreatePlayStatusPayloadJson (
        string playModeState = "stopped",
        string playModeTransition = "none",
        string primaryDiagnosticJson = "null")
    {
        return $$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{SampleProjectFingerprint.ToString()}}",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "ready",
              "blockingReason": null,
              "compileState": "ready",
              "generations": {{CreateUnityGenerationSnapshotJson(playModeGeneration: 42)}},
              "canAcceptExecutionRequests": true,
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": {{primaryDiagnosticJson}},
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": false,
                "isPlayingOrWillChangePlaymode": false
              },
              "timeoutMilliseconds": 1000
            }
            """;
    }

    private static string CreateUnityGenerationSnapshotJson (long playModeGeneration)
    {
        return $$"""
            {
              "compileGeneration": 12,
              "domainReloadGeneration": 7,
              "assetRefreshGeneration": 5,
              "playModeGeneration": {{playModeGeneration}}
            }
            """;
    }
}
