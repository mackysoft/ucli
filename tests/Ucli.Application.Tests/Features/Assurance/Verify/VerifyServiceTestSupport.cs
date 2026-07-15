using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

internal static class VerifyServiceTestSupport
{
    public static readonly Guid CompileRunId = Guid.Parse("34c0c330-8798-4ec1-87ae-3d0ae87fc715");
    public static readonly Guid TestRunId = Guid.Parse("83ca6714-565c-4c9d-a3ca-44446393afca");

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
                TestRunId,
                "/repo/.ucli/local/test/test-run-1",
                "/repo/.ucli/local/test/test-run-1/summary.json")),
            logsService ?? new RecordingVerifyLogsUnityService((_, _, _) => ValueTask.FromResult(LogsReadServiceResult.Completed(0, null))),
            profileFileReader ?? new StubVerifyProfileFileReader((profilePath, root) => VerifyProfileFileReadResult.Success(
                File.ReadAllText(Path.Combine(root, profilePath)),
                profilePath.Replace('\\', '/'))),
            fromInputFileReader ?? new StubVerifyFromInputFileReader((fromPath, root) => VerifyFromInputFileReadResult.Success(
                File.ReadAllText(Path.Combine(root, fromPath)))),
            timeProvider ?? TimeProvider.System);
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
        var claimCode = ReadyClaimCodes.ForTarget(target);
        var verifierId = new AssuranceVerifierId("ready.lifecycle");
        return ReadyExecutionResult.Success(new ReadyExecutionOutput(
            Verdict: AssuranceVerdict.Pass,
            Project: project,
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: verifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [claimCode])
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: claimCode,
                    Status: AssuranceClaimStatus.Passed,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: verifierId,
                    Statement: "Unity is ready.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["target"] = ContractLiteralCodec.ToValue(target),
                    },
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKind.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence: [],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: target,
            RequestedMode: AssuranceRequestedExecutionMode.Auto,
            ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
            SessionKind: AssuranceSessionKind.TransientProbe,
            TimeoutMilliseconds: 10000,
            Lifecycle: null,
            ReadIndex: null));
    }

    public static CompileExecutionResult CreateCompileResult (ProjectIdentityInfo project)
    {
        return CreateCompileResult(project, AssuranceClaimStatus.Passed);
    }

    public static CompileExecutionResult CreateCompileResult (
        ProjectIdentityInfo project,
        AssuranceClaimStatus claimStatus)
    {
        var failed = claimStatus == AssuranceClaimStatus.Failed;
        return CompileExecutionResult.Success(new CompileExecutionOutput(
            Verdict: failed ? AssuranceVerdict.Fail : AssuranceVerdict.Pass,
            Project: project,
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: new AssuranceVerifierId("compile"),
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [CompileClaimCodes.UnityCompileNoErrors],
                    Effects: AssuranceEffectSets.Compile,
                    ReportRef: "compile.summary")
            ],
            Claims:
            [
                new CompileClaimOutput(
                    Id: CompileClaimCodes.UnityCompileNoErrors,
                    Status: claimStatus,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: new AssuranceVerifierId("compile"),
                    Statement: "Unity script compilation has no errors.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal),
                    Evidence: [],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                ["compile.summary"] = AssuranceReportReference.FromPath("/repo/.ucli/local/compile/run-1/summary.json", digest: null),
            },
            ResidualRisks: [],
            RequestedMode: AssuranceRequestedExecutionMode.Auto,
            ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
            SessionKind: AssuranceSessionKind.TransientProbe,
            TimeoutMilliseconds: 10000,
            Compile: new CompileOutput(
                runId: CompileRunId,
                refresh: new CompileRefreshOutput(
                    Origin: CompileRefreshOrigin.AssetDatabaseRefresh,
                    Requested: true,
                    StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                    CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:01Z"),
                    Completed: true),
                scriptCompilation: new CompileScriptCompilationOutput(
                    Started: true,
                    Completed: true,
                    CompileGenerationBefore: 1,
                    CompileGenerationAfter: 2,
                    Diagnostics: new CompileDiagnosticsOutput(
                        ErrorCount: failed ? 1 : 0,
                        WarningCount: 0,
                        PrimaryDiagnostic: null)),
                domainReload: new CompileDomainReloadOutput(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: 1,
                    GenerationAfter: 1,
                    Settled: true),
                lifecycle: new CompileLifecycleOutput(
                    ServerVersion: "0.5.0",
                    UnityVersion: "6000.1.4f1",
                    EditorMode: DaemonEditorMode.Batchmode,
                    LifecycleState: IpcEditorLifecycleState.Ready,
                    BlockingReason: null,
                    CompileState: IpcCompileState.Ready,
                    Generations: new IpcUnityGenerationSnapshot(
                        CompileGeneration: 2,
                        DomainReloadGeneration: 1,
                        AssetRefreshGeneration: 1,
                        PlayModeGeneration: 1),
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
            Timestamp: new DateTimeOffset(2026, 5, 17, 0, 0, 0, TimeSpan.Zero),
            Level: IpcLogLevel.Error,
            Source: IpcUnityLogSource.Runtime,
            Message: "Unity log event.",
            StackTrace: null,
            Cursor: cursor);
    }

}
