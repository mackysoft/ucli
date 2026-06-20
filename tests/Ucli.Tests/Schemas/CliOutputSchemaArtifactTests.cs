using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

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
        Assert.Contains("build.run", commandEntries.Keys);
        Assert.Contains("verify", commandEntries.Keys);
        Assert.Contains("plan", commandEntries.Keys);
        Assert.Contains("call", commandEntries.Keys);
        Assert.Contains("eval", commandEntries.Keys);
        Assert.Contains("ops.describe", commandEntries.Keys);
        Assert.Contains("codes.describe", commandEntries.Keys);
        Assert.Contains("play.status", commandEntries.Keys);
        Assert.Contains("play.enter", commandEntries.Keys);
        Assert.Contains("play.exit", commandEntries.Keys);
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

        var payload = root.GetProperty("payload");
        if (ShouldValidateCommandPayloadSchema(root, payload))
        {
            AssertSchemaValid(
                schemaSet.Validate(payloadSchemaPath!, payload, "$.payload"),
                repositoryRelativeGoldenPath);
        }

        if (string.Equals(command, UcliCommandIds.OpsDescribe.Name, StringComparison.Ordinal)
            && payload.TryGetProperty("operation", out var operation))
        {
            AssertOpsDescribeSchemasUseSupportedSubset(operation);
        }
    }

    [Theory]
    [MemberData(nameof(GetReportRefContractCases))]
    [Trait("Size", "Small")]
    public void ReportRefSchema_RequiresExactlyOneLocation (
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
    public void PlayPayloadSchemas_AcceptLifecycleAndTransitionContracts ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
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
    [Trait("Size", "Small")]
    public void PlayTransitionPayloadSchemas_RejectMismatchedLeafContracts ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
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
    [Trait("Size", "Small")]
    public void PlayEnterPayloadSchema_ValidatesSuccessAndFailureTransitionShapes ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
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
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "playmode",
              "blockingReason": "playMode",
              "compileState": "idle",
              "compileGeneration": "12",
              "domainReloadGeneration": "7",
              "canAcceptExecutionRequests": false,
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "playing",
                "transition": "none",
                "isPlaying": true,
                "isPlayingOrWillChangePlaymode": true,
                "generation": "43"
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

    [Fact]
    [Trait("Size", "Small")]
    public void PlayExitPayloadSchema_ValidatesSuccessAndFailureTransitionShapes ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
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
            generation: "42"));
        using var blockedDocument = JsonDocument.Parse(CreatePlayExitPayloadJson(
            $$"""
            {
              "transition": "exit",
              "result": "blocked",
              "before": {{CreatePlayingPlayLifecycleSnapshotJson()}},
              "observed": {{CreatePlayLifecycleSnapshotJson(lifecycleState: "safeMode", blockingReasonJson: "\"safeMode\"", canAcceptExecutionRequests: false, generation: "44")}},
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
            generation: "42"));
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
            generation: "42"));
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
            generation: "42"));
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
            generation: "43"));
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
                "generation": "44"
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

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleSnapshotSchemas_ValidatePrimaryDiagnosticContract ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
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
    [Trait("Size", "Small")]
    public void PlayStatusPayloadSchema_RejectsMissingRequiredFlatFields ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
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

    [Fact]
    [Trait("Size", "Small")]
    public void RequestSchemasAndPrimitiveOperationNames_DoNotExposePlayModeLifecycleOperations ()
    {
        var requestEnvelopeSchemaPath = Path.Combine(RepositoryRoot, "schemas", "v1", "request", "request-envelope.schema.json");
        var editDslSchemaPath = Path.Combine(RepositoryRoot, "schemas", "v1", "request", "edit-dsl.schema.json");
        var requestSchemas = File.ReadAllText(requestEnvelopeSchemaPath) + File.ReadAllText(editDslSchemaPath);
        var primitiveOperationNames = StaticFieldValueReader.ReadFromStaticClasses<string>(
            typeof(UcliPrimitiveOperationNames).Assembly,
            "PrimitiveOperationNames");

        Assert.DoesNotContain("play.enter", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("play.exit", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("ucli.play.enter", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("ucli.play.exit", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("ucli.play.enter", primitiveOperationNames);
        Assert.DoesNotContain("ucli.play.exit", primitiveOperationNames);
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
              "path": "artifacts/ready.log",
              "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            }
            """,
            true,
        };
        yield return new object[]
        {
            """
            {
              "uri": "https://example.test/report"
            }
            """,
            true,
        };
        yield return new object[]
        {
            """
            {
              "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            }
            """,
            false,
        };
        yield return new object[]
        {
            """
            {
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
        yield return new object[]
        {
            "asset constraint must require assetKind",
            CreateOpsDescribePayload(targetConstraintJson: """{"kind":"assetExists"}"""),
        };
        yield return new object[]
        {
            "nonEmpty constraint must not allow range parameter",
            CreateOpsDescribePayload(targetConstraintJson: """{"kind":"nonEmpty","min":1}"""),
        };
        yield return new object[]
        {
            "range constraint must require a bound",
            CreateOpsDescribePayload(targetConstraintJson: """{"kind":"range"}"""),
        };
        yield return new object[]
        {
            "cursor constraint must not allow serialized property access",
            CreateOpsDescribePayload(targetConstraintJson: """{"kind":"cursor","access":"write"}"""),
        };
        yield return new object[]
        {
            "input argsPath must not expose request-local alias root branch",
            CreateOpsDescribePayload(inputArgsPath: $"$.{UcliOperationContractPropertyNames.Alias}"),
        };
        yield return new object[]
        {
            "input argsPath must not expose request-local alias nested branch",
            CreateOpsDescribePayload(inputArgsPath: $"$.target.{UcliOperationContractPropertyNames.Alias}"),
        };
        yield return new object[]
        {
            "variant field argsPath must not expose request-local alias branch",
            CreateOpsDescribePayload(fieldArgsPath: $"$.target.{UcliOperationContractPropertyNames.Alias}"),
        };
        yield return new object[]
        {
            "variant field argsPath must use uCLI args path syntax",
            CreateOpsDescribePayload(fieldArgsPath: "$.target[0].globalObjectId"),
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

    private static bool ShouldValidateCommandPayloadSchema (JsonElement root, JsonElement payload)
    {
        if (!string.Equals(root.GetProperty("status").GetString(), "error", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var _ in payload.EnumerateObject())
        {
            return true;
        }

        return false;
    }

    private static string CreateOpsDescribePayload (
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

    private static string CreatePlayLifecycleSnapshotJson (
        string playModeState = "stopped",
        string playModeTransition = "none",
        string primaryDiagnosticJson = "null",
        string lifecycleState = "ready",
        string blockingReasonJson = "null",
        bool canAcceptExecutionRequests = true,
        bool isPlaying = false,
        bool isPlayingOrWillChangePlaymode = false,
        string generation = "42")
    {
        return $$"""
            {
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "unityVersion": "6000.1.4f1",
              "projectFingerprint": "project-fingerprint",
              "lifecycleState": "{{lifecycleState}}",
              "blockingReason": {{blockingReasonJson}},
              "compileState": "idle",
              "compileGeneration": "12",
              "domainReloadGeneration": "7",
              "canAcceptExecutionRequests": {{JsonSerializer.Serialize(canAcceptExecutionRequests)}},
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": {{primaryDiagnosticJson}},
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": {{JsonSerializer.Serialize(isPlaying)}},
                "isPlayingOrWillChangePlaymode": {{JsonSerializer.Serialize(isPlayingOrWillChangePlaymode)}},
                "generation": "{{generation}}"
              }
            }
            """;
    }

    private static string CreatePlayingPlayLifecycleSnapshotJson ()
    {
        return CreatePlayLifecycleSnapshotJson(
            playModeState: "playing",
            playModeTransition: "none",
            lifecycleState: "playmode",
            blockingReasonJson: "\"playMode\"",
            canAcceptExecutionRequests: false,
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            generation: "43");
    }

    private static string CreateReadyStoppedPlayLifecycleSnapshotJson ()
    {
        return CreatePlayLifecycleSnapshotJson(generation: "44");
    }

    private static string CreateCompilingStoppedPlayLifecycleSnapshotJson ()
    {
        return CreatePlayLifecycleSnapshotJson(
            lifecycleState: "compiling",
            blockingReasonJson: "\"compile\"",
            canAcceptExecutionRequests: false,
            generation: "44");
    }

    private static string CreatePlayEnterPayloadJson (
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
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "{{lifecycleState}}",
              "blockingReason": {{blockingReasonJson}},
              "compileState": "idle",
              "compileGeneration": "12",
              "domainReloadGeneration": "7",
              "canAcceptExecutionRequests": {{JsonSerializer.Serialize(canAcceptExecutionRequests)}},
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": {{JsonSerializer.Serialize(isPlaying)}},
                "isPlayingOrWillChangePlaymode": {{JsonSerializer.Serialize(isPlayingOrWillChangePlaymode)}},
                "generation": "43"
              },
              "transition": {{transitionJson}},
              "timeoutMilliseconds": 1000
            }
            """;
    }

    private static string CreatePlayExitPayloadJson (
        string transitionJson,
        string lifecycleState = "ready",
        string blockingReasonJson = "null",
        bool canAcceptExecutionRequests = true,
        string playModeState = "stopped",
        string playModeTransition = "none",
        bool isPlaying = false,
        bool isPlayingOrWillChangePlaymode = false,
        string generation = "44")
    {
        return $$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "daemonStatus": "running",
              "serverVersion": "0.5.0",
              "editorMode": "gui",
              "lifecycleState": "{{lifecycleState}}",
              "blockingReason": {{blockingReasonJson}},
              "compileState": "idle",
              "compileGeneration": "12",
              "domainReloadGeneration": "7",
              "canAcceptExecutionRequests": {{JsonSerializer.Serialize(canAcceptExecutionRequests)}},
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": null,
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": {{JsonSerializer.Serialize(isPlaying)}},
                "isPlayingOrWillChangePlaymode": {{JsonSerializer.Serialize(isPlayingOrWillChangePlaymode)}},
                "generation": "{{generation}}"
              },
              "transition": {{transitionJson}},
              "timeoutMilliseconds": 1000
            }
            """;
    }

    private static string CreatePlayStatusPayloadJson (
        string playModeState = "stopped",
        string playModeTransition = "none",
        string primaryDiagnosticJson = "null")
    {
        return $$"""
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
              "compileState": "idle",
              "compileGeneration": "12",
              "domainReloadGeneration": "7",
              "canAcceptExecutionRequests": true,
              "observedAtUtc": "2026-05-21T00:00:00+00:00",
              "actionRequired": null,
              "primaryDiagnostic": {{primaryDiagnosticJson}},
              "playMode": {
                "state": "{{playModeState}}",
                "transition": "{{playModeTransition}}",
                "isPlaying": false,
                "isPlayingOrWillChangePlaymode": false,
                "generation": "42"
              },
              "timeoutMilliseconds": 1000
            }
            """;
    }

    private static void AssertOpsDescribeSchemasUseSupportedSubset (JsonElement operation)
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
