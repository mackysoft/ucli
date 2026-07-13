using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

internal static class VerifyServiceTestSupport
{
    public static VerifyService CreateService (
        string repositoryRoot,
        RecordingVerifyReadyService? readyService = null,
        RecordingVerifyCompileService? compileService = null,
        RecordingVerifyTestRunService? testRunService = null,
        RecordingVerifyLogsUnityService? logsService = null,
        StubVerifyProfileFileReader? profileFileReader = null,
        StubVerifyFromInputFileReader? fromInputFileReader = null,
        TimeProvider? timeProvider = null)
    {
        var project = ProjectIdentityInfoTestFactory.CreateForRepositoryRoot(repositoryRoot);
        return new VerifyService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create(
                unityProjectRoot: Path.Combine(repositoryRoot, "UnityProject"),
                repositoryRoot: repositoryRoot))),
            readyService ?? new RecordingVerifyReadyService(input => CreateReadyResult(input.Target, project)),
            compileService ?? new RecordingVerifyCompileService(_ => CreateCompileResult(project)),
            testRunService ?? new RecordingVerifyTestRunService(_ => TestRunServiceResult.Pass(
                "Tests passed.",
                "test-run-1",
                "/repo/.ucli/local/test/test-run-1",
                "/repo/.ucli/local/test/test-run-1/summary.json")),
            logsService ?? new RecordingVerifyLogsUnityService((_, _, _) => ValueTask.FromResult(LogsReadServiceResult.Success())),
            profileFileReader ?? new StubVerifyProfileFileReader((profilePath, root) => VerifyProfileFileReadResult.Success(
                File.ReadAllText(Path.Combine(root, profilePath)),
                profilePath.Replace('\\', '/'))),
            fromInputFileReader ?? new StubVerifyFromInputFileReader((fromPath, root) => VerifyFromInputFileReadResult.Success(
                File.ReadAllText(Path.Combine(root, fromPath)))),
            timeProvider);
    }

    public static void WriteRequiredPostReadProfile (TestDirectoryScope scope)
    {
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "steps": [
                {
                  "kind": "postRead",
                  "required": true
                }
              ]
            }
            """);
    }

    public static ReadyExecutionResult CreateReadyResult (
        ReadyTarget target,
        ProjectIdentityInfo project)
    {
        var claimCode = ReadyClaimCodes.ForTarget(target).Value;
        var verifierId = "ready.lifecycle";
        return ReadyExecutionResult.Success(new ReadyExecutionOutput(
            Verdict: ReadyVerdictValues.Pass,
            Project: project,
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: verifierId,
                    Kind: verifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [claimCode],
                    Effects: [])
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: claimCode,
                    Status: ReadyClaimStatusValues.Passed,
                    Coverage: ReadyCoverageValues.Full,
                    Required: true,
                    VerifierRef: verifierId,
                    Statement: "Unity is ready.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["target"] = ReadyTargetCodec.ToValue(target),
                    },
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKindValues.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence: [],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, ReadyReportOutput>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: ReadyTargetCodec.ToValue(target),
            RequestedMode: AssuranceExecutionModeCodec.Auto,
            ResolvedMode: AssuranceExecutionModeCodec.Oneshot,
            SessionKind: AssuranceSessionKindValues.TransientProbe,
            TimeoutMilliseconds: 10000));
    }

    public static CompileExecutionResult CreateCompileResult (ProjectIdentityInfo project)
    {
        return CreateCompileResult(project, CompileClaimStatusValues.Passed);
    }

    public static CompileExecutionResult CreateCompileResult (
        ProjectIdentityInfo project,
        string claimStatus)
    {
        var failed = string.Equals(claimStatus, CompileClaimStatusValues.Failed, StringComparison.Ordinal);
        return CompileExecutionResult.Success(new CompileExecutionOutput(
            Verdict: failed ? CompileVerdictValues.Fail : CompileVerdictValues.Pass,
            Project: project,
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: "compile",
                    Kind: "compile",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [CompileClaimCodes.UnityCompileNoErrors.Value],
                    Effects: CompileEffectValues.All,
                    ReportRef: "compile.summary")
            ],
            Claims:
            [
                new CompileClaimOutput(
                    Id: CompileClaimCodes.UnityCompileNoErrors.Value,
                    Status: claimStatus,
                    Coverage: CompileCoverageValues.Full,
                    Required: true,
                    VerifierRef: "compile",
                    Statement: "Unity script compilation has no errors.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal),
                    Evidence: [],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, CompileReportOutput>(StringComparer.Ordinal)
            {
                ["compile.summary"] = new CompileReportOutput("/repo/.ucli/local/compile/run-1/summary.json"),
            },
            ResidualRisks: [],
            RequestedMode: AssuranceExecutionModeCodec.Auto,
            ResolvedMode: AssuranceExecutionModeCodec.Oneshot,
            SessionKind: AssuranceSessionKindValues.TransientProbe,
            TimeoutMilliseconds: 10000,
            Compile: new CompileOutput(
                RunId: "compile-run-1",
                Refresh: new CompileRefreshOutput(
                    Origin: CompileEffectValues.AssetDatabaseRefresh,
                    Requested: true,
                    StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                    CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:01Z"),
                    Completed: true),
                ScriptCompilation: new CompileScriptCompilationOutput(
                    Started: true,
                    Completed: true,
                    CompileGenerationBefore: "1",
                    CompileGenerationAfter: "2",
                    Diagnostics: new CompileDiagnosticsOutput(
                        ErrorCount: failed ? 1 : 0,
                        WarningCount: 0,
                        PrimaryDiagnostic: null)),
                DomainReload: new CompileDomainReloadOutput(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: "1",
                    GenerationAfter: "1",
                    Settled: true),
                Lifecycle: new CompileLifecycleOutput(
                    ServerVersion: "0.5.0",
                    UnityVersion: "6000.1.4f1",
                    EditorMode: "batchmode",
                    LifecycleState: "ready",
                    BlockingReason: null,
                    CompileState: "ready",
                    CompileGeneration: "2",
                    DomainReloadGeneration: "1",
                    CanAcceptExecutionRequests: true,
                    ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                    ActionRequired: null,
                    PrimaryDiagnostic: null))));
    }

    public static string CreateFromJson (
        ProjectFingerprint projectFingerprint,
        string coverageImpact,
        string severity = "warning",
        bool applied = true,
        bool changed = true,
        string touchedJson = """
                  [
                    {
                      "kind": "asset",
                      "path": "Assets/Scene.unity"
                    }
                  ]
            """,
        string sourceKind = "edit",
        string? commit = "context",
        bool persistenceExpected = true,
        string expectedPostState = "deterministic",
        bool includeReadPostcondition = true,
        string op = "edit")
    {
        var projectFingerprintText = projectFingerprint.ToString();
        var commitJson = commit is null ? "null" : $"\"{commit}\"";
        var readPostconditionJson = includeReadPostcondition
            ? """
            ,
            "readPostcondition": {
              "requirements": [
                {
                  "surface": "sceneTreeLite",
                  "minSafeGeneratedAtUtc": "2026-05-17T00:00:00+00:00"
                }
              ]
            }
            """
            : string.Empty;
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "call",
          "payload": {
            "project": {
              "projectPath": "/repo/UnityProject",
              "projectFingerprint": "{{projectFingerprintText}}",
              "unityVersion": "6000.1.4f1"
            },
            "opResults": [
              {
                  "opId": "op-1",
                  "op": "{{op}}",
                  "phase": "call",
                  "applied": {{JsonSerializer.Serialize(applied)}},
                  "changed": {{JsonSerializer.Serialize(changed)}},
                "touched": {{touchedJson}},
                "diagnostics": [
                    {
                      "code": "READ_SURFACE_PARTIAL",
                      "severity": "{{severity}}",
                      "coverageImpact": "{{coverageImpact}}",
                      "message": "Read surface coverage is partial."
                  }
                ]
              }
            ],
            "postReadSource": {
              "schemaVersion": 1,
              "steps": [
                {
                  "opId": "op-1",
                  "sourceKind": "{{sourceKind}}",
                  "playModeMutation": false,
                  "commit": {{commitJson}},
                  "persistenceExpected": {{JsonSerializer.Serialize(persistenceExpected)}},
                  "expectedPostState": "{{expectedPostState}}"
                }
              ]
            }{{readPostconditionJson}}
          },
          "errors": []
        }
        """;
    }

    public static string CreateNoOpFromJson (ProjectFingerprint projectFingerprint)
    {
        var projectFingerprintText = projectFingerprint.ToString();
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "call",
          "payload": {
            "project": {
              "projectPath": "/repo/UnityProject",
              "projectFingerprint": "{{projectFingerprintText}}",
              "unityVersion": "6000.1.4f1"
            },
            "opResults": [],
            "postReadSource": {
              "schemaVersion": 1,
              "steps": []
            }
          },
          "errors": []
        }
        """;
    }

    public static string CreateMixedBoundAndUnboundDiagnosticFromJson (ProjectFingerprint projectFingerprint)
    {
        var projectFingerprintText = projectFingerprint.ToString();
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "call",
          "payload": {
            "project": {
              "projectPath": "/repo/UnityProject",
              "projectFingerprint": "{{projectFingerprintText}}",
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
              },
              {
                "opId": "raw-1",
                "op": "ucli.scene.open",
                "phase": "call",
                "applied": true,
                "changed": true,
                "touched": [],
                "diagnostics": [
                  {
                    "code": "READ_SURFACE_PARTIAL",
                    "severity": "warning",
                    "coverageImpact": "partial",
                    "message": "Read surface coverage is partial."
                  }
                ]
              }
            ],
            "postReadSource": {
              "schemaVersion": 1,
              "steps": [
                {
                  "opId": "edit-1",
                  "sourceKind": "edit",
                  "playModeMutation": false,
                  "commit": "none",
                  "persistenceExpected": false,
                  "expectedPostState": "deterministic"
                },
                {
                  "opId": "raw-1",
                  "sourceKind": "operation",
                  "playModeMutation": false,
                  "commit": null,
                  "persistenceExpected": false,
                  "expectedPostState": "unavailable"
                }
              ]
            }
          },
          "errors": []
        }
        """;
    }

    public static IpcUnityLogEvent CreateLogEvent (string cursor)
    {
        return new IpcUnityLogEvent(
            Timestamp: "2026-05-17T00:00:00+00:00",
            Level: "error",
            Source: "runtime",
            Message: "Unity log event.",
            StackTrace: null,
            Cursor: cursor);
    }

}
